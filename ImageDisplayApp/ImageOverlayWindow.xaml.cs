// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ImageDisplayApp.Native;

namespace ImageDisplayApp
{
    /// <summary>
    /// Transparent, click-through window that draws the overlay image on top of the cloned window.
    /// It has to be its own window for two reasons: the replica (a DWM thumbnail) is always drawn above anything
    /// the main window renders itself, and the main window cannot be per-pixel transparent without breaking the replica.
    /// </summary>
    public partial class ImageOverlayWindow : Window
    {
        public ImageOverlayWindow(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Click-through: mouse input goes to whatever is underneath (the game).
            // No-activate: clicking never pulls focus away from the game.
            IntPtr handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetExStyle(handle, NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE, true);
        }

        public void SetImage(ImageSource? source)
        {
            image.Source = source;
        }

        /// <summary>Places the window over a screen rectangle given in physical pixels.</summary>
        public void PlaceOver(int x, int y, int width, int height)
        {
            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, x, y, Math.Max(1, width), Math.Max(1, height),
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
    }
}
