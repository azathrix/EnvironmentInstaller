#if UNITY_EDITOR
using System.IO;
using System.Threading;
using Azathrix.EnvInstaller.Editor.Core;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Azathrix.EnvInstaller.Editor.Installers
{
    /// <summary>
    /// Unity Package Manager 安装器
    /// 通过修改 Packages/manifest.json 添加依赖
    /// </summary>
    public class UnityPackageInstaller
    {
        public async UniTask<bool> InstallAsync(EnvDependency dep, CancellationToken ct = default)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[UnityPackageInstaller] manifest.json not found");
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            if (dependencies == null)
            {
                Debug.LogError("[UnityPackageInstaller] dependencies not found in manifest.json");
                return false;
            }

            var packageId = dep.PackageId ?? dep.Id;
            var packageVersion = GetPackageValue(dep);

            if (dependencies[packageId] != null)
            {
                Debug.Log($"[UnityPackageInstaller] Package {packageId} already exists, updating to {packageVersion}");
            }

            dependencies[packageId] = packageVersion;

            var newJson = manifest.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(manifestPath, newJson);

            Debug.Log($"[UnityPackageInstaller] Added {packageId}: {packageVersion}");

            await UniTask.Yield();
            return true;
        }

        public bool Uninstall(EnvDependency dep)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            var packageId = dep.PackageId ?? dep.Id;
            if (dependencies == null || dependencies[packageId] == null)
                return true;

            dependencies.Remove(packageId);

            var newJson = manifest.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(manifestPath, newJson);

            Debug.Log($"[UnityPackageInstaller] Removed {packageId}");
            return true;
        }

        public bool IsInstalled(EnvDependency dep)
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            var json = File.ReadAllText(manifestPath);
            var manifest = JObject.Parse(json);
            var dependencies = manifest["dependencies"] as JObject;

            if (dependencies == null)
                return false;

            var packageId = dep.PackageId ?? dep.Id;
            var existing = dependencies[packageId];
            if (existing == null)
                return false;

            if (!string.IsNullOrEmpty(dep.Version))
            {
                var installedVersion = existing.Value<string>();
                return installedVersion == GetPackageValue(dep);
            }

            return true;
        }

        private string GetPackageValue(EnvDependency dep)
        {
            if (!string.IsNullOrEmpty(dep.Url))
            {
                if (!string.IsNullOrEmpty(dep.Version) && !dep.Url.Contains("#"))
                    return $"{dep.Url}#{dep.Version}";
                return dep.Url;
            }
            return dep.Version ?? "latest";
        }
    }
}
#endif
