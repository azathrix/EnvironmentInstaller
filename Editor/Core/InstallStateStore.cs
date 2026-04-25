#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.Core
{
    [Serializable]
    public class InstallRecord
    {
        public string DependencyId;
        public string TargetPath;
        public bool RootCreated;
        public List<string> InstalledFiles = new();
    }

    [Serializable]
    internal class InstallStateData
    {
        public List<InstallRecord> Records = new();
    }

    /// <summary>
    /// 安装状态存储（用于安全卸载）
    /// </summary>
    public static class InstallStateStore
    {
        private static readonly string StateFilePath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/EnvInstaller/install-state.json"));

        private static InstallStateData _cache;

        public static InstallRecord GetRecord(string depId)
        {
            if (string.IsNullOrEmpty(depId)) return null;
            var data = Load();
            return data.Records.FirstOrDefault(r => string.Equals(r.DependencyId, depId, StringComparison.OrdinalIgnoreCase));
        }

        public static void UpsertRecord(InstallRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.DependencyId)) return;

            var data = Load();
            data.Records.RemoveAll(r => string.Equals(r.DependencyId, record.DependencyId, StringComparison.OrdinalIgnoreCase));
            data.Records.Add(record);
            Save(data);
        }

        public static void RemoveRecord(string depId)
        {
            if (string.IsNullOrEmpty(depId)) return;

            var data = Load();
            var removed = data.Records.RemoveAll(r => string.Equals(r.DependencyId, depId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) Save(data);
        }

        private static InstallStateData Load()
        {
            if (_cache != null) return _cache;

            try
            {
                if (!File.Exists(StateFilePath))
                {
                    _cache = new InstallStateData();
                    return _cache;
                }

                var json = File.ReadAllText(StateFilePath);
                _cache = JsonConvert.DeserializeObject<InstallStateData>(json) ?? new InstallStateData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InstallStateStore] 读取安装状态失败: {e.Message}");
                _cache = new InstallStateData();
            }

            return _cache;
        }

        private static void Save(InstallStateData data)
        {
            _cache = data ?? new InstallStateData();

            try
            {
                var dir = Path.GetDirectoryName(StateFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InstallStateStore] 写入安装状态失败: {e.Message}");
            }
        }
    }
}
#endif
