# Image Display (v4): an overlay for games
###### by ur favorite vibecoder

Put map plans over a game: a **live clone of another window** (Miro, Figma, WinTAK, a browser tab...) and/or a
**transparent PNG**, floating over the game, click-through, with global hotkeys.

**Version 4.0.2.** Free and open-source: MIT for the original code, MS-RL for the parts derived from OnTopReplica. See [Licensing](#licensing).
Developed by **SeaMonkee** and **Claude** (Anthropic).

* Windows 10/11, .NET 8 Desktop Runtime. **No administrator rights needed** (see [Administrator rights](#administrator-rights)).
* The game must be **borderless / windowed fullscreen**: overlays can't draw over exclusive fullscreen.

## Build and run

```
cd ImageDisplayApp
dotnet build -c Release
```

Run `bin\Release\net8.0-windows\ImageDisplayApp.exe`. The licence texts are copied next to the exe.

## Features

### Overlay
* **Window clone**: pick any open window from **Clone ▾**. It is shown live (its client area, aspect ratio kept).
* **Image overlay**: **Open** a PNG; transparent areas really are transparent. With a clone running, the image is laid
  exactly over the clone's picture and scales with it; on its own it fills the overlay.
* **Opacity** (menu): fades the clone and the image.
* **No grey bars**: the overlay keeps the clone's shape when you resize (switch off with *Lock clone's aspect ratio*).
* **Always on top** (on by default) so it shows over the game.

### Two modes, switched by hotkey
| | View mode (playing) | Edit mode (setting up) |
|---|---|---|
| Mouse | **click-through**: the game gets every click | the overlay takes the mouse |
| Shown | only the clone / image | clone / image + toolbar + resize frame |

In **edit mode**: drag anywhere to **move**; drag any **edge or corner** to **resize** (images and clones alike).
Toolbar: **Open · Clear · Clone ▾ · Interact · Top · ⋯ · View · ✕**. Right-click or **⋯** opens the full menu.
Using the overlay never takes keyboard focus from the game.

### Interact
**Interact** brings the *real* cloned window to the front of the game so you can use it natively (keyboard, scroll wheel,
middle-drag, everything), then sends it back behind the game and returns focus to the game.

### Crop the clone  (⋯ menu → *Crop clone...*)
Four sliders (left / top / right / bottom, up to 80 %) trim the edges of the cloned window, e.g. to hide its toolbars or
side panels. It applies live, keeps the clone's aspect ratio, and is remembered for the next clone in that copy.
It trims edges only: it can't blank out a patch in the middle of the picture. The crop window is shown in edit mode only.

### Settings: rebind the hotkeys  (⋯ menu → *Settings...*)
Click a hotkey, press the new keys. Rules: at least one of Ctrl / Alt / Shift / Win (so the key still works normally in your
game), no duplicates, no Alt+F4. Saved to `%AppData%\ImageDisplayApp\settings.json` and applied to every running copy.

Default hotkeys (global, they work while a game has focus):

| Keys | Action |
|---|---|
| **Ctrl+Alt+E** | view mode <-> edit mode (starts in edit mode) |
| **Ctrl+Alt+I** | interact with the cloned window (press again to send it back) |
| **Ctrl+Alt+H** | hide / show everything: the clone, the image, the toolbar and the crop window, in every copy |

### Several copies at once
Just run the exe more than once; each copy is a full overlay with its own clone, image, position and crop (new copies
start a little offset from the last). Two copies can even clone the same window with different crops.

* **The hotkeys control all copies at once.** Only one program can own a global hotkey, so one copy holds them and
  tells the others what they mean. If that copy exits, another picks the hotkeys up within a couple of seconds.
* The toolbar's **Interact** button and the menu items affect only the copy you use them in.
* Other copies' windows don't appear in the clone list.

## Changes

* **4.0.2**: no longer asks for administrator rights. It was only needed by the click forwarding that was removed in v3.
* **4.0.1**: *Hide / show* now hides every window of every copy. Before, it hid only the cloned picture, so copies showing
  just an image (and every copy's toolbar) stayed on screen. Also the build fix `using System.IO;` in
  `Settings/AppSettings.cs` (contributed by SeaMonkee): WPF projects drop `System.IO` from the implicit usings.
* **4.0.0**: rebindable hotkeys (Settings), several copies at once, crop the clone, licensing.

## Check these first (I could not run it on Windows)

1. **View-mode click-through over a clone**: clone something, press the view/edit hotkey, click over it: the click should reach
   the game. If the clone disappears in view mode, tell me.
2. **Two copies**: start a second copy; the hotkey should switch both. Close the first one; within ~2 s the second should
   take over the hotkeys.
3. **Interact**, **Crop** and **Settings** (including an Alt-based combination) behave as described above.

## Administrator rights
Not needed. Windows stops a normal program from controlling one that runs as administrator, so **if the game, or the
window you clone, is itself running as administrator**, the overlay may be unable to clone it, bring it forward, or give
focus back to it. If you see that, close the overlay and start it with right-click > *Run as administrator*. Copies started
differently (one elevated, one not) should still hear each other's hotkey commands (untested).

## Limits
* Mouse/keyboard are not forwarded into the clone any more (use Interact). Cloning needs the source window not minimized.
* Positions, crops and images are not saved between runs (only the hotkeys are).
* Hotkeys that another program already owns can't be registered; the Settings window lets you pick others.

## Tests
`dotnet run --project Tests`: 74 checks of the layout, resize, crop, hotkey and settings-file logic (no windows needed).

## Licensing
Free and open-source software under two non-conflicting licenses:

* **MIT** (`LICENSE`): everything original to this project.
* **MS-RL** (`ImageDisplayApp/LICENSE-OnTopReplica.txt`): the files derived from
  [OnTopReplica](https://github.com/LorenzCK/OnTopReplica) (Copyright (c) Lorenz Cuno Klopfenstein 2010):
  `Native/NativeMethods.cs`, `Replica/ThumbnailGeometry.cs`, `Replica/ThumbnailReplica.cs`, `Replica/WindowList.cs`.
  If you distribute those files (source or compiled), keep the notices and the licence text and make their source
  available; distributing this whole project satisfies that.

`NOTICE.md` has the full copyright notices, credits, per-file licence table and trademark notes. It is a good-faith
summary, not legal advice.
