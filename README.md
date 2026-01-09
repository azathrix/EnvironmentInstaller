<p align="center">
  <img src="Documentation~/icon.png" alt="环境安装器 Logo" width="120">
</p>

<h1 align="center">环境安装器</h1>

<p align="center">
  Unity 编辑器环境依赖管理工具，通过 env.json 配置文件定义和管理项目所需的外部依赖
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <a href="https://unity.com/"><img src="https://img.shields.io/badge/Unity-6000.3+-black.svg" alt="Unity"></a>
  <img src="https://img.shields.io/badge/version-0.0.1-green.svg" alt="Version">
</p>

---

## 特性

- 多种下载类型：直接 URL、GitHub Release、GitHub 仓库、NuGet 包
- 多种安装类型：解压安装、直接复制、UPM 包、手动检测
- 可视化编辑器：选中 `env.json` 文件即可在 Inspector 中编辑
- 下载缓存机制：避免重复下载，支持断点续传
- API 支持：可在代码中调用显示安装界面
- 自动扫描：自动扫描 Packages、Assets、PackageCache 目录

## 安装

### 通过 Package Manager

1. 打开 `Window > Package Manager`
2. 点击 `+` 按钮，选择 `Add package from git URL...`
3. 输入包的 Git URL

### 通过 manifest.json

在 `Packages/manifest.json` 的 `dependencies` 中添加：

```json
{
  "dependencies": {
    "com.azathrix.environment-installer": "0.0.1"
  }
}
```

## 依赖

| 包名 | 版本 |
|------|------|
| com.azathrix.unitask | >= 2.5.10 |
| com.unity.nuget.newtonsoft-json | >= 3.2.2 |

## 快速开始

### 创建配置文件

右键菜单：`Assets > Azathrix > 创建环境安装配置`

### 打开环境管理器

菜单：`Azathrix > 环境安装器 > 环境管理器`

### env.json 示例

```json
{
  "dependencies": [
    {
      "id": "LubanTool",
      "displayName": "Luban 配置工具",
      "downloadType": "GitHubRelease",
      "installType": "Extract",
      "url": "https://github.com/focus-creative-games/luban/releases",
      "version": "4.5.0",
      "assetPattern": "*.7z",
      "extractPath": "Luban",
      "targetDir": "Tools/Luban",
      "requiredFiles": ["Tools/Luban/Luban.dll"],
      "optional": false
    }
  ]
}
```

## 配置说明

### 基本字段

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 唯一标识符，用于代码中引用 |
| displayName | string | 显示名称，留空则使用 id |
| downloadType | enum | 下载类型 |
| installType | enum | 安装类型 |
| optional | bool | 是否可选依赖 |
| requiredFiles | string[] | 检测文件路径列表 |

### 下载类型 (downloadType)

| 类型 | 说明 | 相关字段 |
|------|------|----------|
| DirectUrl | 直接 URL 下载 | url |
| GitHubRelease | GitHub Release 下载 | url, version, assetPattern |
| GitHubRepo | GitHub 仓库下载 | url, branch, subPath |
| NuGet | NuGet 包下载 | packageId, version, targetFramework |

### 安装类型 (installType)

| 类型 | 说明 | 相关字段 |
|------|------|----------|
| Extract | 解压安装 (zip/7z) | extractPath, targetDir |
| Copy | 直接复制 | targetDir |
| UnityPackage | 添加到 manifest.json | packageId, url, version |
| Manual | 仅检测，不安装 | requiredFiles |

## API 参考

### EnvDependencyUI

| 方法 | 说明 |
|------|------|
| `ShowInstallWindow(string id)` | 显示单个依赖的安装窗口 |
| `ShowInstallWindow(string[] ids)` | 显示多个依赖的安装窗口 |
| `IsInstalled(string id)` | 检查依赖是否已安装 |
| `AreAllInstalled(string[] ids)` | 检查多个依赖是否全部已安装 |
| `GetMissingDependencies(string[] ids)` | 获取未安装的依赖 ID 列表 |
| `DrawDependencyCheck(string[] ids)` | 绘制依赖检查 UI（嵌入窗口） |
| `DrawSimpleDependencyCheck(string id)` | 绘制简单依赖检查提示（单行） |

### 使用示例

```csharp
using Azathrix.EnvInstaller.Editor.UI;

// 显示安装窗口
EnvDependencyUI.ShowInstallWindow("LubanTool");
EnvDependencyUI.ShowInstallWindow(new[] { "LubanTool", "OtherTool" });

// 检查是否已安装
bool installed = EnvDependencyUI.IsInstalled("LubanTool");

// 在自定义窗口中绘制依赖检查 UI
if (!EnvDependencyUI.DrawDependencyCheck(new[] { "LubanTool" }))
{
    return; // 显示安装界面，阻止后续操作
}

// 简单的单行检查
if (!EnvDependencyUI.DrawSimpleDependencyCheck("LubanTool"))
{
    return;
}
```

## 扫描路径

环境管理器会自动扫描以下位置的 `env.json` 文件：

- `Packages/*/env.json` - UPM 包
- `Assets/**/env.json` - 项目资源
- `Library/PackageCache/*/env.json` - 缓存包

## 缓存

下载的文件缓存在 `Library/EnvCache/` 目录。

清理缓存：`Azathrix > 环境安装器 > 清理缓存`

> ⚠️ 清理缓存后，下次安装需要重新下载文件。

## License

[MIT License](LICENSE) © 2026 Azathrix
