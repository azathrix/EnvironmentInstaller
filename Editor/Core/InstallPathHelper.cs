#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 安装路径与文件判定辅助
    /// </summary>
    public static class InstallPathHelper
    {
        public static bool AreRequiredFilesPresent(string baseDir, IReadOnlyList<string> requiredFiles)
        {
            if (requiredFiles == null || requiredFiles.Count == 0)
                return false;

            foreach (var relativePath in requiredFiles)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return false;

                var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                if (!IsChildOf(baseDir, fullPath))
                    return false;

                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    return false;
            }

            return true;
        }

        public static HashSet<string> CaptureFiles(string rootDir)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(rootDir))
                return result;

            foreach (var file in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories))
                result.Add(Path.GetFullPath(file));
            return result;
        }

        public static bool IsChildOf(string parent, string path)
        {
            var normalizedParent = NormalizeDir(parent);
            var normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeDir(string path)
        {
            var full = Path.GetFullPath(path);
            if (!full.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !full.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                full += Path.DirectorySeparatorChar;
            }

            return full;
        }

        public static List<string> GetAdditionalTrackedFilesFromRequired(string destDir, IReadOnlyList<string> requiredFiles)
        {
            var result = new List<string>();
            if (requiredFiles == null || requiredFiles.Count == 0)
                return result;

            foreach (var relativePath in requiredFiles.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var fullPath = Path.GetFullPath(Path.Combine(destDir, relativePath));
                if (!IsChildOf(destDir, fullPath))
                    continue;

                if (File.Exists(fullPath))
                {
                    result.Add(fullPath);
                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    result.AddRange(Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories).Select(Path.GetFullPath));
                }
            }

            return result;
        }
    }
}
#endif
