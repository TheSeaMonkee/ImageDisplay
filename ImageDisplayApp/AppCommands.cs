// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

namespace ImageDisplayApp;

/// <summary>
/// Commands sent between running copies of the app. Only one copy can own a global hotkey, so that copy
/// broadcasts what the hotkey means (as a registered window message) and every copy, itself included, acts on it.
/// </summary>
internal enum AppCommand
{
    /// <summary>lParam: 1 = edit mode, 0 = view mode.</summary>
    SetEditMode = 1,

    /// <summary>lParam: 1 = hide everything, 0 = show it again.</summary>
    SetHidden = 2,

    /// <summary>lParam: 0 = stop interacting, otherwise start (the value is the game's window handle, or 1 if unknown).</summary>
    SetInteract = 3,

    /// <summary>Reload the settings file and re-register hotkeys (also ends a suspension).</summary>
    ReloadSettings = 4,

    /// <summary>Release the hotkeys while the settings window is capturing a new combination.</summary>
    SuspendHotkeys = 5,
}

internal static class AppCommands
{
    public const string MessageName = "ImageDisplayApp.Command.v1";
}
