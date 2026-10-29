// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using ImageDisplayApp;
using ImageDisplayApp.Native;
using ImageDisplayApp.Replica;
using ImageDisplayApp.Settings;

int pass = 0, fail = 0;
void Check(bool ok, string name)
{
    if (ok) { pass++; Console.WriteLine("  ok   " + name); }
    else { fail++; Console.WriteLine("  FAIL " + name); }
}

// ================================================================ Fit
Console.WriteLine("Fit");
var f = ThumbnailGeometry.Fit(800, 450, 400, 400);
Check(f == new PxRect(175, 0, 450, 450), $"square in 800x450 -> {f}");
f = ThumbnailGeometry.Fit(800, 450, 1600, 900);
Check(f == new PxRect(0, 0, 800, 450), $"same aspect fills -> {f}");
f = ThumbnailGeometry.Fit(400, 400, 1000, 500);
Check(f == new PxRect(0, 100, 400, 200), $"wide in square -> {f}");
Check(ThumbnailGeometry.Fit(0, 100, 10, 10).IsEmpty, "zero host is empty");
Check(ThumbnailGeometry.Fit(100, 100, 0, 10).IsEmpty, "zero source is empty");

// ================================================================ Source planning
Console.WriteLine("ChooseOrigin / PlanSourceRect");
var win = new RECT(100, 100, 1100, 800);
var ss = new SIZE(1000, 700);
var origin = ThumbnailGeometry.ChooseOrigin(ss, win, win);
Check(origin == OriginKind.WindowRect, "source == window rect -> WindowRect");
var src = ThumbnailGeometry.PlanSourceRect(origin, win, win, ss, new POINT(108, 131), 984, 661);
Check(src == new PxRect(8, 31, 984, 661), $"classic frame client rect -> {src}");

var winInv = new RECT(92, 100, 1108, 808);
var frame = new RECT(100, 100, 1100, 800);
origin = ThumbnailGeometry.ChooseOrigin(new SIZE(1000, 700), winInv, frame);
Check(origin == OriginKind.FrameBounds, "source == visible frame -> FrameBounds");
src = ThumbnailGeometry.PlanSourceRect(origin, winInv, frame, new SIZE(1000, 700), new POINT(101, 131), 998, 668);
Check(src == new PxRect(1, 31, 998, 668), $"invisible-border case -> {src}");
Check(ThumbnailGeometry.ChooseOrigin(new SIZE(1016, 708), winInv, frame) == OriginKind.WindowRect, "source == window rect incl. invisible borders -> WindowRect");
Check(ThumbnailGeometry.ChooseOrigin(new SIZE(5, 5), win, frame) == OriginKind.WindowRect, "unknown size falls back to WindowRect");
src = ThumbnailGeometry.PlanSourceRect(OriginKind.WindowRect, win, win, new SIZE(1000, 700), new POINT(5000, 5000), 100, 100);
Check(src == new PxRect(0, 0, 1000, 700), "unplaceable client rect falls back to whole source");
src = ThumbnailGeometry.PlanSourceRect(OriginKind.WindowRect, win, win, new SIZE(1000, 700), new POINT(100, 100), 2000, 2000);
Check(src == new PxRect(0, 0, 1000, 700), $"oversized client clamped -> {src}");

// ================================================================ Resize: free (no aspect)
Console.WriteLine("ViewGeometry.Resize (free)");
var r0 = new PxRect(100, 100, 400, 200);   // right=500 bottom=300
const int MinW = 120, MinH = 80;
PxRect R(ResizeEdges e, int dx, int dy, double aspect = 0) => ViewGeometry.Resize(r0, e, dx, dy, MinW, MinH, aspect);

Check(R(ResizeEdges.Right, 50, 99) == new PxRect(100, 100, 450, 200), "right edge: width grows, height ignores dy");
Check(R(ResizeEdges.Bottom, 99, 40) == new PxRect(100, 100, 400, 240), "bottom edge: height grows, width ignores dx");
Check(R(ResizeEdges.Left, -30, 0) == new PxRect(70, 100, 430, 200), "left edge: x moves, right stays at 500");
Check(R(ResizeEdges.Top, 0, -20) == new PxRect(100, 80, 400, 220), "top edge: y moves, bottom stays at 300");
Check(R(ResizeEdges.Left | ResizeEdges.Top, -10, -10) == new PxRect(90, 90, 410, 210), "top-left corner");
Check(R(ResizeEdges.Right | ResizeEdges.Bottom, 10, 10) == new PxRect(100, 100, 410, 210), "bottom-right corner");
Check(R(ResizeEdges.Right | ResizeEdges.Top, 10, -10) == new PxRect(100, 90, 410, 210), "top-right corner");
Check(R(ResizeEdges.Left | ResizeEdges.Bottom, -10, 10) == new PxRect(90, 100, 410, 210), "bottom-left corner");
var shrunk = R(ResizeEdges.Right, -9999, 0);
Check(shrunk == new PxRect(100, 100, MinW, 200), $"right edge stops at minimum width -> {shrunk}");
shrunk = R(ResizeEdges.Left, 9999, 0);
Check(shrunk.Right == 500 && shrunk.Width == MinW, $"left edge stops at minimum and keeps the right edge fixed -> {shrunk}");
shrunk = R(ResizeEdges.Top, 0, 9999);
Check(shrunk.Bottom == 300 && shrunk.Height == MinH, $"top edge stops at minimum and keeps the bottom edge fixed -> {shrunk}");
Check(R(ResizeEdges.None, 50, 50) == r0, "no edges -> unchanged");

// ================================================================ Resize: aspect locked (2:1)
Console.WriteLine("ViewGeometry.Resize (aspect locked 2:1)");
Check(R(ResizeEdges.Right, 100, 0, 2.0) == new PxRect(100, 100, 500, 250), "right edge: height follows, top stays");
Check(R(ResizeEdges.Left, -100, 0, 2.0) == new PxRect(0, 100, 500, 250), "left edge: right stays at 500, height follows");
Check(R(ResizeEdges.Bottom, 0, 50, 2.0) == new PxRect(100, 100, 500, 250), "bottom edge: width follows, left stays");
Check(R(ResizeEdges.Top, 0, -50, 2.0) == new PxRect(100, 50, 500, 250), "top edge: bottom stays at 300, width follows");
var c = R(ResizeEdges.Right | ResizeEdges.Bottom, 100, 7, 2.0);
Check(c == new PxRect(100, 100, 500, 250), $"bottom-right corner is driven by width -> {c}");
c = R(ResizeEdges.Left | ResizeEdges.Top, -100, -3, 2.0);
Check(c == new PxRect(0, 50, 500, 250) && c.Right == 500 && c.Bottom == 300, $"top-left corner anchors bottom-right at (500,300) -> {c}");
c = R(ResizeEdges.Right, -9999, 0, 2.0);
Check(c.Width >= MinW && c.Height >= MinH && Math.Abs((double)c.Width / c.Height - 2.0) < 0.02, $"min size keeps ratio -> {c}");
c = R(ResizeEdges.Bottom, 0, -9999, 2.0);
Check(c.Width >= MinW && c.Height >= MinH && Math.Abs((double)c.Width / c.Height - 2.0) < 0.02, $"min height keeps ratio -> {c}");

// ================================================================ Aspect helpers
Console.WriteLine("FitToAspect / AspectDiffers");
Check(ViewGeometry.FitToAspect(new PxRect(10, 20, 800, 450), 2.0) == new PxRect(10, 20, 800, 400), "FitToAspect keeps x/y/width, changes height");
Check(ViewGeometry.FitToAspect(new PxRect(10, 20, 800, 450), 0) == new PxRect(10, 20, 800, 450), "aspect 0 means free");
Check(!ViewGeometry.AspectDiffers(new PxRect(0, 0, 800, 400), 2.0), "exact ratio does not differ");
Check(!ViewGeometry.AspectDiffers(new PxRect(0, 0, 800, 401), 2.0), "within tolerance does not differ (no adjust loop)");
Check(ViewGeometry.AspectDiffers(new PxRect(0, 0, 800, 450), 2.0), "clearly different ratio differs");
// the auto-adjust must converge in one step for any size the user can reach
bool converges = true;
for (int w = 120; w <= 3840; w += 37)
    foreach (double a in new[] { 16.0 / 9, 4.0 / 3, 21.0 / 9, 1.0, 0.5625, 2.4 })
    {
        var fitted = ViewGeometry.FitToAspect(new PxRect(0, 0, w, 100), a);
        if (fitted.Height >= MinH && ViewGeometry.AspectDiffers(fitted, a)) converges = false;
    }
Check(converges, "FitToAspect always lands within tolerance (no resize feedback loop)");

// ================================================================ Crop
Console.WriteLine("CropFraction / ApplyCrop");
var client = new PxRect(10, 20, 1000, 500);
Check(ViewGeometry.ApplyCrop(client, CropFraction.None) == client, "no crop leaves the source unchanged");
var cropped = ViewGeometry.ApplyCrop(client, new CropFraction(0.1, 0.2, 0.1, 0));
Check(cropped == new PxRect(110, 120, 800, 400), $"trim 10% left/right and 20% top -> {cropped}");
cropped = ViewGeometry.ApplyCrop(client, new CropFraction(0, 0, 0, 0.5));
Check(cropped == new PxRect(10, 20, 1000, 250), $"trim bottom half -> {cropped}");
cropped = ViewGeometry.ApplyCrop(client, new CropFraction(0.9, 0.9, 0.9, 0.9));
Check(cropped.Width >= 100 && cropped.Height >= 50 && cropped.X + cropped.Width <= 1010 && cropped.Y + cropped.Height <= 520,
      $"extreme crop still leaves a visible area inside the source -> {cropped}");
var cl = new CropFraction(2, -1, 5, 0.3).Clamped();
Check(cl.Left <= 0.9 && cl.Top == 0 && cl.Right + cl.Left <= 0.9 + 1e-9, $"out-of-range values are clamped -> {cl}");
bool alwaysInside = true;
foreach (double l in new[] { 0.0, 0.3, 0.6, 0.95 })
    foreach (double r in new[] { 0.0, 0.3, 0.6, 0.95 })
        foreach (double t in new[] { 0.0, 0.5, 0.95 })
        {
            var c2 = ViewGeometry.ApplyCrop(client, new CropFraction(l, t, r, t));
            if (c2.Width < 1 || c2.Height < 1 || c2.X < client.X || c2.Y < client.Y || c2.Right > client.Right || c2.Bottom > client.Bottom)
                alwaysInside = false;
        }
Check(alwaysInside, "crop always stays inside the source and non-empty");
Check(ViewGeometry.ApplyCrop(default, new CropFraction(0.1, 0.1, 0.1, 0.1)).IsEmpty, "empty source stays empty");

// ================================================================ Hotkeys
Console.WriteLine("Hotkey");
var ctrlAltE = new Hotkey(true, true, false, false, 0x45);
Check(ctrlAltE.DisplayName == "Ctrl+Alt+E", $"display name -> {ctrlAltE.DisplayName}");
Check(ctrlAltE.Modifiers == (0x2u | 0x1u), "modifier flags match RegisterHotKey (Alt=1, Ctrl=2)");
Check(new Hotkey(true, true, true, true, 0x70).Modifiers == 0xFu, "all four modifiers -> 0xF");
Check(new Hotkey(false, false, true, false, 0x7B).DisplayName == "Shift+F12", "function key name");
Check(new Hotkey(true, false, false, false, 0x26).DisplayName == "Ctrl+Up", "arrow key name");
Check(new Hotkey(true, false, false, false, 0x62).DisplayName == "Ctrl+Num2", "numpad key name");
Check(ctrlAltE.TryValidate(out _), "Ctrl+Alt+E is valid");
Check(!new Hotkey(false, false, false, false, 0x45).TryValidate(out var e1) && e1.Contains("Ctrl"), "a bare key is rejected (would hijack it in the game)");
Check(!new Hotkey(true, false, false, false, 0x09).TryValidate(out _), "Tab is not a usable hotkey key");
Check(!new Hotkey(true, false, false, false, 0x1B).TryValidate(out _), "Esc is not a usable hotkey key");
Check(!new Hotkey(false, true, false, false, 0x73).TryValidate(out var e2) && e2.Contains("Alt+F4"), "Alt+F4 is rejected");
Check(new Hotkey(true, true, false, false, 0x73).TryValidate(out _), "Ctrl+Alt+F4 is fine");
bool allSupportedHaveNames = true;
for (int vk = 0; vk < 256; vk++)
    if (Hotkey.IsSupportedKey(vk) && new Hotkey(true, false, false, false, vk).DisplayName.Contains("0x")) allSupportedHaveNames = false;
Check(allSupportedHaveNames, "every supported key has a readable name");

Console.WriteLine("HotkeySettings");
Check(new HotkeySettings().TryValidate(out _), "defaults are valid");
var dup = new HotkeySettings { Interact = new Hotkey(true, true, false, false, 0x45) };
Check(!dup.TryValidate(out var e3) && e3.Contains("same keys"), $"duplicate hotkeys rejected -> {e3}");
var bad = new HotkeySettings { HideAll = new Hotkey(false, false, false, false, 0x48) };
Check(!bad.TryValidate(out var e4) && e4.Contains("Hide"), $"invalid hotkey names which one is wrong -> {e4}");

Console.WriteLine("SettingsStore");
string tmp = Path.Combine(Path.GetTempPath(), "imgdisp-test-" + Guid.NewGuid().ToString("N"));
string file = Path.Combine(tmp, "settings.json");
try
{
    Check(SettingsStore.Load(file).Hotkeys.EditMode == ctrlAltE, "missing file gives defaults");

    var custom = new AppSettings();
    custom.Hotkeys.EditMode = new Hotkey(true, false, true, false, 0x71);   // Ctrl+Shift+F2
    custom.Hotkeys.Interact = new Hotkey(true, false, true, false, 0x72);
    custom.Hotkeys.HideAll = new Hotkey(true, false, true, false, 0x73);
    Check(SettingsStore.Save(custom, file), "save creates the folder and file");
    var back = SettingsStore.Load(file);
    Check(back.Hotkeys.EditMode == custom.Hotkeys.EditMode && back.Hotkeys.Interact == custom.Hotkeys.Interact
          && back.Hotkeys.HideAll == custom.Hotkeys.HideAll, "settings round-trip through the file");
    Check(back.Hotkeys.EditMode.DisplayName == "Ctrl+Shift+F2", "round-tripped hotkey keeps its display name");
    Check(Directory.GetFiles(tmp, "*.tmp").Length == 0, "no temp file left behind");

    File.WriteAllText(file, "{ this is not json");
    Check(SettingsStore.Load(file).Hotkeys.EditMode == ctrlAltE, "corrupt file falls back to defaults");
    File.WriteAllText(file, "{}");
    Check(SettingsStore.Load(file).Hotkeys.EditMode == ctrlAltE, "empty object falls back to defaults");
    File.WriteAllText(file, "{\"Hotkeys\":{\"EditMode\":{\"Ctrl\":true,\"Alt\":false,\"Shift\":false,\"Win\":false,\"Key\":69},"
                          + "\"Interact\":{\"Ctrl\":true,\"Alt\":false,\"Shift\":false,\"Win\":false,\"Key\":69},"
                          + "\"HideAll\":{\"Ctrl\":true,\"Alt\":false,\"Shift\":false,\"Win\":false,\"Key\":72}}}");
    Check(SettingsStore.Load(file).Hotkeys.Interact == new Hotkey(true, true, false, false, 0x49), "file with duplicate hotkeys is ignored, defaults used");
    File.WriteAllText(file, "{\"Hotkeys\":null}");
    Check(SettingsStore.Load(file).Hotkeys.EditMode == ctrlAltE, "null hotkeys section falls back to defaults");

    // two copies saving at once must never leave a half-written file
    var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
    {
        for (int n = 0; n < 25; n++) SettingsStore.Save(custom, file);
    })).ToArray();
    Task.WaitAll(tasks);
    Check(SettingsStore.Load(file).Hotkeys.EditMode == custom.Hotkeys.EditMode, "concurrent saves leave a readable file");
}
finally
{
    try { Directory.Delete(tmp, true); } catch { }
}
Check(!SettingsStore.Save(new AppSettings(), Path.Combine("/proc/nonexistent-dir-xyz", "s.json")), "save to an unwritable place fails cleanly (returns false)");

Console.WriteLine("AppCommand");
Check(((int)AppCommand.SetEditMode, (int)AppCommand.SetHidden, (int)AppCommand.SetInteract, (int)AppCommand.ReloadSettings, (int)AppCommand.SuspendHotkeys) == (1, 2, 3, 4, 5),
      "command numbers are stable (copies of the app must agree on them)");

Console.WriteLine();
Console.WriteLine($"{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
