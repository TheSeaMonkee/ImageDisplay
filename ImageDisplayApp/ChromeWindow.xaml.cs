// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ImageDisplayApp.Native;
using ImageDisplayApp.Replica;

namespace ImageDisplayApp
{
    /// <summary>
    /// Edit-mode window laid exactly over the overlay: the toolbar, the move surface and the edge and corner resize
    /// handles. It has to be a separate window because a DWM thumbnail (the window replica) is always drawn above
    /// whatever the main window renders itself. It is hidden in view mode, so the game gets the mouse back.
    /// </summary>
    public partial class ChromeWindow : Window
    {
        private enum DragKind
        {
            None,
            Move,
            Resize,
        }

        private readonly MainWindow _main;

        private DragKind _drag = DragKind.None;
        private ResizeEdges _edges;
        private POINT _startCursor;
        private PxRect _startView;
        private double _startAspect;

        /// <param name="main">The window that owns the view rectangle this toolbar edits.</param>
        /// <param name="owner">The window to stack directly above (the image overlay), so the toolbar is always on top of it.</param>
        public ChromeWindow(MainWindow main, Window owner)
        {
            InitializeComponent();
            _main = main;
            Owner = owner;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Using the toolbar must never pull keyboard focus away from the game.
            NativeMethods.SetExStyle(new WindowInteropHelper(this).Handle, NativeMethods.WS_EX_NOACTIVATE, true);
        }

        /// <summary>Lays this window exactly over the view rectangle (screen pixels).</summary>
        internal void SyncToView(PxRect view)
        {
            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, view.X, view.Y, Math.Max(1, view.Width), Math.Max(1, view.Height),
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        /// <summary>Updates the buttons to reflect the main window's state.</summary>
        public void RefreshState()
        {
            btnClear.IsEnabled = _main.HasImage;
            btnInteract.IsEnabled = _main.IsCloning;
            btnTop.IsChecked = _main.AlwaysOnTop;
        }

        /// <summary>Shows a hint / status message in the middle of the overlay (null to clear).</summary>
        public void SetStatus(string? text)
        {
            statusText.Text = text ?? string.Empty;
            statusText.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
        }

        // ---------------------------------------------------------------------------------------------- toolbar buttons

        private void Open_Click(object sender, RoutedEventArgs e) => _main.OpenImage();

        private void Clear_Click(object sender, RoutedEventArgs e) => _main.ClearImage();

        private void Clone_Click(object sender, RoutedEventArgs e) => _main.ShowCloneMenu((FrameworkElement)sender);

        private void Interact_Click(object sender, RoutedEventArgs e) => _main.ToggleInteract();

        private void Top_Click(object sender, RoutedEventArgs e) => _main.AlwaysOnTop = btnTop.IsChecked == true;

        private void More_Click(object sender, RoutedEventArgs e) => _main.ShowMainMenu((FrameworkElement)sender);

        private void View_Click(object sender, RoutedEventArgs e) => _main.SetEditMode(false);

        private void Close_Click(object sender, RoutedEventArgs e) => _main.Close();

        private void Surface_RightClick(object sender, MouseButtonEventArgs e) =>
            _main.ShowMainMenu((FrameworkElement)sender);

        // ------------------------------------------------------------------------------------------- move / resize drag

        private void Surface_MouseDown(object sender, MouseButtonEventArgs e) =>
            BeginDrag(DragKind.Move, ResizeEdges.None, (UIElement)sender, e);

        private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && TryParseEdges(element.Tag, out ResizeEdges edges))
            {
                BeginDrag(DragKind.Resize, edges, element, e);
            }
        }

        private static bool TryParseEdges(object? tag, out ResizeEdges edges)
        {
            edges = tag switch
            {
                "L" => ResizeEdges.Left,
                "R" => ResizeEdges.Right,
                "T" => ResizeEdges.Top,
                "B" => ResizeEdges.Bottom,
                "TL" => ResizeEdges.Top | ResizeEdges.Left,
                "TR" => ResizeEdges.Top | ResizeEdges.Right,
                "BL" => ResizeEdges.Bottom | ResizeEdges.Left,
                "BR" => ResizeEdges.Bottom | ResizeEdges.Right,
                _ => ResizeEdges.None,
            };

            return edges != ResizeEdges.None;
        }

        private void BeginDrag(DragKind kind, ResizeEdges edges, UIElement source, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            NativeMethods.GetCursorPos(out _startCursor);
            _startView = _main.View;
            _startAspect = _main.AspectForResize;
            _drag = kind;
            _edges = edges;
            source.CaptureMouse();
            e.Handled = true;
        }

        // Works in raw screen pixels (cursor position and the view rectangle), so DPI scaling cannot skew it.
        private void Drag_MouseMove(object sender, MouseEventArgs e)
        {
            if (_drag == DragKind.None)
            {
                return;
            }

            NativeMethods.GetCursorPos(out POINT cursor);
            int dx = cursor.X - _startCursor.X;
            int dy = cursor.Y - _startCursor.Y;

            if (_drag == DragKind.Move)
            {
                _main.SetView(new PxRect(_startView.X + dx, _startView.Y + dy, _startView.Width, _startView.Height));
            }
            else
            {
                _main.SetView(ViewGeometry.Resize(_startView, _edges, dx, dy,
                    MainWindow.MinWidthPx, MainWindow.MinHeightPx, _startAspect));
            }
        }

        private void Drag_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_drag == DragKind.None)
            {
                return;
            }

            _drag = DragKind.None;
            ((UIElement)sender).ReleaseMouseCapture();
            e.Handled = true;
        }
    }
}
