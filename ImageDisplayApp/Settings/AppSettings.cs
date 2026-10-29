// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.IO;
using System.Text.Json;

namespace ImageDisplayApp.Settings;

/// <summary>The rebindable global hotkeys. They apply to every running copy of the app at once.</summary>
public sealed class HotkeySettings
{
    public Hotkey EditMode { get; set; } = new Hotkey(true, true, false, false, 0x45);  // Ctrl+Alt+E
    public Hotkey Interact { get; set; } = new Hotkey(true, true, false, false, 0x49);  // Ctrl+Alt+I
    public Hotkey HideAll { get; set; } = new Hotkey(true, true, false, false, 0x48);   // Ctrl+Alt+H

    /// <summary>True if every hotkey is valid and no two are the same.</summary>
    public bool TryValidate(out string error)
    {
        var named = new (string Name, Hotkey? Key)[]
        {
            ("View / edit mode", EditMode),
            ("Interact", Interact),
            ("Hide / show", HideAll),
        };

        foreach (var (name, key) in named)
        {
            if (key == null)
            {
                error = name + " has no hotkey.";
                return false;
            }

            if (!key.TryValidate(out string reason))
            {
                error = name + ": " + reason;
                return false;
            }
        }

        for (int i = 0; i < named.Length; i++)
        {
            for (int j = i + 1; j < named.Length; j++)
            {
                if (named[i].Key == named[j].Key)
                {
                    error = $"{named[i].Name} and {named[j].Name} use the same keys.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }
}

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = new HotkeySettings();
}

/// <summary>Reads and writes the settings file shared by all running copies of the app.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageDisplayApp", "settings.json");

    /// <summary>Loads the settings. Never throws: a missing, unreadable or invalid file gives the defaults.</summary>
    public static AppSettings Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            if (loaded?.Hotkeys == null || !loaded.Hotkeys.TryValidate(out _))
            {
                return new AppSettings();
            }

            return loaded;
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    /// <summary>Saves the settings. The file is replaced atomically, so another copy never reads half a file.</summary>
    public static bool Save(AppSettings settings, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string temp = path + "." + Environment.ProcessId + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
