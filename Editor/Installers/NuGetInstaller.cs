#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;

namespace Azathrix.EnvInstaller.Editor.Installers
{
    /// <summary>
    /// NuGet 包安装器，从 nuget.org 下载并安装 .NET 包
    /// </summary>
    public class NuGetInstaller : IEnvInstaller
    {
        public DownloadType SupportedType => DownloadType.NuGet;

        public async UniTask<bool> InstallAsync(EnvDependency dep, string destDir, Downloader downloader, CancellationToken ct = default)
        {
            var packageName = string.IsNullOrEmpty(dep.PackageId) ? dep.Id : dep.PackageId;
            var version = dep.Version ?? "latest";
            var cachePath = EnvManager.GetCachePath(packageName, version, ".nupkg");
            var tempDir = Path.Combine(Path.GetTempPath(), $"nuget_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(destDir);

                if (!File.Exists(cachePath))
                {
                    var url = $"https://www.nuget.org/api/v2/package/{packageName}/{version}";
                    if (!await downloader.DownloadAsync(url, cachePath, ct))
                        return false;
                }

                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return false;

                var frameworks = !string.IsNullOrEmpty(dep.TargetFramework)
                    ? new[] { dep.TargetFramework }
                    : new[] { "netstandard2.0", "netstandard2.1", "netstandard1.1" };

                foreach (var fw in frameworks)
                {
                    var path = Path.Combine(extractDir, "lib", fw, $"{packageName}.dll");
                    if (File.Exists(path))
                    {
                        File.Copy(path, Path.Combine(destDir, $"{packageName}.dll"), true);
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        public UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string destDir, string cachePath, CancellationToken ct = default)
        {
            var packageName = string.IsNullOrEmpty(dep.PackageId) ? dep.Id : dep.PackageId;
            var tempDir = Path.Combine(Path.GetTempPath(), $"nuget_{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(destDir);
                var extractDir = Path.Combine(tempDir, "extracted");
                if (!Extractor.ExtractZip(cachePath, extractDir))
                    return UniTask.FromResult(false);

                var frameworks = !string.IsNullOrEmpty(dep.TargetFramework)
                    ? new[] { dep.TargetFramework }
                    : new[] { "netstandard2.0", "netstandard2.1", "netstandard1.1" };

                foreach (var fw in frameworks)
                {
                    var path = Path.Combine(extractDir, "lib", fw, $"{packageName}.dll");
                    if (File.Exists(path))
                    {
                        File.Copy(path, Path.Combine(destDir, $"{packageName}.dll"), true);
                        return UniTask.FromResult(true);
                    }
                }
                return UniTask.FromResult(false);
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
            if (dep.RequiredFiles != null)
            {
                foreach (var file in dep.RequiredFiles)
                {
                    if (File.Exists(Path.Combine(destDir, file)))
                        return true;
                }
            }
            var packageName = string.IsNullOrEmpty(dep.PackageId) ? dep.Id : dep.PackageId;
            return File.Exists(Path.Combine(destDir, $"{packageName}.dll"));
        }
    }
}
#endif
