// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using ImageDisplayApp.Replica;

namespace ImageDisplayApp;

/// <summary>Which edge(s) of the overlay a resize drag is holding. A corner is two flags.</summary>
[Flags]
internal enum ResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
}

/// <summary>
/// How much to trim from each edge of the cloned window, as a fraction of its size (0 = nothing, 0.25 = a quarter).
/// Fractions, not pixels, so the crop still fits if the cloned window is resized.
/// </summary>
internal readonly record struct CropFraction(double Left, double Top, double Right, double Bottom)
{
    public static CropFraction None => default;

    public bool IsNone => Left <= 0 && Top <= 0 && Right <= 0 && Bottom <= 0;

    /// <summary>Keeps every value in range and always leaves at least <paramref name="minRemaining"/> of each dimension.</summary>
    public CropFraction Clamped(double minRemaining = 0.1)
    {
        double maxTrim = 1.0 - minRemaining;

        double left = Math.Clamp(Left, 0, maxTrim);
        double right = Math.Clamp(Right, 0, maxTrim - left);
        double top = Math.Clamp(Top, 0, maxTrim);
        double bottom = Math.Clamp(Bottom, 0, maxTrim - top);
        return new CropFraction(left, top, right, bottom);
    }
}

/// <summary>Pure rectangle math for moving and resizing the overlay (all values are screen pixels).</summary>
internal static class ViewGeometry
{
    /// <summary>
    /// The rectangle that results from dragging <paramref name="edges"/> of <paramref name="start"/> by (dx, dy).
    /// The edges that are not being dragged stay put. With <paramref name="aspect"/> &gt; 0 (width / height) the
    /// result keeps that ratio: side handles let the other dimension follow, corners are driven by the width.
    /// </summary>
    public static PxRect Resize(PxRect start, ResizeEdges edges, int dx, int dy, int minWidth, int minHeight, double aspect)
    {
        int left = start.X;
        int top = start.Y;
        int right = start.Right;
        int bottom = start.Bottom;

        if (edges.HasFlag(ResizeEdges.Left))
        {
            left = Math.Min(left + dx, right - minWidth);
        }

        if (edges.HasFlag(ResizeEdges.Right))
        {
            right = Math.Max(right + dx, left + minWidth);
        }

        if (edges.HasFlag(ResizeEdges.Top))
        {
            top = Math.Min(top + dy, bottom - minHeight);
        }

        if (edges.HasFlag(ResizeEdges.Bottom))
        {
            bottom = Math.Max(bottom + dy, top + minHeight);
        }

        int width = right - left;
        int height = bottom - top;

        if (aspect <= 0)
        {
            return new PxRect(left, top, width, height);
        }

        bool horizontal = (edges & (ResizeEdges.Left | ResizeEdges.Right)) != 0;
        bool vertical = (edges & (ResizeEdges.Top | ResizeEdges.Bottom)) != 0;

        if (vertical && !horizontal)
        {
            width = (int)Math.Round(height * aspect);
        }
        else
        {
            height = (int)Math.Round(width / aspect);
        }

        // Respect the minimums without breaking the ratio.
        if (width < minWidth)
        {
            width = minWidth;
            height = (int)Math.Round(width / aspect);
        }

        if (height < minHeight)
        {
            height = minHeight;
            width = (int)Math.Round(height * aspect);
        }

        // The edge opposite the dragged one is the anchor.
        int x = edges.HasFlag(ResizeEdges.Left) ? start.Right - width : start.X;
        int y = edges.HasFlag(ResizeEdges.Top) ? start.Bottom - height : start.Y;
        return new PxRect(x, y, width, height);
    }

    /// <summary>The part of <paramref name="source"/> that remains after trimming the edges by <paramref name="crop"/>.</summary>
    public static PxRect ApplyCrop(PxRect source, CropFraction crop)
    {
        if (crop.IsNone || source.IsEmpty)
        {
            return source;
        }

        CropFraction c = crop.Clamped();
        int left = (int)Math.Round(source.Width * c.Left);
        int right = (int)Math.Round(source.Width * c.Right);
        int top = (int)Math.Round(source.Height * c.Top);
        int bottom = (int)Math.Round(source.Height * c.Bottom);

        return new PxRect(
            source.X + left,
            source.Y + top,
            Math.Max(1, source.Width - left - right),
            Math.Max(1, source.Height - top - bottom));
    }

    /// <summary>Adjusts the height so the rectangle has the given aspect ratio (top-left stays put).</summary>
    public static PxRect FitToAspect(PxRect rect, double aspect)
    {
        if (aspect <= 0 || rect.Width <= 0)
        {
            return rect;
        }

        return rect with { Height = Math.Max(1, (int)Math.Round(rect.Width / aspect)) };
    }

    /// <summary>True if the rectangle's ratio differs from <paramref name="aspect"/> by more than <paramref name="tolerance"/> (relative).</summary>
    public static bool AspectDiffers(PxRect rect, double aspect, double tolerance = 0.015)
    {
        if (aspect <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        double current = (double)rect.Width / rect.Height;
        return Math.Abs(current / aspect - 1.0) > tolerance;
    }
}
