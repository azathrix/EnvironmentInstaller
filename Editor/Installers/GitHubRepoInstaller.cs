#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
            var (owner, repo, branch) = ParseUrl(dep.Url, dep.Branch);
            var cachePath = EnvManager.GetCachePath($"{owner}_{repo}", branch, ".zip");
            var tempDir = Path.Combine(Path.GetTempPath(), $"github_{Guid.NewGuid()}");

            try
            {
                var cacheValid = File.Exists(cachePath) && new FileInfo(cachePath).Length > 1024;
                if (!cacheValid)
                {
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                    var url = $"https://github.com/{owner}/{repo}/archive/refs/heads/{branch}.zip";
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

        private static (string owner, string repo, string branch) ParseUrl(string url, string defaultBranch)
        {
            var branch = string.IsNullOrEmpty(defaultBranch) ? "master" : defaultBranch;
            if (url.Contains("@"))
            {
                var parts = url.Split('@');
                url = parts[0];
                branch = parts[1];
            }
            var segments = url.Split('/');
            return (segments[0], segments[1], branch);
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
