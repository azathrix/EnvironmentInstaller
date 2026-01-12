#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 下载任务状态
    /// </summary>
    public enum TaskState { Downloading, Downloaded, Installing, Completed, Failed, Cancelled }

    /// <summary>
    /// 下载服务，管理依赖包的下载和安装队列
    /// </summary>
    [InitializeOnLoad]
    public static class DownloadService
    {
        public class DownloadTask
        {
            public string Id;
            public EnvDependency Dep;
            public float Progress;
            public long Downloaded;
            public long Total;
            public long Speed;
            public TaskState State;
            public string Error;
            public CancellationTokenSource Cts;
            public string CachePath;
            public bool IsRunning => State == TaskState.Downloading || State == TaskState.Installing;
        }

        private static readonly Dictionary<string, DownloadTask> _tasks = new();
        private static readonly Queue<DownloadTask> _installQueue = new();
        private static bool _isInstalling;
        public static event Action OnTasksChanged;

        static DownloadService()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (!_isInstalling && _installQueue.Count > 0)
            {
                var task = _installQueue.Dequeue();
                if (task.State == TaskState.Downloaded)
                    RunInstallAsync(task).Forget();
            }
        }

        public static IReadOnlyDictionary<string, DownloadTask> Tasks => _tasks;

        public static bool IsDownloading(string depId) =>
            _tasks.TryGetValue(depId, out var t) && t.IsRunning;

        public static DownloadTask GetTask(string depId) =>
            _tasks.TryGetValue(depId, out var t) ? t : null;

        public static bool HasCache(EnvDependency dep)
        {
            var cachePath = GetCachePathForDep(dep);
            return File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
        }

        public static void DeleteCache(EnvDependency dep)
        {
            var cachePath = GetCachePathForDep(dep);
            try { if (File.Exists(cachePath)) File.Delete(cachePath); } catch { }
            _tasks.Remove(dep.Id);
            OnTasksChanged?.Invoke();
        }

        public static void StartInstallFromCache(EnvDependency dep)
        {
            if (_tasks.TryGetValue(dep.Id, out var existing) && existing.IsRunning)
                return;

            var cachePath = GetCachePathForDep(dep);
            if (!File.Exists(cachePath))
                return;

            var task = new DownloadTask
            {
                Id = dep.Id,
                Dep = dep,
                Progress = 1f,
                State = TaskState.Downloaded,
                Cts = new CancellationTokenSource(),
                CachePath = cachePath
            };
            _tasks[dep.Id] = task;
            _installQueue.Enqueue(task);
            OnTasksChanged?.Invoke();
        }

        public static void StartDownload(EnvDependency dep)
        {
            if (_tasks.TryGetValue(dep.Id, out var existing))
            {
                if (existing.IsRunning || existing.State == TaskState.Downloaded)
                    return;
                _tasks.Remove(dep.Id);
            }

            var task = new DownloadTask
            {
                Id = dep.Id,
                Dep = dep,
                Progress = 0.01f,
                State = TaskState.Downloading,
                Cts = new CancellationTokenSource()
            };
            _tasks[dep.Id] = task;
            OnTasksChanged?.Invoke();

            // UnityPackage 类型不需要下载，直接安装
            if (dep.InstallType == InstallType.UnityPackage)
            {
                task.State = TaskState.Downloaded;
                task.Progress = 1f;
                _installQueue.Enqueue(task);
                OnTasksChanged?.Invoke();
                return;
            }

            RunDownloadAsync(task).Forget();
        }

        public static void CancelDownload(string depId)
        {
            if (_tasks.TryGetValue(depId, out var task))
            {
                task.Cts?.Cancel();
                task.State = TaskState.Cancelled;
                _tasks.Remove(depId);
                OnTasksChanged?.Invoke();
            }
        }

        private static async UniTaskVoid RunDownloadAsync(DownloadTask task)
        {
            Debug.Log($"[DownloadService] 开始下载: {task.Dep.GetDisplayName()}");

            var downloader = new Downloader();
            downloader.OnProgress += (p, d, t, s) =>
            {
                task.Progress = p;
                task.Downloaded = d;
                task.Total = t;
                task.Speed = s;
                EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
            };

            try
            {
                var dep = task.Dep;
                var cachePath = GetCachePathForDep(dep);
                task.CachePath = cachePath;

                var cacheValid = File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
                if (cacheValid)
                {
                    Debug.Log($"[DownloadService] 使用缓存: {cachePath}");
                    task.State = TaskState.Downloaded;
                    task.Progress = 1f;
                }
                else
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);

                    var url = await GetDownloadUrlAsync(dep);

                    if (string.IsNullOrEmpty(url))
                    {
                        task.State = TaskState.Failed;
                        task.Error = "无法获取下载地址";
                        return;
                    }
                    Debug.Log($"[DownloadService] 请求地址: {url}");
                    var success = await downloader.DownloadAsync(url, cachePath, task.Cts.Token);
                    if (!success || !File.Exists(cachePath) || new FileInfo(cachePath).Length < 1024)
                    {
                        task.State = TaskState.Failed;
                        task.Error = "下载失败";
                        return;
                    }
                    task.State = TaskState.Downloaded;
                }

                Debug.Log($"[DownloadService] 下载完成: {task.Dep.GetDisplayName()}");
                _installQueue.Enqueue(task);
                EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
            }
            catch (OperationCanceledException)
            {
                task.State = TaskState.Cancelled;
                task.Error = "已取消";
            }
            catch (Exception e)
            {
                task.State = TaskState.Failed;
                task.Error = e.Message;
                Debug.LogError($"[DownloadService] 下载失败: {e}");
            }

            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
        }

        private static async UniTaskVoid RunInstallAsync(DownloadTask task)
        {
            _isInstalling = true;
            task.State = TaskState.Installing;
            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();

            Debug.Log($"[DownloadService] 开始安装: {task.Dep.GetDisplayName()}");

            try
            {
                var manager = new EnvManager();
                var success = await manager.InstallFromCacheAsync(task.Dep, task.CachePath, task.Cts.Token);

                task.State = success ? TaskState.Completed : TaskState.Failed;
                if (!success)
                {
                    task.Error = "安装失败，缓存可能已损坏";
                    try { if (File.Exists(task.CachePath)) File.Delete(task.CachePath); } catch { }
                }

                if (success)
                {
                    EditorApplication.delayCall += () =>
                    {
                        AssetDatabase.Refresh();
                        OnTasksChanged?.Invoke();
                    };
                }
            }
            catch (Exception e)
            {
                task.State = TaskState.Failed;
                task.Error = "安装失败，缓存可能已损坏";
                try { if (File.Exists(task.CachePath)) File.Delete(task.CachePath); } catch { }
                Debug.LogError($"[DownloadService] 安装失败: {e}");
            }

            _isInstalling = false;
            EditorApplication.delayCall += () => OnTasksChanged?.Invoke();
        }

        private static string GetCachePathForDep(EnvDependency dep)
        {
            var ext = dep.DownloadType switch
            {
                DownloadType.NuGet => ".nupkg",
                DownloadType.DirectUrl or DownloadType.GitHubRelease => Path.GetExtension(dep.Url),
                _ => ".zip"
            };
            if (string.IsNullOrEmpty(ext)) ext = ".zip";
            return EnvManager.GetCachePath(dep.Id, dep.Version, ext);
        }

        private static async UniTask<string> GetDownloadUrlAsync(EnvDependency dep)
        {
            return dep.DownloadType switch
            {
                DownloadType.GitHubRepo => await GetGitHubRepoUrlAsync(dep.Url, dep.Branch),
                DownloadType.GitHubRelease => dep.Url,
                DownloadType.NuGet => $"https://www.nuget.org/api/v2/package/{dep.PackageId ?? dep.Id}/{dep.Version}",
                DownloadType.DirectUrl => dep.Url,
                _ => null
            };
        }

        private static async UniTask<string> GetGitHubRepoUrlAsync(string url, string defaultBranch)
        {
            var refName = string.IsNullOrEmpty(defaultBranch) ? "master" : defaultBranch;
            var isTag = false;

            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                url = parts[0];
                refName = parts[1];
            }
            var segments = url.Split('/');
            var owner = segments[0];
            var repo = segments[1];

            // 支持 tag: 前缀
            if (refName.StartsWith("tag:"))
            {
                refName = refName.Substring(4);
                isTag = true;

                // 支持 latest 获取最新 release tag
                if (refName.Equals("latest", StringComparison.OrdinalIgnoreCase))
                {
                    var latestTag = await GetLatestReleaseTagAsync(owner, repo);
                    if (!string.IsNullOrEmpty(latestTag))
                    {
                        refName = latestTag;
                        Debug.Log($"[DownloadService] 获取到最新 release: {refName}");
                    }
                    else
                    {
                        Debug.LogWarning("[DownloadService] 无法获取最新 release，使用 main 分支");
                        refName = "main";
                        isTag = false;
                    }
                }
            }

            var refType = isTag ? "tags" : "heads";
            return $"https://github.com/{owner}/{repo}/archive/refs/{refType}/{refName}.zip";
        }

        private static async UniTask<string> GetLatestReleaseTagAsync(string owner, string repo)
        {
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            using var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("User-Agent", "Unity");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                return null;

            // 简单解析 JSON 获取 tag_name
            var json = request.downloadHandler.text;
            var tagKey = "\"tag_name\":\"";
            var startIdx = json.IndexOf(tagKey, StringComparison.Ordinal);
            if (startIdx < 0) return null;

            startIdx += tagKey.Length;
            var endIdx = json.IndexOf("\"", startIdx, StringComparison.Ordinal);
            return endIdx > startIdx ? json.Substring(startIdx, endIdx - startIdx) : null;
        }

        public static void ClearCompleted()
        {
            var toRemove = new List<string>();
            foreach (var kvp in _tasks)
                if (!kvp.Value.IsRunning)
                    toRemove.Add(kvp.Key);
            foreach (var id in toRemove)
                _tasks.Remove(id);
            OnTasksChanged?.Invoke();
        }
    }
}
#endif
