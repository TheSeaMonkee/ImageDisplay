// SPDX-License-Identifier: MS-RL
// Portions Copyright (c) Lorenz Cuno Klopfenstein 2010 (OnTopReplica, https://github.com/LorenzCK/OnTopReplica),
// licensed under the Microsoft Reciprocal License (MS-RL). This file follows that project's technique and structure
// and is therefore also under MS-RL: see LICENSE-OnTopReplica.txt.
// Modifications Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Runtime.InteropServices;
using ImageDisplayApp.Native;

namespace ImageDisplayApp.Replica;

internal enum ReplicaStatus
{
    /// <summary>The replica is showing the target's client area.</summary>
    Ok,

    /// <summary>The target exists but there is currently nothing to show (e.g. it is minimized).</summary>
    Unavailable,

    /// <summary>The target window no longer exists.</summary>
    TargetGone,
}

/// <summary>
/// A live DWM thumbnail of another top-level window, drawn into a host area of our own window.
/// Only the target's client area is shown (no title bar / border / menu).
/// </summary>
internal sealed class ThumbnailReplica : IDisposable
{
    private IntPtr _thumbnail;

    private PxRect _source;   // part of the thumbnail source being shown, in source coordinates

    private bool _hasApplied;
    private PxRect _appliedDestination;
    private PxRect _appliedSource;
    private bool _appliedVisible;
    private byte _appliedOpacity;

    private ThumbnailReplica(IntPtr target, IntPtr thumbnail)
    {
        Target = target;
        _thumbnail = thumbnail;
    }

    public IntPtr Target { get; }

    /// <summary>0 (invisible) to 255 (opaque). Applied on the next <see cref="Update"/>.</summary>
    /// <remarks>
    /// Done through the thumbnail's own opacity property: the destination window's alpha does not fade a DWM thumbnail.
    /// </remarks>
    public byte Opacity { get; set; } = 255;

    /// <summary>Edges of the cloned window to trim away (e.g. to hide parts of its interface). Applied on the next <see cref="Update"/>.</summary>
    public CropFraction Crop { get; set; }

    /// <summary>
    /// Where the replica's picture currently is, in the destination window's client pixels (empty when nothing is
    /// shown). Lets the overlay image line up with exactly what is being displayed.
    /// </summary>
    public PxRect DisplayedRect { get; private set; }

    /// <summary>Width / height of the part of the target being shown, or 0 when nothing is shown.</summary>
    public double SourceAspect => _source.IsEmpty ? 0 : (double)_source.Width / _source.Height;

    /// <summary>
    /// Starts replicating <paramref name="target"/> into <paramref name="destination"/>.
    /// Returns null if DWM refuses (e.g. composition off, or the target is elevated and we are not).
    /// </summary>
    public static ThumbnailReplica? TryCreate(IntPtr destination, IntPtr target, out int hresult)
    {
        hresult = NativeMethods.DwmRegisterThumbnail(destination, target, out IntPtr thumbnail);
        if (hresult != 0 || thumbnail == IntPtr.Zero)
        {
            return null;
        }

        return new ThumbnailReplica(target, thumbnail);
    }

    /// <summary>
    /// Recomputes what part of the target to show and where, and pushes it to DWM.
    /// Cheap enough to call on a timer: nothing is sent to DWM when nothing changed.
    /// </summary>
    /// <param name="hostRect">The area to draw into, in the destination window's client pixels.</param>
    public ReplicaStatus Update(PxRect hostRect)
    {
        if (_thumbnail == IntPtr.Zero || !NativeMethods.IsWindow(Target))
        {
            return ReplicaStatus.TargetGone;
        }

        if (hostRect.IsEmpty || NativeMethods.IsIconic(Target))
        {
            return Hide();
        }

        if (NativeMethods.DwmQueryThumbnailSourceSize(_thumbnail, out SIZE sourceSize) != 0
            || sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return Hide();
        }

        if (!NativeMethods.GetWindowRect(Target, out RECT windowRect)
            || !NativeMethods.GetClientRect(Target, out RECT clientRect)
            || clientRect.Width <= 0 || clientRect.Height <= 0)
        {
            return Hide();
        }

        RECT frameBounds = windowRect;
        if (NativeMethods.DwmGetWindowAttribute(Target, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out RECT frame, Marshal.SizeOf<RECT>()) == 0)
        {
            frameBounds = frame;
        }

        POINT clientOrigin = default;
        if (!NativeMethods.ClientToScreen(Target, ref clientOrigin))
        {
            return Hide();
        }

        OriginKind origin = ThumbnailGeometry.ChooseOrigin(sourceSize, windowRect, frameBounds);
        _source = ThumbnailGeometry.PlanSourceRect(origin, windowRect, frameBounds, sourceSize, clientOrigin,
            clientRect.Width, clientRect.Height);
        _source = ViewGeometry.ApplyCrop(_source, Crop);
        PxRect fitted = ThumbnailGeometry.Fit(hostRect.Width, hostRect.Height, _source.Width, _source.Height);

        var destination = new PxRect(hostRect.X + fitted.X, hostRect.Y + fitted.Y, fitted.Width, fitted.Height);
        DisplayedRect = destination;
        return Apply(destination, _source, visible: true);
    }

    public void Dispose()
    {
        if (_thumbnail != IntPtr.Zero)
        {
            NativeMethods.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = IntPtr.Zero;
        }
    }

    private ReplicaStatus Hide()
    {
        _source = default;
        DisplayedRect = default;
        return Apply(default, default, visible: false);
    }

    private ReplicaStatus Apply(PxRect destination, PxRect source, bool visible)
    {
        bool unchanged = _hasApplied
            && _appliedVisible == visible
            && _appliedOpacity == Opacity
            && _appliedDestination == destination
            && _appliedSource == source;

        if (!unchanged)
        {
            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeMethods.DWM_TNP_VISIBLE | NativeMethods.DWM_TNP_OPACITY,
                opacity = Opacity,
                fVisible = visible ? 1 : 0,
                // fSourceClientAreaOnly is deliberately left unset: the whole window is the source and the client
                // area is selected explicitly through rcSource, so the coordinates are unambiguous.
            };

            if (visible)
            {
                props.dwFlags |= NativeMethods.DWM_TNP_RECTDESTINATION | NativeMethods.DWM_TNP_RECTSOURCE;
                props.rcDestination = new RECT(destination.X, destination.Y, destination.Right, destination.Bottom);
                props.rcSource = new RECT(source.X, source.Y, source.Right, source.Bottom);
            }

            if (NativeMethods.DwmUpdateThumbnailProperties(_thumbnail, ref props) != 0)
            {
                return NativeMethods.IsWindow(Target) ? ReplicaStatus.Unavailable : ReplicaStatus.TargetGone;
            }

            _hasApplied = true;
            _appliedVisible = visible;
            _appliedOpacity = Opacity;
            _appliedDestination = destination;
            _appliedSource = source;
        }

        return visible ? ReplicaStatus.Ok : ReplicaStatus.Unavailable;
    }
}
