// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageDisplayApp.Native;
using ImageDisplayApp.Replica;
using ImageDisplayApp.Settings;
using Microsoft.Win32;

namespace ImageDisplayApp
{
    /// <summary>
    /// The heart of the app. It owns the "view rectangle" (where the overlay sits on screen) and keeps three stacked
    /// windows in step with it, bottom to top:
    ///   1. this window, which only hosts the live replica of another window (parked invisible when there is none),
    ///   2. <see cref="ImageOverlayWindow"/>, the optional image, with real per-pixel transparency,
    ///   3. <see cref="ChromeWindow"/>, the toolbar and resize handles (edit mode only).
    ///
    /// View mode (default while playing): everything is click-through and nothing but the picture is visible.
    /// Edit mode: toolbar, move and resize.
    ///
    /// Several copies of the app can run at once. Only one process can own a global hotkey, so whichever copy holds
    /// the hotkeys broadcasts what they mean and every copy (itself included) acts on it. If that copy exits,
    /// another one picks the hotkeys up (each copy keeps retrying while it doesn't hold them).
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MinWidthPx = 120;
        public const int MinHeightPx = 80;

        private const int ParkX = -32000;
        private const int ParkY = -32000;

        private const int HotkeyEdit = 1;
        private const int HotkeyInteract = 2;
        private const int HotkeyHide = 3;
        private const int HotkeyCount = 3;

        private static readonly string[] HotkeyNames = { "view / edit mode", "interact", "hide / show" };

        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _hotkeyTimer;
        private readonly bool[] _hotkeyActive = new bool[HotkeyCount];

        private AppSettings _settings;
        private uint _commandMessage;
        private bool _hotkeysSuspended;
        private bool _groupInteract;

        private IntPtr _hwnd;
        private ImageOverlayWindow? _overlay;
        private ChromeWindow? _chrome;
        private CropWindow? _cropWindow;
        private ThumbnailReplica? _replica;
        private BitmapImage? _image;

        private PxRect _view;
        private CropFraction _crop;
        private bool _editMode = true;
        private bool _interacting;
        private bool _hidden;
        private bool _inLayout;
        private bool _closing;
        private bool _lockAspect = true;
        private bool _alwaysOnTop = true;
        private byte _opacity = 255;
        private IntPtr _returnTo;
        private string? _status;

        public MainWindow()
        {
            InitializeComponent();

            _settings = SettingsStore.Load();

            // A new copy starts a little down and to the right of the previous ones, so they don't sit exactly on top of each other.
            int stagger = 40 * (CountOtherInstances() % 8);
            _view = new PxRect(120 + stagger, 120 + stagger, 800, 450);
            _status = IdleHelp;

            // Notices the cloned window resizing or closing, and keeps the replica fresh.
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, _) => ApplyLayout();

            // While another copy (or program) holds a hotkey, keep trying: if that copy exits, this one takes over.
            _hotkeyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _hotkeyTimer.Tick += (_, _) =>
            {
                if (!_hotkeysSuspended && Array.IndexOf(_hotkeyActive, false) >= 0)
                {
                    TryRegisterHotkeys();
                }
            };
            _hotkeyTimer.Start();
        }

        // ------------------------------------------------------------------------------------------ state for chrome

        internal PxRect View => _view;

        internal CropFraction Crop => _crop;

        public bool IsCloning => _replica != null;

        public bool HasImage => _image != null;

        /// <summary>Width / height to hold while resizing, or 0 for free resizing.</summary>
        public double AspectForResize =>
            _lockAspect && _replica != null && !_interacting ? _replica.SourceAspect : 0;

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set
            {
                _alwaysOnTop = value;
                Topmost = value;
                if (_overlay != null)
                {
                    _overlay.Topmost = value;
                }

                if (_chrome != null)
                {
                    _chrome.Topmost = value;
                }

                if (_cropWindow != null)
                {
                    _cropWindow.Topmost = value;
                }

                _chrome?.RefreshState();
            }
        }

        /// <summary>The window to parent message boxes and file dialogs to (this window itself is usually parked off-screen).</summary>
        private Window DialogOwner => _chrome != null && _chrome.IsVisible ? _chrome : this;

        private string IdleHelp =>
            "Open an image or clone a window\n(toolbar, or right-click)\n\n"
            + _settings.Hotkeys.EditMode.DisplayName + "   view / edit mode\n"
            + _settings.Hotkeys.Interact.DisplayName + "   interact with the cloned window\n"
            + _settings.Hotkeys.HideAll.DisplayName + "   hide / show everything";

        /// <summary>How many other copies of this app are running.</summary>
        private static int CountOtherInstances()
        {
            try
            {
                using Process self = Process.GetCurrentProcess();
                Process[] all = Process.GetProcessesByName(self.ProcessName);
                int others = all.Count(p => p.Id != self.Id);
                foreach (Process p in all)
                {
                    p.Dispose();
                }

                return others;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------------------------------------ lifecycle

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;

            // Never take focus from the game, whatever is clicked.
            NativeMethods.SetExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE, true);

            _commandMessage = NativeMethods.RegisterWindowMessage(AppCommands.MessageName);

            // Windows normally drops messages sent from a less privileged program to a more privileged one. Allow ours, so
            // a copy started "as administrator" and one started normally still hear each other's hotkey commands.
            NativeMethods.ChangeWindowMessageFilterEx(_hwnd, _commandMessage, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);

            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
            TryRegisterHotkeys();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Stacking order is guaranteed by ownership: this window < overlay image < toolbar.
            _overlay = new ImageOverlayWindow(this);
            _overlay.Show();
            _overlay.SetImage(_image);

            _chrome = new ChromeWindow(this, _overlay);
            _chrome.Show();
            _chrome.SetStatus(_status);

            AlwaysOnTop = _alwaysOnTop; // applies Topmost to all the windows
            ApplyClickThrough();
            ApplyLayout();
            UpdateChromeVisibility();

            WarnIfHotkeysInactive();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ApplyLayout(); // Windows may have rescaled the window; put it back to the view rectangle
        }

        protected override void OnClosed(EventArgs e)
        {
            // The owned windows (image, toolbar) are already closed by now, so touch nothing but our own resources.
            _closing = true;
            _timer.Stop();
            _hotkeyTimer.Stop();
            UnregisterHotkeys();
            _replica?.Dispose();
            _replica = null;
            base.OnClosed(e);
        }

        // ---------------------------------------------------------------------------------------------------- hotkeys

        private Hotkey[] CurrentHotkeys() => new[]
        {
            _settings.Hotkeys.EditMode,
            _settings.Hotkeys.Interact,
            _settings.Hotkeys.HideAll,
        };

        /// <summary>Registers every hotkey this copy doesn't hold yet. Fails quietly for ones another copy or program holds.</summary>
        private void TryRegisterHotkeys()
        {
            if (_closing || _hwnd == IntPtr.Zero)
            {
                return;
            }

            Hotkey[] keys = CurrentHotkeys();
            for (int i = 0; i < HotkeyCount; i++)
            {
                if (!_hotkeyActive[i])
                {
                    _hotkeyActive[i] = NativeMethods.RegisterHotKey(
                        _hwnd, i + 1, keys[i].Modifiers | NativeMethods.MOD_NOREPEAT, (uint)keys[i].Key);
                }
            }
        }

        private void UnregisterHotkeys()
        {
            for (int i = 0; i < HotkeyCount; i++)
            {
                if (_hotkeyActive[i])
                {
                    NativeMethods.UnregisterHotKey(_hwnd, i + 1);
                    _hotkeyActive[i] = false;
                }
            }
        }

        /// <summary>
        /// Tells the user about hotkeys nobody holds. Stays quiet when another copy of the app is running, because
        /// that copy probably holds them (and will hand them over if it exits).
        /// </summary>
        private void WarnIfHotkeysInactive()
        {
            if (_hotkeysSuspended || _closing || CountOtherInstances() > 0)
            {
                return;
            }

            Hotkey[] keys = CurrentHotkeys();
            var failed = new List<string>();
            for (int i = 0; i < HotkeyCount; i++)
            {
                if (!_hotkeyActive[i])
                {
                    failed.Add(keys[i].DisplayName + "  (" + HotkeyNames[i] + ")");
                }
            }

            if (failed.Count > 0)
            {
                MessageBox.Show(DialogOwner,
                    "These hotkeys could not be registered, probably because another program uses them. " +
                    "You can change them under  ...  > Settings.\n\n" + string.Join("\n", failed),
                    "Image Display", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                handled = true;
                OnHotkey(wParam.ToInt32());
            }
            else if (_commandMessage != 0 && (uint)msg == _commandMessage)
            {
                handled = true;
                HandleCommand((AppCommand)wParam.ToInt32(), lParam);
            }
            else if (msg == NativeMethods.WM_NCHITTEST && !_editMode)
            {
                // View mode: this window ignores the mouse (second line of defence next to the layered style).
                handled = true;
                return new IntPtr(NativeMethods.HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }

        /// <summary>A hotkey this copy holds was pressed: decide what it means and tell every copy.</summary>
        private void OnHotkey(int id)
        {
            switch (id)
            {
                case HotkeyEdit:
                    Broadcast(AppCommand.SetEditMode, _editMode ? 0 : 1);
                    break;

                case HotkeyHide:
                    Broadcast(AppCommand.SetHidden, _hidden ? 0 : 1);
                    break;

                case HotkeyInteract:
                    _groupInteract = !_groupInteract;
                    int game = 0;
                    if (_groupInteract)
                    {
                        // The game is whatever has focus right now; every copy returns focus there when interacting ends.
                        IntPtr foreground = NativeMethods.GetForegroundWindow();
                        game = foreground != IntPtr.Zero && !BelongsToThisProcess(foreground)
                            ? (int)foreground.ToInt64()
                            : 1;
                    }

                    Broadcast(AppCommand.SetInteract, game);
                    break;
            }
        }

        /// <summary>Sends a command to every copy of the app, this one included. Falls back to acting locally if that fails.</summary>
        private void Broadcast(AppCommand command, int value)
        {
            bool posted = _commandMessage != 0
                && NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, _commandMessage,
                    new IntPtr((int)command), new IntPtr(value));

            if (!posted)
            {
                HandleCommand(command, new IntPtr(value));
            }
        }

        private void HandleCommand(AppCommand command, IntPtr value)
        {
            if (_closing)
            {
                return;
            }

            switch (command)
            {
                case AppCommand.SetEditMode:
                    SetEditMode(value != IntPtr.Zero);
                    break;

                case AppCommand.SetHidden:
                    SetHidden(value != IntPtr.Zero);
                    break;

                case AppCommand.SetInteract:
                    if (value == IntPtr.Zero)
                    {
                        if (_interacting)
                        {
                            EndInteract();
                        }
                    }
                    else
                    {
                        StartInteract(value);
                    }

                    break;

                case AppCommand.SuspendHotkeys:
                    _hotkeysSuspended = value != IntPtr.Zero;
                    if (_hotkeysSuspended)
                    {
                        UnregisterHotkeys();
                    }

                    break;

                case AppCommand.ReloadSettings:
                    ReloadSettings();
                    break;
            }
        }

        private void ReloadSettings()
        {
            _settings = SettingsStore.Load();
            _hotkeysSuspended = false;
            UnregisterHotkeys();
            TryRegisterHotkeys();

            if (_replica == null && _image == null)
            {
                ShowStatus(IdleHelp); // the hint lists the hotkeys, which may have changed
            }
        }

        /// <summary>Opens the settings window. The new keys are applied in every running copy.</summary>
        public void ShowSettings()
        {
            // Release the hotkeys while the window is open, so the combination being captured isn't swallowed by itself.
            Broadcast(AppCommand.SuspendHotkeys, 1);

            var dialog = new SettingsWindow(DialogOwner, _settings) { Topmost = _alwaysOnTop };
            dialog.ShowDialog();

            Broadcast(AppCommand.ReloadSettings, 0); // also ends the suspension, saved or not

            // Give the copies a moment to re-register, then say so if nothing could take a hotkey.
            var check = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                WarnIfHotkeysInactive();
            };
            check.Start();
        }

        // ------------------------------------------------------------------------------------------------- the modes

        public void SetEditMode(bool edit)
        {
            _editMode = edit;
            ApplyClickThrough();
            UpdateChromeVisibility();
        }

        /// <summary>
        /// View mode makes the clone host click-through so mouse input reaches the game. A DWM thumbnail window
        /// cannot be per-pixel transparent, so this uses the classic layered + transparent styles instead.
        /// Edit mode removes both so the host behaves like an ordinary window.
        /// </summary>
        private void ApplyClickThrough()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            const int style = NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;

            if (_editMode)
            {
                NativeMethods.SetExStyle(_hwnd, style, false);
            }
            else
            {
                NativeMethods.SetExStyle(_hwnd, style, true);
                NativeMethods.SetLayeredWindowAttributes(_hwnd, 0, 255, NativeMethods.LWA_ALPHA);
            }
        }

        /// <summary>
        /// Shows the edit-mode windows (toolbar, crop dialog) only in edit mode, and never while interacting or hidden.
        /// </summary>
        private void UpdateChromeVisibility()
        {
            if (_closing)
            {
                return;
            }

            bool show = _editMode && !_interacting && !_hidden;

            if (_chrome != null)
            {
                if (show)
                {
                    _chrome.Show();
                    _chrome.SyncToView(_view);
                    _chrome.RefreshState();
                }
                else
                {
                    _chrome.Hide();
                }
            }

            // Hiding or minimizing an owner does not hide the windows it owns, so every window is shown/hidden explicitly.
            if (_cropWindow != null)
            {
                if (show)
                {
                    _cropWindow.Show();
                }
                else
                {
                    _cropWindow.Hide();
                }
            }
        }

        public void SetHidden(bool hidden)
        {
            if (hidden == _hidden)
            {
                return;
            }

            _hidden = hidden;

            if (hidden)
            {
                // Hiding this window does NOT hide the windows it owns, so hide each one explicitly:
                // the toolbar and crop dialog (via UpdateChromeVisibility, which sees _hidden), the image, then this window.
                UpdateChromeVisibility();
                _overlay?.Hide();
                Hide();
            }
            else
            {
                Show();
                _overlay?.Show();
                ApplyLayout();
                UpdateChromeVisibility();
            }
        }

        public void ToggleHideAll() => SetHidden(!_hidden);

        // -------------------------------------------------------------------------------------------------- layout

        /// <summary>Moves or resizes the overlay (screen pixels). Called by the toolbar's drag handles.</summary>
        internal void SetView(PxRect rect)
        {
            _view = new PxRect(rect.X, rect.Y, Math.Max(MinWidthPx, rect.Width), Math.Max(MinHeightPx, rect.Height));
            ApplyLayout();
        }

        /// <summary>Puts every window where it belongs and refreshes the replica. Safe to call at any time.</summary>
        private void ApplyLayout()
        {
            if (_closing || _inLayout || _hwnd == IntPtr.Zero || _overlay == null)
            {
                return;
            }

            bool targetGone = false;
            _inLayout = true;

            try
            {
                bool cloneVisible = _replica != null && !_interacting;
                PositionHost(cloneVisible);

                if (_replica != null)
                {
                    ReplicaStatus status = UpdateReplica(cloneVisible);

                    // Keep the window the same shape as the clone, so there are no empty bars around it.
                    if (status == ReplicaStatus.Ok && cloneVisible && _lockAspect
                        && ViewGeometry.AspectDiffers(_view, _replica.SourceAspect))
                    {
                        _view = ViewGeometry.FitToAspect(_view, _replica.SourceAspect);
                        PositionHost(true);
                        status = UpdateReplica(true);
                    }

                    if (status == ReplicaStatus.TargetGone)
                    {
                        targetGone = true;
                    }
                    else if (cloneVisible)
                    {
                        ShowStatus(status == ReplicaStatus.Ok
                            ? null
                            : "The cloned window is minimized or has nothing to show right now");
                    }
                }

                PlaceOverlay(cloneVisible);

                if (_chrome != null && _editMode && !_interacting && !_hidden)
                {
                    _chrome.SyncToView(_view);
                }
            }
            finally
            {
                _inLayout = false;
            }

            if (targetGone)
            {
                StopClone();
                ShowStatus("The cloned window was closed\n\n" + IdleHelp);
            }
        }

        /// <summary>The clone host fills the view rectangle while cloning, and is parked as an invisible 1x1 otherwise.</summary>
        private void PositionHost(bool show)
        {
            const uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;

            if (show)
            {
                NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, _view.X, _view.Y, _view.Width, _view.Height, flags);
            }
            else
            {
                NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, ParkX, ParkY, 1, 1, flags);
            }
        }

        private ReplicaStatus UpdateReplica(bool cloneVisible)
        {
            PxRect host = default;
            if (cloneVisible && NativeMethods.GetClientRect(_hwnd, out RECT client))
            {
                // The host's client area is the whole window (it has no frame), so the picture is laid out in it directly.
                host = new PxRect(0, 0, client.Width, client.Height);
            }

            return _replica!.Update(host); // an empty host hides the thumbnail
        }

        /// <summary>
        /// The overlay image covers exactly what the clone displays (so it lines up with it and scales with it),
        /// or the whole view rectangle when nothing is being cloned.
        /// </summary>
        private void PlaceOverlay(bool cloneVisible)
        {
            if (_overlay == null)
            {
                return;
            }

            if (_interacting)
            {
                _overlay.PlaceOver(ParkX, ParkY, 1, 1);
                return;
            }

            PxRect area = _view;

            if (cloneVisible && _replica != null && !_replica.DisplayedRect.IsEmpty)
            {
                POINT origin = default;
                if (NativeMethods.ClientToScreen(_hwnd, ref origin))
                {
                    PxRect shown = _replica.DisplayedRect;
                    area = new PxRect(origin.X + shown.X, origin.Y + shown.Y, shown.Width, shown.Height);
                }
            }

            _overlay.PlaceOver(area.X, area.Y, area.Width, area.Height);
        }

        private void ShowStatus(string? text)
        {
            _status = text;
            _chrome?.SetStatus(text);
        }

        /// <summary>Shows the idle hint only when there is neither a clone nor an image.</summary>
        private void RefreshStatus()
        {
            if (_replica == null)
            {
                ShowStatus(_image == null ? IdleHelp : null);
            }
        }

        // -------------------------------------------------------------------------------------------------- the crop

        /// <summary>Trims the edges of the cloned window. Applies live; kept for the next clone too.</summary>
        internal void SetCrop(CropFraction crop)
        {
            _crop = crop.Clamped();

            if (_replica != null)
            {
                _replica.Crop = _crop;
            }

            ApplyLayout(); // the clone's shape changed, so the overlay follows it
        }

        public void ShowCropWindow()
        {
            if (_cropWindow != null)
            {
                _cropWindow.Show();
                _cropWindow.Activate();
                return;
            }

            var window = new CropWindow(this, DialogOwner) { Topmost = _alwaysOnTop };
            window.Closed += (_, _) => _cropWindow = null;
            _cropWindow = window;
            window.Show();
        }

        // ------------------------------------------------------------------------------------------------ the image

        public void OpenImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(DialogOwner) == true)
            {
                LoadImage(dialog.FileName);
            }
        }

        private void LoadImage(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // don't keep the file locked
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();

                _image = bitmap;
                _overlay?.SetImage(bitmap);
                RefreshStatus();
                ApplyLayout();
                _chrome?.RefreshState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(DialogOwner, "Could not open that image.\n\n" + ex.Message,
                    "Image Display", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ClearImage()
        {
            _image = null;
            _overlay?.SetImage(null);
            RefreshStatus();
            _chrome?.RefreshState();
        }

        private void SetOpacity(byte alpha)
        {
            _opacity = alpha;

            if (_replica != null)
            {
                _replica.Opacity = alpha; // the thumbnail's own opacity (window alpha does not fade a thumbnail)
            }

            if (_overlay != null)
            {
                _overlay.Opacity = alpha / 255.0;
            }

            ApplyLayout();
        }

        // ----------------------------------------------------------------------------------------------- clone mode

        private void PopulateCloneItems(ItemCollection items)
        {
            items.Clear();

            IntPtr overlayHandle = _overlay != null ? new WindowInteropHelper(_overlay).Handle : IntPtr.Zero;
            IntPtr chromeHandle = _chrome != null ? new WindowInteropHelper(_chrome).Handle : IntPtr.Zero;
            List<CloneableWindow> windows = WindowList.Enumerate(_hwnd, overlayHandle, chromeHandle);

            if (windows.Count == 0)
            {
                items.Add(new MenuItem { Header = "(no windows found)", IsEnabled = false });
                return;
            }

            foreach (CloneableWindow window in windows)
            {
                string title = window.Title.Length > 60 ? window.Title.Substring(0, 60) + "\u2026" : window.Title;
                if (window.ProcessName.Length > 0)
                {
                    title += "  (" + window.ProcessName + ")";
                }

                var item = new MenuItem
                {
                    // '_' marks an access key in WPF menus; double it so titles show literally.
                    Header = title.Replace("_", "__"),
                    ToolTip = window.Title,
                };

                IntPtr handle = window.Handle;
                item.Click += (_, _) => StartClone(handle);
                items.Add(item);
            }
        }

        /// <summary>Shows a pick-a-window menu under <paramref name="anchor"/> (used by the toolbar).</summary>
        public void ShowCloneMenu(FrameworkElement anchor)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false, // close when the user clicks elsewhere
            };

            if (IsCloning)
            {
                menu.Items.Add(MakeItem("Stop cloning", StopClone));
                menu.Items.Add(new Separator());
            }

            PopulateCloneItems(menu.Items);
            menu.IsOpen = true;
        }

        private void StartClone(IntPtr target)
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            StopClone();

            ThumbnailReplica? replica = ThumbnailReplica.TryCreate(_hwnd, target, out int hresult);
            if (replica == null)
            {
                MessageBox.Show(DialogOwner,
                    $"Could not clone that window (DWM error 0x{hresult:X8}).\n\n" +
                    "Desktop composition must be enabled, and the window must still be open.\n\n" +
                    "If it belongs to a program running as administrator, close this app and start it again with " +
                    "right-click > Run as administrator.",
                    "Clone window", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshStatus();
                return;
            }

            replica.Opacity = _opacity;
            replica.Crop = _crop;
            _replica = replica;
            ShowStatus(null);
            _timer.Start();
            ApplyLayout();
            _chrome?.RefreshState();
        }

        private void StopClone()
        {
            _timer.Stop();
            _replica?.Dispose();
            _replica = null;

            if (_interacting)
            {
                _interacting = false;
                _returnTo = IntPtr.Zero;
            }

            ApplyLayout(); // parks the clone host; the image goes back to covering the whole view
            UpdateChromeVisibility();
            RefreshStatus();
            _chrome?.RefreshState();
        }

        // ---------------------------------------------------------------------------------------------- interacting

        /// <summary>
        /// Brings the real cloned window to the front of the game so it can be used normally (keyboard, scroll wheel,
        /// middle-drag, everything), and sends it back and returns focus to the game when called again.
        /// This is the toolbar / menu version, which affects only this copy; the hotkey affects every copy.
        /// </summary>
        public void ToggleInteract()
        {
            if (_interacting)
            {
                EndInteract();
                return;
            }

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            StartInteract(foreground != IntPtr.Zero && !BelongsToThisProcess(foreground) ? foreground : new IntPtr(1));
        }

        /// <param name="game">The window to give focus back to afterwards (a handle of 1 or less means "unknown").</param>
        private void StartInteract(IntPtr game)
        {
            if (_replica == null || _hidden || _interacting)
            {
                return;
            }

            IntPtr target = _replica.Target;
            _returnTo = game.ToInt64() > 1 && game != target && NativeMethods.IsWindow(game) ? game : IntPtr.Zero;

            _interacting = true;
            ApplyLayout();            // parks the clone and the image so they don't cover the real window
            UpdateChromeVisibility(); // hides the toolbar

            if (NativeMethods.IsIconic(target))
            {
                NativeMethods.ShowWindow(target, NativeMethods.SW_RESTORE);
            }

            // Above the game even if the game is a topmost window.
            NativeMethods.SetWindowPos(target, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.BringToForeground(target);
        }

        private void EndInteract()
        {
            IntPtr target = _replica != null ? _replica.Target : IntPtr.Zero;
            _interacting = false;

            // Back behind the game (this also drops its topmost flag).
            if (target != IntPtr.Zero && NativeMethods.IsWindow(target))
            {
                NativeMethods.SetWindowPos(target, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            if (_returnTo != IntPtr.Zero && NativeMethods.IsWindow(_returnTo))
            {
                NativeMethods.BringToForeground(_returnTo);
            }

            _returnTo = IntPtr.Zero;
            ApplyLayout();
            UpdateChromeVisibility();
        }

        private static bool BelongsToThisProcess(IntPtr hwnd)
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == (uint)Environment.ProcessId;
        }

        // ------------------------------------------------------------------------------------------------- the menu

        private static MenuItem MakeItem(string header, Action onClick, bool isChecked = false, bool isEnabled = true,
            string? gesture = null)
        {
            var item = new MenuItem { Header = header, IsChecked = isChecked, IsEnabled = isEnabled };
            if (gesture != null)
            {
                item.InputGestureText = gesture;
            }

            item.Click += (_, _) => onClick();
            return item;
        }

        /// <summary>The full menu (right-click on the overlay in edit mode, or the toolbar's "..." button).</summary>
        public void ShowMainMenu(FrameworkElement anchor)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.MousePoint,
                StaysOpen = false,
            };

            HotkeySettings keys = _settings.Hotkeys;

            menu.Items.Add(MakeItem("Open image...", OpenImage));
            menu.Items.Add(MakeItem("Remove image", ClearImage, isEnabled: HasImage));
            menu.Items.Add(new Separator());

            var clone = new MenuItem { Header = "Clone window" };
            PopulateCloneItems(clone.Items);
            menu.Items.Add(clone);
            menu.Items.Add(MakeItem("Stop cloning", StopClone, isEnabled: IsCloning));
            menu.Items.Add(MakeItem("Crop clone...", ShowCropWindow, isEnabled: IsCloning));
            menu.Items.Add(MakeItem("Interact with cloned window", ToggleInteract, isEnabled: IsCloning,
                gesture: keys.Interact.DisplayName));
            menu.Items.Add(new Separator());

            menu.Items.Add(MakeItem("Lock clone's aspect ratio", () =>
            {
                _lockAspect = !_lockAspect;
                ApplyLayout();
            }, isChecked: _lockAspect));
            menu.Items.Add(MakeItem("Always on top", () => AlwaysOnTop = !AlwaysOnTop, isChecked: _alwaysOnTop));

            var opacity = new MenuItem { Header = "Opacity" };
            foreach (int percent in new[] { 100, 80, 60, 40, 20 })
            {
                byte alpha = (byte)(percent * 255 / 100);
                opacity.Items.Add(MakeItem(percent + "%", () => SetOpacity(alpha), isChecked: _opacity == alpha));
            }

            menu.Items.Add(opacity);
            menu.Items.Add(new Separator());

            menu.Items.Add(MakeItem("View mode (hide controls)", () => SetEditMode(false), gesture: keys.EditMode.DisplayName));
            menu.Items.Add(MakeItem("Hide everything", ToggleHideAll, gesture: keys.HideAll.DisplayName));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Settings...", ShowSettings));
            menu.Items.Add(MakeItem("Exit", Close));

            menu.IsOpen = true;
        }
    }
}
