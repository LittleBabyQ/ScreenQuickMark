# Screen QuickMark

[English](README.md) | [简体中文](README.zh-CN.md)

Screen QuickMark 是一款轻量级 Windows 屏幕标注工具，适合演示、在线会议、教学、产品演示和截图讲解场景。

它可以让你直接在当前屏幕上圈画、添加文字标注、清空或撤销标注，并在需要时快速回到正常桌面操作。

## 功能特点

- 全屏透明标注覆盖层
- 圈画/画笔标注模式
- 文字标注模式
- 标注关闭时支持鼠标穿透，不影响正常桌面操作
- 一键清空所有标注
- 撤销上一步标注
- 显示或隐藏浮动工具栏
- 支持快捷键快速控制
- 本地运行：无需账号、不上传云端、不使用分析追踪

## 快捷键

| 快捷键 | 功能 |
|---|---|
| `Ctrl + Shift + D` | 进入/退出标注模式 |
| `Ctrl + Shift + E` | 切换圈画模式/文字模式 |
| `Ctrl + Shift + C` | 清空所有标注 |
| `Ctrl + Shift + Z` | 撤销上一步标注 |
| `Ctrl + Shift + T` | 显示或隐藏工具栏 |
| `Ctrl + Shift + Q` | 退出应用 |

## 安装方式

### Microsoft Store

Screen QuickMark 正在准备发布到 Microsoft Store。

商店版本上线后，用户可以直接通过 Microsoft Store 安装和更新。

### 手动安装 MSIX

测试版本可以从 GitHub Releases 下载 `.msix` 安装包。

如果安装包使用测试证书签名，你可能需要先安装随包提供的 `.cer` 证书，然后再安装 `.msix`。

> 注意：测试证书只用于本地测试安装。正式 Microsoft Store 版本会由微软重新签名，用户不需要手动信任证书。

## 隐私说明

Screen QuickMark 在本地设备上运行。

- 不收集个人信息。
- 不上传屏幕截图。
- 不上传标注内容。
- 不使用分析或追踪 SDK。
- 所有圈画和文字标注都在本地处理。

正式发布 Microsoft Store 前会提供更完整的隐私政策页面。

## 从源码构建

环境要求：

- Windows 10/11
- .NET 8 SDK
- Windows App SDK
- WinUI 3
- Microsoft Graphics Win2D

构建示例：

```powershell
dotnet build ScreenQuickMark.csproj -p:Platform=x64 -p:Configuration=Release
```

如果需要生成 MSIX 安装包，需要额外提供打包和签名相关的 MSBuild 参数。

## 项目状态

当前版本：`1.0.0.0`

Screen QuickMark 目前处于首个公开版本准备阶段。当前版本重点覆盖稳定的屏幕圈画、文字标注和快捷键控制能力。

## License

License 信息会在正式公开发布前补充。

## 作者

Created by BabyQ.
