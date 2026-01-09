#if UNITY_EDITOR
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;

namespace Azathrix.EnvInstaller.Editor.Installers
{
    /// <summary>
    /// 环境安装器接口
    /// </summary>
    public interface IEnvInstaller
    {
        DownloadType SupportedType { get; }
        UniTask<bool> InstallAsync(EnvDependency dep, string destDir, Downloader downloader, CancellationToken ct = default);
        UniTask<bool> InstallFromCacheAsync(EnvDependency dep, string destDir, string cachePath, CancellationToken ct = default);
        bool IsInstalled(EnvDependency dep, string destDir);
    }
}
#endif
