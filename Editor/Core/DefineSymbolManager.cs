#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
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
                var symbol = ResolveDefineSymbol(dep);
                if (string.IsNullOrEmpty(symbol)) continue;

                var isInstalled = manager.IsInstalled(dep);
                var hasDefine = DefineSymbolManager.Has(symbol);

                if (isInstalled && !hasDefine)
                    DefineSymbolManager.Add(symbol);
                else if (!isInstalled)
                    DefineSymbolManager.Remove(symbol);
            }
        }

        private static string ResolveDefineSymbol(EnvDependency dep)
        {
            var raw = !string.IsNullOrWhiteSpace(dep.DefineSymbol) ? dep.DefineSymbol : dep.Id;
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
    }
}
#endif
