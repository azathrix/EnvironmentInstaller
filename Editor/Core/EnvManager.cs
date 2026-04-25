#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Installers;
using Azathrix.Framework.Editor;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 环境依赖管理器
    /// </summary>
    public class EnvManager
    {
        private const string DefineSource = "EnvInstaller";

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
            if (dep == null) return false;

            var trackFiles = ShouldTrackInstalledFiles(dep);
            var destDir = trackFiles ? GetInstallPath(dep) : null;
            var rootExistedBefore = trackFiles && Directory.Exists(destDir);
            var filesBefore = trackFiles ? InstallPathHelper.CaptureFiles(destDir) : null;

            bool result;
            if (dep.InstallType == InstallType.UnityPackage)
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                result = await _unityPackageInstaller.InstallAsync(dep, ct);
            }
            else if (dep.InstallType == InstallType.Manual)
            {
                OnStatusChanged?.Invoke($"正在检查 {dep.GetDisplayName()}...");
                result = await _manualInstaller.InstallAsync(dep, ct);
            }
            else if (!_installers.TryGetValue(dep.DownloadType, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的下载类型: {dep.DownloadType}");
                return false;
            }
            else
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                result = await installer.InstallAsync(dep, destDir, _downloader, ct);
            }

            if (result)
            {
                OnInstallSucceeded(dep, destDir, rootExistedBefore, filesBefore);
            }

            return result;
        }

        /// <summary>
        /// 从缓存安装
        /// </summary>
        public async UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string cachePath, CancellationToken ct = default)
        {
            if (dep == null) return false;

            var trackFiles = ShouldTrackInstalledFiles(dep);
            var destDir = trackFiles ? GetInstallPath(dep) : null;
            var rootExistedBefore = trackFiles && Directory.Exists(destDir);
            var filesBefore = trackFiles ? InstallPathHelper.CaptureFiles(destDir) : null;

            bool result;
            if (dep.InstallType == InstallType.UnityPackage)
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                result = await _unityPackageInstaller.InstallAsync(dep, ct);
            }
            else if (dep.InstallType == InstallType.Manual)
            {
                OnStatusChanged?.Invoke($"正在检查 {dep.GetDisplayName()}...");
                result = await _manualInstaller.InstallAsync(dep, ct);
            }
            else if (!_installers.TryGetValue(dep.DownloadType, out var installer))
            {
                OnStatusChanged?.Invoke($"不支持的下载类型: {dep.DownloadType}");
                return false;
            }
            else
            {
                OnStatusChanged?.Invoke($"正在安装 {dep.GetDisplayName()}...");
                result = await installer.InstallFromCacheAsync(dep, destDir, cachePath, ct);
            }

            if (result)
            {
                OnInstallSucceeded(dep, destDir, rootExistedBefore, filesBefore);
            }

            return result;
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
            if (dep == null) return false;

            bool result;
            if (dep.InstallType == InstallType.UnityPackage)
            {
                result = _unityPackageInstaller.Uninstall(dep);
                if (result)
                    InstallStateStore.RemoveRecord(dep.Id);
            }
            else if (dep.InstallType == InstallType.Manual)
            {
                // Manual 依赖仅移除宏，不删除用户手动导入的文件
                result = true;
            }
            else
            {
                var path = GetInstallPath(dep);
                result = SafeUninstallFiles(dep, path);
                if (result)
                    InstallStateStore.RemoveRecord(dep.Id);
            }

            if (!result)
                return false;

            RemoveDefine(dep);
            RefreshCompilation();
            return true;
        }

        private static bool ShouldTrackInstalledFiles(EnvDependency dep)
        {
            return dep.InstallType != InstallType.UnityPackage && dep.InstallType != InstallType.Manual;
        }

        private static void OnInstallSucceeded(EnvDependency dep, string destDir, bool rootExistedBefore, HashSet<string> filesBefore)
        {
            if (ShouldTrackInstalledFiles(dep))
            {
                RecordInstalledFiles(dep, destDir, rootExistedBefore, filesBefore);
            }
            else
            {
                InstallStateStore.RemoveRecord(dep.Id);
            }

            AddDefine(dep);
            RefreshCompilation();
        }

        private static void RecordInstalledFiles(EnvDependency dep, string destDir, bool rootExistedBefore, HashSet<string> filesBefore)
        {
            if (string.IsNullOrEmpty(dep.Id) || string.IsNullOrEmpty(destDir))
                return;

            var before = filesBefore ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var after = InstallPathHelper.CaptureFiles(destDir);
            var installedFiles = after.Except(before, StringComparer.OrdinalIgnoreCase).ToList();

            if (installedFiles.Count == 0)
            {
                installedFiles.AddRange(InstallPathHelper.GetAdditionalTrackedFilesFromRequired(destDir, dep.RequiredFiles));
            }

            InstallStateStore.UpsertRecord(new InstallRecord
            {
                DependencyId = dep.Id,
                TargetPath = Path.GetFullPath(destDir),
                RootCreated = !rootExistedBefore && Directory.Exists(destDir),
                InstalledFiles = installedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            });
        }

        private bool SafeUninstallFiles(EnvDependency dep, string targetDir)
        {
            try
            {
                var record = InstallStateStore.GetRecord(dep.Id);
                if (record != null &&
                    !string.IsNullOrEmpty(record.TargetPath) &&
                    PathsEqual(record.TargetPath, targetDir))
                {
                    var removed = RemoveTrackedFiles(targetDir, record.InstalledFiles);
                    if (record.RootCreated)
                        removed |= DeleteDirectoryIfEmpty(targetDir);
                    if (!removed && CanDeleteWholeTarget(dep, targetDir))
                        removed = DeleteDirectoryWithMeta(targetDir);
                    return removed;
                }

                if (TryRemoveRequiredFiles(dep, targetDir, out var removedRequired))
                    return removedRequired;

                if (CanDeleteWholeTarget(dep, targetDir))
                    return DeleteDirectoryWithMeta(targetDir);

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryRemoveRequiredFiles(EnvDependency dep, string targetDir, out bool removedAny)
        {
            removedAny = false;
            if (dep.RequiredFiles == null || dep.RequiredFiles.Length == 0)
                return false;

            foreach (var relativePath in dep.RequiredFiles.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var fullPath = Path.GetFullPath(Path.Combine(targetDir, relativePath));
                if (!InstallPathHelper.IsChildOf(targetDir, fullPath))
                    continue;

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    DeleteMetaFile(fullPath);
                    RemoveEmptyParents(fullPath, targetDir);
                    removedAny = true;
                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    DeleteMetaFile(fullPath);
                    RemoveEmptyParents(fullPath, targetDir);
                    removedAny = true;
                }
            }

            DeleteDirectoryIfEmpty(targetDir);
            return true;
        }

        private static bool RemoveTrackedFiles(string targetDir, IEnumerable<string> trackedFiles)
        {
            var removedAny = false;
            if (trackedFiles == null)
                return false;

            foreach (var filePath in trackedFiles.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!InstallPathHelper.IsChildOf(targetDir, fullPath))
                    continue;

                if (!File.Exists(fullPath))
                    continue;

                File.Delete(fullPath);
                DeleteMetaFile(fullPath);
                RemoveEmptyParents(fullPath, targetDir);
                removedAny = true;
            }

            DeleteDirectoryIfEmpty(targetDir);
            return removedAny;
        }

        private static bool CanDeleteWholeTarget(EnvDependency dep, string targetDir)
        {
            if (!Directory.Exists(targetDir))
                return false;

            if (string.IsNullOrEmpty(dep.TargetDir))
                return true;

            var defaultPath = Path.Combine(DefaultInstallDir, dep.Id ?? string.Empty);
            return PathsEqual(defaultPath, targetDir);
        }

        private static bool DeleteDirectoryIfEmpty(string directory)
        {
            if (!Directory.Exists(directory))
                return false;

            if (Directory.EnumerateFileSystemEntries(directory).Any())
                return false;

            Directory.Delete(directory, false);
            DeleteMetaFile(directory);
            return true;
        }

        private static bool DeleteDirectoryWithMeta(string directory)
        {
            if (!Directory.Exists(directory))
                return false;

            Directory.Delete(directory, true);
            DeleteMetaFile(directory);
            return true;
        }

        private static void DeleteMetaFile(string assetPath)
        {
            var metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }

        private static void RemoveEmptyParents(string path, string stopDir)
        {
            var current = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(current) &&
                   InstallPathHelper.IsChildOf(stopDir, current) &&
                   !PathsEqual(current, stopDir))
            {
                if (!Directory.Exists(current))
                {
                    current = Path.GetDirectoryName(current);
                    continue;
                }

                if (Directory.EnumerateFileSystemEntries(current).Any())
                    break;

                Directory.Delete(current, false);
                DeleteMetaFile(current);
                current = Path.GetDirectoryName(current);
            }
        }

        private static void AddDefine(EnvDependency dep)
        {
            var symbol = GetDefineSymbol(dep);
            if (string.IsNullOrEmpty(symbol)) return;
            DefineSymbolManager.Add(symbol, DefineSource);
        }

        private static bool RemoveDefine(EnvDependency dep)
        {
            var symbol = GetDefineSymbol(dep);
            if (string.IsNullOrEmpty(symbol)) return false;

            var has = DefineSymbolManager.Has(symbol);
            DefineSymbolManager.Remove(symbol);
            return has;
        }

        private static string GetDefineSymbol(EnvDependency dep)
        {
            if (!string.IsNullOrWhiteSpace(dep.DefineSymbol))
                return NormalizeDefine(dep.DefineSymbol);

            return NormalizeDefine(dep.Id);
        }

        private static string NormalizeDefine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var sb = new StringBuilder(raw.Length + 4);
            foreach (var ch in raw.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    sb.Append(char.ToUpperInvariant(ch));
                else
                    sb.Append('_');
            }

            while (sb.Length > 0 && sb[0] == '_')
                sb.Remove(0, 1);

            if (sb.Length == 0)
                return null;

            if (char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void RefreshCompilation()
        {
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
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
