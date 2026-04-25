#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Azathrix.EnvInstaller.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.UI
{
    /// <summary>
    /// 环境安装弹窗
    /// </summary>
    public class EnvInstallWindow : EditorWindow
    {
        private EnvDependency[] _deps;
        private Action _onComplete;
        private EnvManager _manager;

        private string _status = "";
        private int _animFrame;
        private double _lastAnimTime;
        private Dictionary<string, bool> _installedStatus;

        /// <summary>
        /// 显示安装窗口
        /// </summary>
        public static void Show(EnvDependency[] deps, Action onComplete = null)
        {
            var window = GetWindow<EnvInstallWindow>(true, "依赖管理", true);
            window._deps = deps;
            window._onComplete = onComplete;
            window.minSize = new Vector2(400, 300);
            window.RefreshStatus();
            window.ShowUtility();
        }

        /// <summary>
        /// 通过依赖 ID 列表显示安装窗口
        /// </summary>
        public static void ShowByIds(string[] depIds, Action onComplete = null)
        {
            var deps = EnvScanner.FindByIds(depIds);
            if (deps == null || deps.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到指定的环境依赖", "确定");
                return;
            }

            Show(deps.Select(d => d.Dependency).ToArray(), onComplete);
        }

        private void OnEnable()
        {
            _manager = new EnvManager();
            _installedStatus = new Dictionary<string, bool>();
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
            RefreshStatus();
        }

        private void OnTasksChanged()
        {
            RefreshStatus();
            Repaint();
            _onComplete?.Invoke();
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
            if (_deps == null) return false;
            foreach (var dep in _deps)
                if (DownloadService.IsDownloading(dep.Id))
                    return true;
            return false;
        }

        private void RefreshStatus()
        {
            if (_deps == null) return;
            _installedStatus.Clear();
            foreach (var dep in _deps)
                _installedStatus[dep.Id] = _manager.IsInstalled(dep);
        }

        private void OnGUI()
        {
            // 工具栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("清理缓存", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EnvManager.ClearCache();
                _status = "缓存已清理";
            }
            EditorGUILayout.EndHorizontal();

            if (_deps == null || _deps.Length == 0)
            {
                EditorGUILayout.HelpBox("没有依赖项", MessageType.Info);
                return;
            }

            // Box 框
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var required = _deps.Where(d => !d.Optional).ToArray();
            var optional = _deps.Where(d => d.Optional).ToArray();

            var rowIndex = 0;
            foreach (var dep in required)
                DrawDepItem(dep, rowIndex++);

            if (required.Length > 0 && optional.Length > 0)
            {
                EditorGUILayout.Space(2);
                var lineRect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                EditorGUILayout.Space(2);
            }

            foreach (var dep in optional)
                DrawDepItem(dep, rowIndex++);

            EditorGUILayout.EndVertical();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private void DrawDepItem(EnvDependency dep, int rowIndex = 0)
        {
            var installed = _installedStatus.TryGetValue(dep.Id, out var v) && v;
            var task = DownloadService.GetTask(dep.Id);
            var hasCache = !installed && DownloadService.HasCache(dep);
            var reqLabel = dep.Optional ? "[可选]" : "[必须]";

            // 斑马纹背景
            var rowRect = EditorGUILayout.BeginHorizontal();
            if (rowIndex % 2 == 1)
            {
                var bgColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.85f, 0.85f, 0.85f);
                EditorGUI.DrawRect(rowRect, bgColor);
            }

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
                if (GUILayout.Button("取消", GUILayout.Width(50)))
                    DownloadService.CancelDownload(dep.Id);
            }
            else if (installed)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("卸载", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("确认", $"确定要卸载 {dep.GetDisplayName()} 吗？", "确定", "取消"))
                    {
                        _manager.Uninstall(dep);
                        AssetDatabase.Refresh();
                        RefreshStatus();
                        _onComplete?.Invoke();
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
                    if (GUILayout.Button(actionLabel, GUILayout.Width(50)))
                        DownloadService.StartDownload(dep);
                }
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndHorizontal();

            // 进度条在条目下方（仅下载中显示）
            if (task?.State == TaskState.Downloading)
            {
                var progRect = EditorGUILayout.GetControlRect(false, 16);
                progRect.x += 20;
                progRect.width -= 20;
                string sizeText;
                if (task.Downloaded > 0)
                {
                    var dlMB = task.Downloaded / 1048576f;
                    sizeText = task.Total > 0
                        ? $"{dlMB:F2} MB / {task.Total / 1048576f:F2} MB ({task.Progress * 100:F0}%)"
                        : $"{dlMB:F2} MB (下载中...)";
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
                EditorGUILayout.HelpBox(task.Error, MessageType.Error);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
