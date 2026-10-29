// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageDisplayApp.Settings;

namespace ImageDisplayApp
{
    /// <summary>Lets the user rebind the global hotkeys. Shown modally; the caller handles applying the result.</summary>
    public partial class SettingsWindow : Window
    {
        private static readonly string[] ActionNames = { "View / edit mode", "Interact", "Hide / show" };

        private readonly Hotkey[] _hotkeys;
        private int _capturing = -1;

        /// <summary>The settings to save when the user presses Save; null if they cancelled.</summary>
        public AppSettings? Result { get; private set; }

        public SettingsWindow(Window owner, AppSettings current)
        {
            InitializeComponent();
            Owner = owner;

            _hotkeys = new[] { current.Hotkeys.EditMode, current.Hotkeys.Interact, current.Hotkeys.HideAll };
            Refresh();
        }

        private Button ButtonFor(int index) => index switch
        {
            0 => btnEdit,
            1 => btnInteract,
            _ => btnHide,
        };

        private void Refresh()
        {
            for (int i = 0; i < _hotkeys.Length; i++)
            {
                ButtonFor(i).Content = i == _capturing ? "Press the new keys..." : _hotkeys[i].DisplayName;
            }
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tag } && int.TryParse(tag, out int index))
            {
                _capturing = index;
                messageText.Text = string.Empty;
                Refresh();
            }
        }

        private static bool IsModifierKey(Key key) =>
            key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_capturing < 0)
            {
                return;
            }

            e.Handled = true;

            // With Alt held, WPF reports Key.System and puts the real key in SystemKey.
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                _capturing = -1;
                Refresh();
                return;
            }

            if (IsModifierKey(key))
            {
                return; // wait for the actual key
            }

            ModifierKeys modifiers = Keyboard.Modifiers;
            var candidate = new Hotkey(
                modifiers.HasFlag(ModifierKeys.Control),
                modifiers.HasFlag(ModifierKeys.Alt),
                modifiers.HasFlag(ModifierKeys.Shift),
                modifiers.HasFlag(ModifierKeys.Windows),
                KeyInterop.VirtualKeyFromKey(key));

            if (!candidate.TryValidate(out string error))
            {
                messageText.Text = error;
                return; // keep capturing so they can try again
            }

            for (int i = 0; i < _hotkeys.Length; i++)
            {
                if (i != _capturing && _hotkeys[i] == candidate)
                {
                    messageText.Text = candidate.DisplayName + " is already used for \"" + ActionNames[i] + "\".";
                    return;
                }
            }

            _hotkeys[_capturing] = candidate;
            _capturing = -1;
            messageText.Text = string.Empty;
            Refresh();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new HotkeySettings();
            _hotkeys[0] = defaults.EditMode;
            _hotkeys[1] = defaults.Interact;
            _hotkeys[2] = defaults.HideAll;
            _capturing = -1;
            messageText.Text = string.Empty;
            Refresh();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
            {
                Hotkeys = new HotkeySettings
                {
                    EditMode = _hotkeys[0],
                    Interact = _hotkeys[1],
                    HideAll = _hotkeys[2],
                },
            };

            if (!settings.Hotkeys.TryValidate(out string error))
            {
                messageText.Text = error;
                return;
            }

            if (!SettingsStore.Save(settings))
            {
                messageText.Text = "Could not write the settings file (" + SettingsStore.DefaultPath + ").";
                return;
            }

            Result = settings;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
        }
    }
}
