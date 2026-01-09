#if UNITY_EDITOR
using System;
using System.Linq;
using Azathrix.EnvInstaller.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.UI
{
    /// <summary>
    /// 环境依赖 UI API，供其他代码调用
    /// </summary>
    public static class EnvDependencyUI
    {
        private static EnvManager _manager;
        private static EnvManager Manager => _manager ??= new EnvManager();

        /// <summary>
        /// 通过 ID 显示依赖安装窗口
        /// </summary>
        public static void ShowInstallWindow(string dependencyId, Action onComplete = null)
        {
            var dep = EnvScanner.FindById(dependencyId);
            if (dep == null)
            {
                EditorUtility.DisplayDialog("提示", $"未找到环境依赖: {dependencyId}", "确定");
                return;
            }

            EnvInstallWindow.Show(new[] { dep.Dependency }, onComplete);
        }

        /// <summary>
        /// 通过 ID 列表显示依赖安装窗口
        /// </summary>
        public static void ShowInstallWindow(string[] dependencyIds, Action onComplete = null)
        {
            EnvInstallWindow.ShowByIds(dependencyIds, onComplete);
        }

        /// <summary>
        /// 检查依赖是否已安装
        /// </summary>
        public static bool IsInstalled(string dependencyId)
        {
            var dep = EnvScanner.FindById(dependencyId);
            if (dep == null) return false;
            return Manager.IsInstalled(dep.Dependency);
        }

        /// <summary>
        /// 检查多个依赖是否全部已安装
        /// </summary>
        public static bool AreAllInstalled(string[] dependencyIds)
        {
            foreach (var id in dependencyIds)
            {
                if (!IsInstalled(id))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取未安装的依赖 ID 列表
        /// </summary>
        public static string[] GetMissingDependencies(string[] dependencyIds)
        {
            return dependencyIds.Where(id => !IsInstalled(id)).ToArray();
        }

        /// <summary>
        /// 绘制依赖检查 UI（用于嵌入其他窗口），返回是否所有依赖已安装
        /// </summary>
        public static bool DrawDependencyCheck(string[] dependencyIds)
        {
            var deps = EnvScanner.FindByIds(dependencyIds);
            if (deps == null || deps.Count == 0) return true;

            // 只检查必须依赖
            var requiredDeps = deps.Where(d => !d.Dependency.Optional).ToList();
            if (requiredDeps.Count == 0) return true;

            var missingDeps = requiredDeps.Where(d => !Manager.IsInstalled(d.Dependency)).ToList();
            if (missingDeps.Count == 0) return true;

            // 绘制安装界面
            EditorGUILayout.Space(20);
            GUILayout.FlexibleSpace();

            // 状态标签
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var dep in requiredDeps)
            {
                var installed = Manager.IsInstalled(dep.Dependency);
                DrawStatusLabel(dep.Dependency.GetDisplayName(), installed);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // 安装按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("安装环境", GUILayout.Width(200), GUILayout.Height(40)))
            {
                EnvInstallWindow.Show(missingDeps.Select(d => d.Dependency).ToArray(), () =>
                {
                    if (EditorWindow.focusedWindow != null)
                        EditorWindow.focusedWindow.Repaint();
                });
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            return false;
        }

        /// <summary>
        /// 绘制简单的依赖检查提示（单行）
        /// </summary>
        public static bool DrawSimpleDependencyCheck(string dependencyId)
        {
            var dep = EnvScanner.FindById(dependencyId);
            if (dep == null)
            {
                EditorGUILayout.HelpBox($"未找到环境依赖配置: {dependencyId}", MessageType.Warning);
                return false;
            }

            if (Manager.IsInstalled(dep.Dependency))
                return true;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"需要安装环境依赖: {dep.Dependency.GetDisplayName()}", MessageType.Warning);
            if (GUILayout.Button("安装", GUILayout.Width(60), GUILayout.Height(38)))
            {
                EnvInstallWindow.Show(new[] { dep.Dependency });
            }
            EditorGUILayout.EndHorizontal();

            return false;
        }

        private static void DrawStatusLabel(string label, bool ok)
        {
            var color = ok ? Color.green : Color.gray;
            var icon = ok ? "✓" : "○";
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            GUILayout.Label($"{icon} {label}", style, GUILayout.Width(100));
        }
    }
}
#endif
