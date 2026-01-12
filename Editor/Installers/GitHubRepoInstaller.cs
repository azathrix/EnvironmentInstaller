#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Azathrix.EnvInstaller.Editor.Installers
{
    /// <summary>
    /// GitHub 仓库安装器，从 GitHub 仓库下载源码或资源
    /// </summary>
    public class GitHubRepoInstaller : IEnvInstaller
    {
        public DownloadType SupportedType => DownloadType.GitHubRepo;

        public async UniTask<bool> InstallAsync(EnvDependency dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var (owner, repo, refName, isTag) = await ParseUrlAsync(dep.Url, dep.Branch);
            var cachePath = EnvManager.GetCachePath($"{owner}_{repo}", refName, ".zip");
            var tempDir = Path.Combine(Path.GetTempPath(), $"github_{Guid.NewGuid()}");

            try
            {
                var cacheValid = File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
                if (!cacheValid)
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    // tag 使用 refs/tags/，branch 使用 refs/heads/
                    var refType = isTag ? "tags" : "heads";
                    var url = $"https://github.com/{owner}/{repo}/archive/refs/{refType}/{refName}.zip";
                    Debug.Log($"[GitHubRepoInstaller] 开始下载: {url}");
                    if (!await downloader.DownloadAsync(url, cachePath, ct))
                    {
                        Debug.LogError($"[GitHubRepoInstaller] 下载失败: {url}");
                        return false;
                    }
                }

                if (!File.Exists(cachePath))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 下载后文件不存在: {cachePath}");
                    return false;
                }

                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 解压失败: {cachePath}");
                    return false;
                }

                var dirs = Directory.GetDirectories(extractDir);
                var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;

                var srcDir = string.IsNullOrEmpty(dep.SubPath)
                    ? rootDir
                    : Path.Combine(rootDir, dep.SubPath);

                if (!Directory.Exists(srcDir))
                {
                    Debug.LogError($"[GitHubRepoInstaller] 源目录不存在: {srcDir}");
                    return false;
                }

                Directory.CreateDirectory(destDir);
                CopyDirectory(srcDir, destDir);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GitHubRepoInstaller] 安装异常: {e}");
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"github_{Guid.NewGuid()}");
            try
            {
                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return UniTask.FromResult(false);

                var dirs = Directory.GetDirectories(extractDir);
                var rootDir = dirs.Length > 0 ? dirs[0] : extractDir;
                var srcDir = string.IsNullOrEmpty(dep.SubPath) ? rootDir : Path.Combine(rootDir, dep.SubPath);

                if (!Directory.Exists(srcDir))
                    return UniTask.FromResult(false);

                Directory.CreateDirectory(destDir);
                CopyDirectory(srcDir, destDir);
                return UniTask.FromResult(true);
            }
            catch
            {
                return UniTask.FromResult(false);
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public bool IsInstalled(EnvDependency dep, string destDir)
        {
            if (!Directory.Exists(destDir))
                return false;

            if (dep.RequiredFiles != null)
            {
                foreach (var file in dep.RequiredFiles)
                {
                    if (File.Exists(Path.Combine(destDir, file)))
                        return true;
                }
                return false;
            }

            return Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories).Length > 0;
        }

        private static async UniTask<(string owner, string repo, string refName, bool isTag)> ParseUrlAsync(string url, string defaultBranch)
        {
            var refName = string.IsNullOrEmpty(defaultBranch) ? "master" : defaultBranch;
            var isTag = false;

            // 解析 owner/repo
            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                url = parts[0];
                refName = parts[1];
            }
            var segments = url.Split('/');
            var owner = segments[0];
            var repo = segments[1];

            // 支持 tag: 前缀来指定 tag
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
                        Debug.Log($"[GitHubRepoInstaller] 获取到最新 release: {refName}");
                    }
                    else
                    {
                        Debug.LogWarning("[GitHubRepoInstaller] 无法获取最新 release，使用 main 分支");
                        refName = "main";
                        isTag = false;
                    }
                }
            }

            return (owner, repo, refName, isTag);
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

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
#endif
