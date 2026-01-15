#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// 下载类型
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DownloadType
    {
        DirectUrl,
        GitHubRelease,
        GitHubRepo,
        NuGet
    }

    /// <summary>
    /// 安装类型
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum InstallType
    {
        Extract,
        Copy,
        UnityPackage,
        Manual
    }

    /// <summary>
    /// env.json 根对象
    /// </summary>
    [Serializable]
    public class EnvConfig
    {
        [JsonProperty("dependencies")]
        public EnvDependency[] Dependencies { get; set; } = Array.Empty<EnvDependency>();
    }

    /// <summary>
    /// 环境依赖定义
    /// </summary>
    [Serializable]
    public class EnvDependency
    {
        // 基本信息
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("downloadType")]
        public DownloadType DownloadType { get; set; }

        [JsonProperty("installType")]
        public InstallType InstallType { get; set; }

        // 下载参数
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("assetPattern")]
        public string AssetPattern { get; set; }

        [JsonProperty("branch")]
        public string Branch { get; set; }

        [JsonProperty("subPath")]
        public string SubPath { get; set; }

        [JsonProperty("packageId")]
        public string PackageId { get; set; }

        [JsonProperty("targetFramework")]
        public string TargetFramework { get; set; }

        // 安装参数
        [JsonProperty("extractPath")]
        public string ExtractPath { get; set; }

        [JsonProperty("targetDir")]
        public string TargetDir { get; set; }

        // 检测和选项
        [JsonProperty("requiredFiles")]
        public string[] RequiredFiles { get; set; }

        [JsonProperty("optional")]
        public bool Optional { get; set; }

        [JsonProperty("defineSymbol")]
        public string DefineSymbol { get; set; }

        /// <summary>
        /// 获取显示名称，如果未设置则返回 ID
        /// </summary>
        public string GetDisplayName() => string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;
    }

    /// <summary>
    /// 扫描到的依赖信息（包含来源路径）
    /// </summary>
    public class ScannedDependency
    {
        public EnvDependency Dependency { get; set; }
        public string SourcePath { get; set; }  // env.json 文件路径
        public string SourceName { get; set; }  // 来源名称（包名或目录名）
    }
}
#endif
