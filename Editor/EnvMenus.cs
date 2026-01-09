#if UNITY_EDITOR
using System.IO;
using Azathrix.EnvInstaller.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor
{
    /// <summary>
    /// 环境安装器菜单
    /// </summary>
    public static class EnvMenus
    {
        [MenuItem("Azathrix/环境安装器/清理缓存")]
        public static void ClearCache()
        {
            EnvManager.ClearCache();
            EditorUtility.DisplayDialog("提示", "缓存已清理", "确定");
        }

        [MenuItem("Assets/Azathrix/创建环境安装配置", false, 19)]
        public static void CreateEnvJson()
        {
            var path = GetSelectedPath();
            var envPath = Path.Combine(path, "env.json");

            if (File.Exists(envPath))
            {
                EditorUtility.DisplayDialog("提示", "env.json 已存在", "确定");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(GetRelativePath(envPath));
                return;
            }

            var template = EnvJsonService.CreateTemplate();
            EnvJsonService.Write(envPath, template);
            AssetDatabase.Refresh();

            var relativePath = GetRelativePath(envPath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static string GetSelectedPath()
        {
            var path = "Assets";

            foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    path = Path.GetDirectoryName(path);
                break;
            }

            return path;
        }

        private static string GetRelativePath(string absolutePath)
        {
            var dataPath = Application.dataPath;
            if (absolutePath.StartsWith(dataPath))
                return "Assets" + absolutePath.Substring(dataPath.Length);

            var projectRoot = Path.GetFullPath(Path.Combine(dataPath, ".."));
            if (absolutePath.StartsWith(projectRoot))
                return absolutePath.Substring(projectRoot.Length + 1);

            return absolutePath;
        }
    }
}
#endif
