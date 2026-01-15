#if UNITY_EDITOR
using System.Collections.Generic;
using Azathrix.Framework.Editor;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 环境依赖宏定义管理（使用框架的 DefineSymbolManager）
    /// </summary>
    public static class EnvDefineHelper
    {
        /// <summary>
        /// 同步依赖的宏定义状态
        /// </summary>
        public static void SyncDefines(IEnumerable<ScannedDependency> dependencies, EnvManager manager)
        {
            foreach (var scanned in dependencies)
            {
                var dep = scanned.Dependency;
                if (string.IsNullOrEmpty(dep.DefineSymbol)) continue;

                var isInstalled = manager.IsInstalled(dep);
                var hasDefine = DefineSymbolManager.Has(dep.DefineSymbol);

                if (isInstalled && !hasDefine)
                    DefineSymbolManager.Add(dep.DefineSymbol);
                else if (!isInstalled && hasDefine)
                    DefineSymbolManager.Remove(dep.DefineSymbol);
            }
        }
    }
}
#endif
