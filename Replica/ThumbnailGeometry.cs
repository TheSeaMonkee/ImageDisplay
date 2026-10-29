// SPDX-License-Identifier: MS-RL
// Portions Copyright (c) Lorenz Cuno Klopfenstein 2010 (OnTopReplica, https://github.com/LorenzCK/OnTopReplica),
// licensed under the Microsoft Reciprocal License (MS-RL). This file follows that project's technique and structure
// and is therefore also under MS-RL: see LICENSE-OnTopReplica.txt.
// Modifications Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using ImageDisplayApp.Native;

namespace ImageDisplayApp.Replica;

/// <summary>Integer pixel rectangle (x, y, width, height).</summary>
internal readonly record struct PxRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Which on-screen rectangle the top-left of a DWM thumbnail source corresponds to.
/// On Windows 10/11 a window's <c>GetWindowRect</c> includes invisible resize borders, while its DWM "extended frame
/// bounds" are the visible edges; the thumbnail source matches one or the other.
/// </summary>
internal enum OriginKind
{
    WindowRect,
    FrameBounds,
}

/// <summary>
/// Pure coordinate math for the window replica, kept free of Win32 calls so it can be unit-tested.
/// All values are physical pixels.
/// </summary>
internal static class ThumbnailGeometry
{
    /// <summary>Fits a source size into a host area, preserving aspect ratio and centering (letterboxing).</summary>
    public static PxRect Fit(int hostWidth, int hostHeight, int sourceWidth, int sourceHeight)
    {
        if (hostWidth <= 0 || hostHeight <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return default;
        }

        double scale = Math.Min((double)hostWidth / sourceWidth, (double)hostHeight / sourceHeight);
        int w = Math.Clamp((int)Math.Round(sourceWidth * scale), 1, hostWidth);
        int h = Math.Clamp((int)Math.Round(sourceHeight * scale), 1, hostHeight);
        return new PxRect((hostWidth - w) / 2, (hostHeight - h) / 2, w, h);
    }

    /// <summary>
    /// Decides which rectangle the thumbnail source's origin maps to, by matching the source size DWM reports
    /// against the window rectangle and the visible frame bounds.
    /// </summary>
    public static OriginKind ChooseOrigin(SIZE sourceSize, RECT windowRect, RECT frameBounds)
    {
        if (sourceSize.Width == windowRect.Width && sourceSize.Height == windowRect.Height)
        {
            return OriginKind.WindowRect;
        }

        if (sourceSize.Width == frameBounds.Width && sourceSize.Height == frameBounds.Height)
        {
            return OriginKind.FrameBounds;
        }

        return OriginKind.WindowRect;
    }

    /// <summary>
    /// Computes the part of the thumbnail source (in source coordinates) that corresponds to the target window's
    /// client area. Using the real client origin avoids any guessing about title bar, border or menu sizes.
    /// Falls back to the whole source if the client area cannot be placed inside it.
    /// </summary>
    public static PxRect PlanSourceRect(OriginKind origin, RECT windowRect, RECT frameBounds, SIZE sourceSize,
        POINT clientOriginOnScreen, int clientWidth, int clientHeight)
    {
        RECT o = origin == OriginKind.FrameBounds ? frameBounds : windowRect;

        int x0 = Math.Max(0, clientOriginOnScreen.X - o.Left);
        int y0 = Math.Max(0, clientOriginOnScreen.Y - o.Top);
        int x1 = Math.Min(sourceSize.Width, clientOriginOnScreen.X - o.Left + clientWidth);
        int y1 = Math.Min(sourceSize.Height, clientOriginOnScreen.Y - o.Top + clientHeight);

        if (x1 - x0 < 1 || y1 - y0 < 1)
        {
            return new PxRect(0, 0, sourceSize.Width, sourceSize.Height);
        }

        return new PxRect(x0, y0, x1 - x0, y1 - y0);
    }
}
