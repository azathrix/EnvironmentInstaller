#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;

namespace Azathrix.EnvInstaller.Editor.Core
{
    /// <summary>
    /// env.json 读写服务
    /// </summary>
    public static class EnvJsonService
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        /// <summary>
        /// 读取 env.json 文件
        /// </summary>
        public static EnvConfig Read(string path)
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<EnvConfig>(json, Settings);
        }

        /// <summary>
        /// 写入 env.json 文件
        /// </summary>
        public static void Write(string path, EnvConfig config)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(config, Settings);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 创建默认的 env.json 模板
        /// </summary>
        public static EnvConfig CreateTemplate()
        {
            return new EnvConfig
            {
                Dependencies = new[]
                {
                    new EnvDependency
                    {
                        Id = "example-tool",
                        DisplayName = "示例工具",
                        DownloadType = DownloadType.DirectUrl,
                        InstallType = InstallType.Extract,
                        Url = "https://example.com/tool.zip",
                        TargetDir = "Tools/Example",
                        RequiredFiles = new[] { "Example/example.exe" },
                        Optional = false
                    }
                }
            };
        }
    }
}
#endif
