# Notice: copyright, credits and licenses

**Image Display** (overlay for map plans: live window replica, image overlay, hotkeys)
Copyright (c) 2026 SeaMonkee.

## Developers

* **SeaMonkee**: concept, requirements, direction, testing.
* **Claude** (an AI assistant made by Anthropic): design and implementation.

The copyright line names SeaMonkee because an AI system cannot hold copyright; Claude is credited here and in the
source headers as a developer of the code.

## Licensing at a glance

This is free and open-source software under **two licenses that do not conflict**:

| Files | License | Why |
|---|---|---|
| Everything original to this project (see the table below) | **MIT** (`LICENSE`) | Permissive. |
| Files derived from OnTopReplica (listed below) | **MS-RL** (`ImageDisplayApp/LICENSE-OnTopReplica.txt`) | MS-RL is file-level: any file containing OnTopReplica code stays MS-RL. It lets you license other, entirely-your-own files however you like, so MIT for those is fine. |

Both are OSI-approved open-source licenses. (MS-RL is not compatible with the GPL, which is why this project is not
GPL.)

### Files under MS-RL (derived from OnTopReplica)

* `ImageDisplayApp/Native/NativeMethods.cs`
* `ImageDisplayApp/Replica/ThumbnailGeometry.cs`
* `ImageDisplayApp/Replica/ThumbnailReplica.cs`
* `ImageDisplayApp/Replica/WindowList.cs`

Each carries both notices: the upstream copyright (OnTopReplica, (c) Lorenz Cuno Klopfenstein 2010) and the
modifications copyright (c) 2026 SeaMonkee.

### Files under MIT (original)

* `ImageDisplayApp/MainWindow.xaml(.cs)`, `ChromeWindow.xaml(.cs)`, `ImageOverlayWindow.xaml(.cs)`,
  `SettingsWindow.xaml(.cs)`, `CropWindow.xaml(.cs)`
* `ImageDisplayApp/ViewGeometry.cs`, `AppCommands.cs`
* `ImageDisplayApp/Settings/Hotkey.cs`, `Settings/AppSettings.cs`
* `Tests/Program.cs`
* The build files (`*.csproj`, `*.sln`, `app.manifest`) and this documentation.

`App.xaml`, `App.xaml.cs` and `AssemblyInfo.cs` are unmodified Visual Studio project-template boilerplate.

## Code sourced from others

### OnTopReplica (MS-RL)

* Project: https://github.com/LorenzCK/OnTopReplica
* Copyright (c) Lorenz Cuno Klopfenstein 2010. Licensed under the **Microsoft Reciprocal License (MS-RL)**.
* What is taken from it: the technique of showing another window with a live DWM thumbnail and the shape of the
  Win32 interop used for it (window listing, thumbnail registration and properties, source-size handling). The code
  here was written for this project (WPF, .NET 8) rather than copied file-for-file, but because it follows that
  project's technique and structure it is conservatively treated as derived and kept under MS-RL.
* What is **not** used: OnTopReplica's other code, its WindowsFormsAero dependency, its icons, and its menu renderer.
* OnTopReplica's own credits, passed on for completeness: inspired by Switcher (insentient.net) and other
  DWM-based applications; some DWM example code originally from Ookii (Channel 9 / MSDN); WindowsFormsAero.

If you distribute this software, in source or compiled form, the MS-RL requires you to keep the notices above, to
include `LICENSE-OnTopReplica.txt`, and to make the source of the MS-RL files available. Distributing this whole
repository (or the zip) satisfies that.

### Microsoft .NET and Windows APIs

The program runs on the .NET 8 Desktop Runtime (WPF), which is MIT-licensed and **not** bundled here; it calls
documented Windows APIs (user32, dwmapi). There are no NuGet packages.

## Trademarks

ATAK, WinTAK, Miro, Figma, Windows, .NET and other product names are trademarks of their respective owners. They are
mentioned only as examples of programs this tool can display; this project is not affiliated with or endorsed by
any of them.

## No warranty

The software is provided "as is", without warranty of any kind. See `LICENSE` and `LICENSE-OnTopReplica.txt`.

*This notice is a good-faith summary, not legal advice.*
