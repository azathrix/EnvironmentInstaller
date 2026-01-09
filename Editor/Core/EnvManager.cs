#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Installers;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 环境依赖管理器
    /// </summary>
    public class EnvManager
    {
        public static string DefaultInstallDir => Path.Combine(Application.dataPath, "Plugins/Env");
        public static string CacheDir => Path.Combine(Application.dataPath, "../Library/EnvCache");
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private readonly Dictionary<DownloadType, IEnvInstaller> _installers;
        private readonly UnityPackageInstaller _unityPackageInstaller;
        private readonly ManualImportInstaller _manualInstaller;
        private readonly Downloader _downloader;

        public event Action<string> OnStatusChanged;
        public event Action<float, long, long> OnProgress;

        public EnvManager()
        {
            _downloader = new Downloader();
            _downloader.OnProgress += (p, d, t, s) => OnProgress?.Invoke(p, d, t);

            _installers = new Dictionary<DownloadType, IEnvInstaller>
            {
                { DownloadType.DirectUrl, new DirectUrlInstaller() },
                { DownloadType.GitHubRelease, new GitHubReleaseInstaller() },
                { DownloadType.GitHubRepo, new GitHubRepoInstaller() },
                { DownloadType.NuGet, new NuGetInstaller() }
            };

            _unityPackageInstaller = new UnityPackageInstaller();
            _manualInstaller = new ManualImportInstaller();
        }

        /// <summary>
        /// 获取安装路径
        /// </summary>
        public string GetInstallPath(EnvDependency dep)
        {
            if (!string.IsNullOrEmpty(dep.TargetDir))
                return Path.GetFullPath(Path.Combine(ProjectRoot, dep.TargetDir));
            return Path.Combine(DefaultInstallDir, dep.Id);
        }

        /// <summary>
        /// 检查是否已安装
        /// </summary>
        public bool IsInstalled(EnvDependency dep)
        {
            // 特殊处理 UnityPackage 类型
            if (dep.InstallType == InstallType.UnityPackage)
                return _unityPackageInstaller.IsInstalled(dep);

            // 特殊处理 Manual 类型
            if (dep.InstallType == InstallType.Manual)
                return _manualInstaller.IsInstalled(dep);

            if (!_installers.TryGetValue(dep.DownloadType, out var installer))
                return false;
            return installer.IsInstalled(dep, GetInstallPath(dep));
        }

        /// <summary>
        /// 安装依赖
        /// </summary>
        public async UniTask<bool> InstallAsync(EnvDependency dep, CancellationToken ct = default)
        {
            // 特殊处理 UnityPackage 类型
            if (dep.InstallType == InstallType.UnityPackage)
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                return await _unityPackageInstaller.InstallAsync(dep, ct);
            }

            // 特殊处理 Manual 类型
            if (dep.InstallType == InstallType.Manual)
            {
                OnStatusChanged?.Invoke($"正在检查 {dep.GetDisplayName()}...");
                return await _manualInstaller.InstallAsync(dep, ct);
            }

            if (!_installers.TryGetValue(dep.DownloadType, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的下载类型: {dep.DownloadType}");
                return false;
            }

            OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
            var destDir = GetInstallPath(dep);
            return await installer.InstallAsync(dep, destDir, _downloader, ct);
        }

        /// <summary>
        /// 从缓存安装
        /// </summary>
        public async UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string cachePath, CancellationToken ct = default)
        {
            // 特殊处理 UnityPackage 类型
            if (dep.InstallType == InstallType.UnityPackage)
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                return await _unityPackageInstaller.InstallAsync(dep, ct);
            }

            // 特殊处理 Manual 类型
            if (dep.InstallType == InstallType.Manual)
            {
                OnStatusChanged?.Invoke($"正在检查 {dep.GetDisplayName()}...");
                return await _manualInstaller.InstallAsync(dep, ct);
            }

            if (!_installers.TryGetValue(dep.DownloadType, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的下载类型: {dep.DownloadType}");
                return false;
            }

            OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
            var destDir = GetInstallPath(dep);
            return await installer.InstallFromCacheAsync(dep, destDir, cachePath, ct);
        }

        /// <summary>
        /// 获取缓存文件路径
        /// </summary>
        public static string GetCachePath(string id, string version, string ext)
        {
            var cacheDir = Path.GetFullPath(CacheDir);
            Directory.CreateDirectory(cacheDir);
            var fileName = string.IsNullOrEmpty(version) ? $"{id}{ext}" : $"{id}_{version}{ext}";
            return Path.Combine(cacheDir, fileName);
        }

        /// <summary>
        /// 卸载依赖
        /// </summary>
        public bool Uninstall(EnvDependency dep)
        {
            // 特殊处理 UnityPackage 类型
            if (dep.InstallType == InstallType.UnityPackage)
                return _unityPackageInstaller.Uninstall(dep);

            // Manual 类型不支持卸载
            if (dep.InstallType == InstallType.Manual)
                return false;

            var path = GetInstallPath(dep);
            if (!Directory.Exists(path)) return false;
            try
            {
                Directory.Delete(path, true);
                var metaPath = path + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            if (Directory.Exists(CacheDir))
                Directory.Delete(CacheDir, true);
        }
    }
}
#endif
