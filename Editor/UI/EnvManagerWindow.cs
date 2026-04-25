#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Azathrix.EnvInstaller.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.UI
{
    /// <summary>
    /// 环境管理器主窗口
    /// </summary>
    public class EnvManagerWindow : EditorWindow
    {
        private Dictionary<string, List<ScannedDependency>> _groupedDeps;
        private Dictionary<string, bool> _foldouts;
        private Dictionary<string, bool> _installedStatus;
        private EnvManager _manager;
        private Vector2 _scrollPos;
        private string _filter;
        private int _animFrame;
        private double _lastAnimTime;

        [MenuItem("Azathrix/环境安装器/环境管理器")]
        public static void ShowWindow() => GetWindow<EnvManagerWindow>("环境管理器");

        private void OnEnable()
        {
            _manager = new EnvManager();
            _foldouts = new Dictionary<string, bool>();
            _installedStatus = new Dictionary<string, bool>();
            Refresh();
            DownloadService.OnTasksChanged += OnTasksChanged;
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            DownloadService.OnTasksChanged -= OnTasksChanged;
            EditorApplication.update -= OnUpdate;
        }

        private void OnFocus()
        {
            if (_manager == null) return;
            Refresh();
        }

        private void OnTasksChanged()
        {
            RefreshInstalledStatus();
            Repaint();
        }

        private void OnUpdate()
        {
            if (HasActiveDownloads() && EditorApplication.timeSinceStartup - _lastAnimTime > 0.2)
            {
                _animFrame++;
                _lastAnimTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private bool HasActiveDownloads()
        {
            if (_groupedDeps == null) return false;
            foreach (var group in _groupedDeps.Values)
                foreach (var dep in group)
                    if (DownloadService.IsDownloading(dep.Dependency.Id))
                        return true;
            return false;
        }

        private void Refresh()
        {
            _groupedDeps = EnvScanner.ScanGroupedBySource();
            foreach (var key in _groupedDeps.Keys)
                if (!_foldouts.ContainsKey(key))
                    _foldouts[key] = true;
            RefreshInstalledStatus();
        }

        private void RefreshInstalledStatus()
        {
            _installedStatus.Clear();
            if (_groupedDeps == null) return;
            foreach (var group in _groupedDeps.Values)
                foreach (var dep in group)
                    _installedStatus[dep.Dependency.Id] = _manager.IsInstalled(dep.Dependency);
        }

        private void OnGUI()
        {
            // 工具栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                Refresh();
            if (GUILayout.Button("清理缓存", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EnvManager.ClearCache();
                ShowNotification(new GUIContent("缓存已清理"));
            }
            GUILayout.FlexibleSpace();
            _filter = EditorGUILayout.TextField(_filter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(150));
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(40)))
                _filter = null;
            EditorGUILayout.EndHorizontal();

            if (_groupedDeps == null || _groupedDeps.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何 env.json 文件\n\n在 Packages 或 Assets 目录下创建 env.json 文件来定义环境依赖", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var kvp in _groupedDeps)
            {
                if (!string.IsNullOrEmpty(_filter) && !kvp.Key.Contains(_filter, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                DrawGroup(kvp.Key, kvp.Value);
            }
            EditorGUILayout.Space(5);

            EditorGUILayout.EndScrollView();
        }

        private void DrawGroup(string groupName, List<ScannedDependency> deps)
        {
            var foldout = _foldouts.TryGetValue(groupName, out var f) && f;
            var installedCount = deps.Count(d => _installedStatus.TryGetValue(d.Dependency.Id, out var v) && v);
            var totalCount = deps.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, $"{groupName} ({installedCount}/{totalCount})", true);
            _foldouts[groupName] = foldout;

            if (foldout)
            {
                foreach (var dep in deps)
                    DrawDepItem(dep.Dependency);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawDepItem(EnvDependency dep)
        {
            var installed = _installedStatus.TryGetValue(dep.Id, out var v) && v;
            var task = DownloadService.GetTask(dep.Id);
            var hasCache = !installed && DownloadService.HasCache(dep);
            var reqLabel = dep.Optional ? "[可选]" : "[必须]";

            EditorGUILayout.BeginHorizontal();

            // 状态图标
            string icon;
            Color color;
            if (installed)
            {
                icon = "✓";
                color = new Color(0.2f, 0.9f, 0.3f);
            }
            else if (task != null)
            {
                switch (task.State)
                {
                    case TaskState.Downloading:
                        var anim = new[] { "●", "◐", "◑", "◒" };
                        icon = anim[_animFrame % 4];
                        color = new Color(1f, 0.7f, 0.2f);
                        break;
                    case TaskState.Downloaded:
                        icon = "◉";
                        color = new Color(0.4f, 0.7f, 1f);
                        break;
                    case TaskState.Installing:
                        var installAnim = new[] { "◐", "◑", "◒", "◓" };
                        icon = installAnim[_animFrame % 4];
                        color = new Color(0.5f, 0.9f, 0.5f);
                        break;
                    case TaskState.Failed:
                        icon = "✗";
                        color = new Color(1f, 0.3f, 0.3f);
                        break;
                    default:
                        icon = hasCache ? "◉" : "○";
                        color = hasCache ? new Color(0.4f, 0.7f, 1f) : Color.gray;
                        break;
                }
            }
            else
            {
                icon = hasCache ? "◉" : "○";
                color = hasCache ? new Color(0.4f, 0.7f, 1f) : Color.gray;
            }

            var iconStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = color }, fontStyle = FontStyle.Bold };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(20));

            GUILayout.Label($"{dep.GetDisplayName()} {reqLabel}", GUILayout.MinWidth(150));
            GUILayout.FlexibleSpace();

            // 状态文字
            if (task?.State == TaskState.Installing)
            {
                var stateStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
                GUILayout.Label("[安装中]", stateStyle, GUILayout.Width(60));
            }

            // 按钮
            var oldBg = GUI.backgroundColor;
            if (task != null && task.IsRunning)
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
                if (GUILayout.Button("取消", GUILayout.Width(40)))
                    DownloadService.CancelDownload(dep.Id);
            }
            else if (installed)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("卸载", GUILayout.Width(40)))
                {
                    if (EditorUtility.DisplayDialog("确认", $"确定要卸载 {dep.GetDisplayName()} 吗？", "确定", "取消"))
                    {
                        _manager.Uninstall(dep);
                        AssetDatabase.Refresh();
                        RefreshInstalledStatus();
                    }
                }
            }
            else if (task == null || task.State is TaskState.Downloaded or TaskState.Failed or TaskState.Cancelled or TaskState.Completed)
            {
                if (hasCache)
                {
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                    if (GUILayout.Button("安装", GUILayout.Width(40)))
                        DownloadService.StartInstallFromCache(dep);
                    GUI.backgroundColor = oldBg;
                    if (GUILayout.Button("▼", GUILayout.Width(20)))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("重新下载"), false, () =>
                        {
                            DownloadService.DeleteCache(dep);
                            DownloadService.StartDownload(dep);
                        });
                        menu.AddItem(new GUIContent("删除缓存"), false, () =>
                        {
                            DownloadService.DeleteCache(dep);
                            Repaint();
                        });
                        menu.ShowAsContext();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                    var actionLabel = dep.InstallType == InstallType.Manual ? "检查" : "下载";
                    if (GUILayout.Button(actionLabel, GUILayout.Width(40)))
                        DownloadService.StartDownload(dep);
                }
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndHorizontal();

            // 进度条
            if (task?.State == TaskState.Downloading)
            {
                var progRect = EditorGUILayout.GetControlRect(false, 16);
                progRect.x += 20;
                progRect.width -= 20;
                string sizeText;
                if (task.Downloaded > 0)
                {
                    var dlStr = FormatSize(task.Downloaded);
                    var speedStr = task.Speed > 0 ? $" - {FormatSize(task.Speed)}/s" : "";
                    sizeText = task.Total > 0
                        ? $"{dlStr} / {FormatSize(task.Total)}{speedStr}"
                        : $"{dlStr}{speedStr}";
                }
                else
                    sizeText = "正在连接...";
                EditorGUI.ProgressBar(progRect, task.Progress, sizeText);
            }

            // 错误信息
            if (task?.State == TaskState.Failed)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var errorStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red }, fontSize = 10 };
                GUILayout.Label(task.Error, errorStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1048576f:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }
    }
}
#endif
