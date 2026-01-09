#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 扫描目录查找 env.json 文件
    /// </summary>
    public static class EnvScanner
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// 扫描所有 env.json 并构建依赖索引
        /// </summary>
        public static Dictionary<string, ScannedDependency> ScanAll()
        {
            var result = new Dictionary<string, ScannedDependency>();

            // 1. 扫描 Packages 目录
            ScanDirectory(Path.Combine(ProjectRoot, "Packages"), result, false);

            // 2. 扫描 Assets 目录
            ScanDirectory(Application.dataPath, result, true);

            // 3. 扫描 Library/PackageCache 目录
            ScanDirectory(Path.Combine(ProjectRoot, "Library/PackageCache"), result, false);

            return result;
        }

        /// <summary>
        /// 获取所有扫描到的依赖（按来源分组）
        /// </summary>
        public static Dictionary<string, List<ScannedDependency>> ScanGroupedBySource()
        {
            var all = ScanAll();
            var grouped = new Dictionary<string, List<ScannedDependency>>();

            foreach (var kvp in all)
            {
                var dep = kvp.Value;
                if (!grouped.ContainsKey(dep.SourceName))
                    grouped[dep.SourceName] = new List<ScannedDependency>();
                grouped[dep.SourceName].Add(dep);
            }

            return grouped;
        }

        /// <summary>
        /// 通过 ID 查找依赖
        /// </summary>
        public static ScannedDependency FindById(string id)
        {
            var all = ScanAll();
            return all.TryGetValue(id, out var dep) ? dep : null;
        }

        /// <summary>
        /// 通过 ID 列表查找依赖
        /// </summary>
        public static List<ScannedDependency> FindByIds(string[] ids)
        {
            var all = ScanAll();
            var result = new List<ScannedDependency>();
            foreach (var id in ids)
            {
                if (all.TryGetValue(id, out var dep))
                    result.Add(dep);
            }
            return result;
        }

        private static void ScanDirectory(string dir, Dictionary<string, ScannedDependency> result, bool recursive)
        {
            if (!Directory.Exists(dir)) return;

            if (recursive)
            {
                // 递归扫描所有子目录
                foreach (var envPath in Directory.GetFiles(dir, "env.json", SearchOption.AllDirectories))
                {
                    ProcessEnvFile(envPath, result);
                }
            }
            else
            {
                // 只扫描直接子目录
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var envPath = Path.Combine(subDir, "env.json");
                    if (File.Exists(envPath))
                    {
                        ProcessEnvFile(envPath, result);
                    }
                }
            }
        }

        private static void ProcessEnvFile(string envPath, Dictionary<string, ScannedDependency> result)
        {
            try
            {
                var config = EnvJsonService.Read(envPath);
                if (config?.Dependencies == null) return;

                var sourceName = GetSourceName(envPath);

                foreach (var dep in config.Dependencies)
                {
                    if (string.IsNullOrEmpty(dep.Id)) continue;

                    // 如果已存在相同 ID，跳过（优先使用先扫描到的）
                    if (result.ContainsKey(dep.Id)) continue;

                    result[dep.Id] = new ScannedDependency
                    {
                        Dependency = dep,
                        SourcePath = envPath,
                        SourceName = sourceName
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnvScanner] 解析 {envPath} 失败: {e.Message}");
            }
        }

        private static string GetSourceName(string envPath)
        {
            // 获取 env.json 所在目录的名称
            var dir = Path.GetDirectoryName(envPath);
            if (string.IsNullOrEmpty(dir)) return "Unknown";

            var dirName = Path.GetFileName(dir);

            // 如果是 PackageCache，去掉版本号后缀 (如 com.example.package@1.0.0)
            var atIndex = dirName.IndexOf('@');
            if (atIndex > 0)
                dirName = dirName.Substring(0, atIndex);

            return dirName;
        }
    }
}
#endif
