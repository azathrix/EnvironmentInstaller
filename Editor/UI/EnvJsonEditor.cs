#if UNITY_EDITOR
using System.Collections.Generic;
using Azathrix.EnvInstaller.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.UI
{
    /// <summary>
    /// env.json 可视化编辑器
    /// </summary>
    [CustomEditor(typeof(TextAsset))]
    public class EnvJsonEditor : UnityEditor.Editor
    {
        private EnvConfig _config;
        private string _filePath;
        private bool _isEnvJson;
        private Vector2 _scrollPos;
        private Dictionary<int, bool> _foldouts = new();
        private static readonly string[] DownloadTypeNames = { "DirectUrl", "GitHubRelease", "GitHubRepo", "NuGet" };
        private static readonly string[] InstallTypeNames = { "Extract", "Copy", "UnityPackage", "Manual" };

        private void OnEnable()
        {
            _filePath = AssetDatabase.GetAssetPath(target);
            _isEnvJson = _filePath.EndsWith("env.json");
            if (_isEnvJson)
                LoadConfig();
        }

        private void LoadConfig()
        {
            var fullPath = System.IO.Path.GetFullPath(_filePath);
            _config = EnvJsonService.Read(fullPath);
            _config ??= new EnvConfig { Dependencies = new EnvDependency[0] };
        }

        private void SaveConfig()
        {
            var fullPath = System.IO.Path.GetFullPath(_filePath);
            EnvJsonService.Write(fullPath, _config);
            AssetDatabase.Refresh();
        }

        public override void OnInspectorGUI()
        {
            if (!_isEnvJson)
            {
                base.OnInspectorGUI();
                return;
            }

            DrawEnvEditor();
        }

        private void DrawEnvEditor()
        {
            GUI.enabled = true;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("环境安装配置", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 工具栏
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加依赖", GUILayout.Width(80)))
            {
                var list = new List<EnvDependency>(_config.Dependencies ?? new EnvDependency[0]);
                list.Add(new EnvDependency { Id = "NewDependency", DownloadType = DownloadType.DirectUrl, InstallType = InstallType.Extract });
                _config.Dependencies = list.ToArray();
                _foldouts[list.Count - 1] = true;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", GUILayout.Width(50)))
                LoadConfig();
            if (GUILayout.Button("保存", GUILayout.Width(50)))
                SaveConfig();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (_config.Dependencies == null || _config.Dependencies.Length == 0)
            {
                EditorGUILayout.HelpBox("暂无依赖项，点击\"添加依赖\"创建", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var toRemove = -1;
            for (var i = 0; i < _config.Dependencies.Length; i++)
            {
                if (DrawDependencyItem(i, _config.Dependencies[i]))
                    toRemove = i;
                EditorGUILayout.Space(4);
            }

            if (toRemove >= 0)
            {
                var list = new List<EnvDependency>(_config.Dependencies);
                list.RemoveAt(toRemove);
                _config.Dependencies = list.ToArray();
            }

            EditorGUILayout.EndScrollView();
        }

        private bool DrawDependencyItem(int index, EnvDependency dep)
        {
            var remove = false;
            _foldouts.TryGetValue(index, out var foldout);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);
            foldout = EditorGUILayout.Foldout(foldout, $"{dep.GetDisplayName()} [{dep.DownloadType}]", true);
            _foldouts[index] = foldout;

            GUILayout.Space(-5);
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("删除", GUILayout.Width(50)))
                remove = true;
            GUI.backgroundColor = oldBg;
            
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);
            if (foldout)
            {
                EditorGUILayout.Space(6);
                var oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80;

                // 基本信息
                EditorGUILayout.LabelField("基本信息", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                dep.Id = EditorGUILayout.TextField(new GUIContent("ID", "唯一标识符，用于代码中引用此依赖"), dep.Id);
                dep.DisplayName = EditorGUILayout.TextField(new GUIContent("显示名称", "在UI中显示的友好名称，留空则使用ID"), dep.DisplayName);
                dep.DownloadType = (DownloadType)EditorGUILayout.Popup(new GUIContent("下载类型", "DirectUrl: 直接URL下载\nGitHubRelease: GitHub Release下载\nGitHubRepo: GitHub仓库下载\nNuGet: NuGet包下载"), (int)dep.DownloadType, DownloadTypeNames);
                dep.InstallType = (InstallType)EditorGUILayout.Popup(new GUIContent("安装类型", "Extract: 解压安装(zip/7z)\nCopy: 直接复制\nUnityPackage: 添加到manifest.json\nManual: 仅检测不安装"), (int)dep.InstallType, InstallTypeNames);
                dep.Optional = EditorGUILayout.Toggle(new GUIContent("可选", "勾选表示此依赖为可选项，不会强制要求安装"), dep.Optional);
                dep.DefineSymbol = EditorGUILayout.TextField(new GUIContent("宏定义", "可选。留空时自动使用 ID 生成宏名（非字母数字会转为下划线）"), dep.DefineSymbol);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // 下载配置
                EditorGUILayout.LabelField("下载配置", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                switch (dep.DownloadType)
                {
                    case DownloadType.DirectUrl:
                        dep.Url = EditorGUILayout.TextField(new GUIContent("下载地址", "文件的直接下载URL"), dep.Url);
                        break;
                    case DownloadType.GitHubRelease:
                        dep.Url = EditorGUILayout.TextField(new GUIContent("仓库地址", "GitHub Release页面URL，如: https://github.com/user/repo/releases"), dep.Url);
                        dep.Version = EditorGUILayout.TextField(new GUIContent("版本", "Release版本号，如: 1.0.0"), dep.Version);
                        dep.AssetPattern = EditorGUILayout.TextField(new GUIContent("资源匹配", "匹配Release资源的通配符，如: *.zip 或 *win*.7z"), dep.AssetPattern);
                        break;
                    case DownloadType.GitHubRepo:
                        dep.Url = EditorGUILayout.TextField(new GUIContent("仓库地址", "格式: owner/repo 或 owner/repo@branch"), dep.Url);
                        dep.Branch = EditorGUILayout.TextField(new GUIContent("分支", "要下载的分支名，默认master"), dep.Branch);
                        dep.SubPath = EditorGUILayout.TextField(new GUIContent("子路径", "仓库内的子目录路径，留空下载整个仓库"), dep.SubPath);
                        break;
                    case DownloadType.NuGet:
                        dep.PackageId = EditorGUILayout.TextField(new GUIContent("包ID", "NuGet包的ID，如: Newtonsoft.Json"), dep.PackageId);
                        dep.Version = EditorGUILayout.TextField(new GUIContent("版本", "包版本号，如: 13.0.1"), dep.Version);
                        dep.TargetFramework = EditorGUILayout.TextField(new GUIContent("目标框架", "目标框架，如: netstandard2.0"), dep.TargetFramework);
                        break;
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // 安装配置
                if (dep.InstallType != InstallType.Manual)
                {
                    EditorGUILayout.LabelField("安装配置", EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    switch (dep.InstallType)
                    {
                        case InstallType.Extract:
                            dep.ExtractPath = EditorGUILayout.TextField(new GUIContent("提取路径", "压缩包内要提取的子目录，留空提取全部"), dep.ExtractPath);
                            dep.TargetDir = EditorGUILayout.TextField(new GUIContent("目标目录", "安装目标目录，相对于项目根目录，如: Tools/Luban"), dep.TargetDir);
                            break;
                        case InstallType.Copy:
                            dep.TargetDir = EditorGUILayout.TextField(new GUIContent("目标目录", "复制目标目录，相对于项目根目录"), dep.TargetDir);
                            break;
                        case InstallType.UnityPackage:
                            dep.PackageId = EditorGUILayout.TextField(new GUIContent("包ID", "UPM包ID，如: com.unity.textmeshpro"), dep.PackageId);
                            dep.Url = EditorGUILayout.TextField(new GUIContent("Git地址", "Git仓库URL，用于从Git安装包"), dep.Url);
                            dep.Version = EditorGUILayout.TextField(new GUIContent("版本", "包版本或Git标签"), dep.Version);
                            break;
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(6);
                }

                // 检测文件
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("检测文件", "用于检测依赖是否已安装的文件路径列表"), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(25)))
                {
                    var list = new List<string>(dep.RequiredFiles ?? new string[0]) { "" };
                    dep.RequiredFiles = list.ToArray();
                }
                
                GUILayout.Space(3);
                EditorGUILayout.EndHorizontal();
                var files = dep.RequiredFiles ?? new string[0];
                if (files.Length > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    for (var j = 0; j < files.Length; j++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        files[j] = EditorGUILayout.TextField(files[j]);
                        if (GUILayout.Button("-", GUILayout.Width(25)))
                        {
                            var list = new List<string>(files);
                            list.RemoveAt(j);
                            files = list.ToArray();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    dep.RequiredFiles = files;
                    EditorGUILayout.EndVertical();
                }

                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();
            return remove;
        }
    }
}
#endif
