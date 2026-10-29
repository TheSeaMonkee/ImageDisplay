// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Text.Json.Serialization;

namespace ImageDisplayApp.Settings;

/// <summary>A global hotkey: modifier keys plus one key (a Windows virtual-key code).</summary>
/// <remarks>Named Hotkey (not KeyBinding) to avoid clashing with System.Windows.Input.KeyBinding.</remarks>
public sealed record Hotkey(bool Ctrl, bool Alt, bool Shift, bool Win, int Key)
{
    // The values RegisterHotKey expects.
    private const uint ModAlt = 0x1;
    private const uint ModControl = 0x2;
    private const uint ModShift = 0x4;
    private const uint ModWin = 0x8;

    /// <summary>The RegisterHotKey modifier flags.</summary>
    [JsonIgnore]
    public uint Modifiers =>
        (Alt ? ModAlt : 0u) | (Ctrl ? ModControl : 0u) | (Shift ? ModShift : 0u) | (Win ? ModWin : 0u);

    /// <summary>Human-readable form, e.g. "Ctrl+Alt+E".</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(TryKeyName(Key, out string name) ? name : "0x" + Key.ToString("X2"));
            return string.Join("+", parts);
        }
    }

    public static bool IsSupportedKey(int virtualKey) => TryKeyName(virtualKey, out _);

    /// <summary>Checks that the combination is safe and sensible as a global hotkey.</summary>
    public bool TryValidate(out string error)
    {
        if (Modifiers == 0)
        {
            error = "Include at least one of Ctrl, Alt, Shift or Win, so the key still works normally in your game.";
            return false;
        }

        if (!IsSupportedKey(Key))
        {
            error = "That key can't be used in a hotkey.";
            return false;
        }

        if (Alt && !Ctrl && !Shift && !Win && Key == 0x73) // Alt+F4
        {
            error = "Alt+F4 closes windows, so it can't be used here.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryKeyName(int vk, out string name)
    {
        string? result = vk switch
        {
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),   // A-Z
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),   // 0-9
            >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),        // F1-F24
            >= 0x60 and <= 0x69 => "Num" + (vk - 0x60),      // numpad 0-9
            0x6A => "Num*",
            0x6B => "Num+",
            0x6D => "Num-",
            0x6E => "Num.",
            0x6F => "Num/",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => null,
        };

        name = result ?? string.Empty;
        return result != null;
    }
}
