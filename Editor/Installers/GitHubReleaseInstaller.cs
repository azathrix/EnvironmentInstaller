#if UNITY_EDITOR
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;

namespace Azathrix.EnvInstaller.Editor.Installers
{
    /// <summary>
    /// GitHub Release 安装器，从 GitHub Release 页面下载资源
    /// </summary>
    public class GitHubReleaseInstaller : IEnvInstaller
    {
        public DownloadType SupportedType => DownloadType.GitHubRelease;

        public async UniTask<bool> InstallAsync(EnvDependency dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(dep.Url).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".7z";
            var cachePath = EnvManager.GetCachePath(dep.Id, dep.Version, ext);

            try
            {
                if (!File.Exists(cachePath))
                {
                    if (!await downloader.DownloadAsync(dep.Url, cachePath, ct))
                        return false;
                }

                Directory.CreateDirectory(destDir);

                if (ext is ".7z" or ".zip")
                {
                    if (ext == ".7z" && !Extractor.Is7zAvailable())
                    {
                        if (!await Extractor.Install7zAsync(downloader))
                            return false;
                    }

                    if (!Extractor.Extract(cachePath, destDir))
                        return false;
                }
                else
                {
                    var fileName = Path.GetFileName(dep.Url);
                    File.Copy(cachePath, Path.Combine(destDir, fileName), true);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(cachePath).ToLowerInvariant();
            try
            {
                Directory.CreateDirectory(destDir);
                if (ext is ".7z" or ".zip")
                {
                    if (!Extractor.Extract(cachePath, destDir))
                        return UniTask.FromResult(false);
                }
                else
                {
                    var fileName = Path.GetFileName(dep.Url);
                    File.Copy(cachePath, Path.Combine(destDir, fileName), true);
                }
                return UniTask.FromResult(true);
            }
            catch
            {
                return UniTask.FromResult(false);
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
    }
}
#endif
