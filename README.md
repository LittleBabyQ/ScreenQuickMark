# Screen QuickMark

[English](README.md) | [简体中文](README.zh-CN.md)

Screen QuickMark is a lightweight Windows screen annotation tool for presentations, online meetings, teaching, demos, and quick visual explanations.

It lets you draw directly on top of your current screen, add text notes, clear or undo annotations, and quickly return to normal desktop interaction.

## Features

- Full-screen transparent annotation overlay
- Pen drawing mode
- Text annotation mode
- Click-through mode when annotation is disabled
- Clear all annotations
- Undo last annotation
- Show or hide the floating toolbar
- Keyboard shortcuts for fast control
- Local-only operation: no account, no cloud upload, no analytics

## Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + D` | Toggle annotation mode |
| `Ctrl + Shift + E` | Switch between pen mode and text mode |
| `Ctrl + Shift + C` | Clear all annotations |
| `Ctrl + Shift + Z` | Undo last annotation |
| `Ctrl + Shift + T` | Show or hide toolbar |
| `Ctrl + Shift + Q` | Exit application |

## Installation

### Microsoft Store

Screen QuickMark is being prepared for Microsoft Store release.

After the Store version is available, installation will be handled directly by Microsoft Store.

### Manual MSIX Installation

For test builds, download the `.msix` package from GitHub Releases.

If the package is signed with a test certificate, you may need to install the provided `.cer` certificate before installing the `.msix` package.

> Note: The test certificate is only required for manual local testing. Microsoft Store releases are signed by Microsoft and do not require users to trust a separate certificate.

## Privacy

Screen QuickMark works locally on your device.

- It does not collect personal information.
- It does not upload screenshots.
- It does not upload annotation content.
- It does not use analytics or tracking SDKs.
- All drawing and text annotations are processed locally.

A detailed privacy policy will be provided before Microsoft Store release.

## Build From Source

Requirements:

- Windows 10/11
- .NET 8 SDK
- Windows App SDK
- WinUI 3
- Microsoft Graphics Win2D

Build command example:

```powershell
dotnet build ScreenQuickMark.csproj -p:Platform=x64 -p:Configuration=Release
```

To generate an MSIX package, use MSBuild properties for package generation and signing.

## Project Status

Current version: `1.0.0.0`

Screen QuickMark is currently in early release preparation. The first public release focuses on stable screen annotation, text annotation, and keyboard-driven control.

## License

License information will be added before public release.

## Author

Created by BabyQ.
