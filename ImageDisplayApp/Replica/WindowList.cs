// SPDX-License-Identifier: MS-RL
// Portions Copyright (c) Lorenz Cuno Klopfenstein 2010 (OnTopReplica, https://github.com/LorenzCK/OnTopReplica),
// licensed under the Microsoft Reciprocal License (MS-RL). This file follows that project's technique and structure
// and is therefore also under MS-RL: see LICENSE-OnTopReplica.txt.
// Modifications Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Diagnostics;
using System.Text;
using ImageDisplayApp.Native;

namespace ImageDisplayApp.Replica;

internal readonly record struct CloneableWindow(IntPtr Handle, string Title, string ProcessName);

internal static class WindowList
{
    /// <summary>Lists visible, titled, "normal" top-level windows (what the taskbar would show), sorted by title.</summary>
    /// <param name="exclude">Windows to leave out (this app's own windows).</param>
    public static List<CloneableWindow> Enumerate(params IntPtr[] exclude)
    {
        var skip = new HashSet<IntPtr>(exclude);
        var result = new List<CloneableWindow>();

        // Other running copies of this app have windows too; they are never useful to clone.
        string ownProcess = Process.GetCurrentProcess().ProcessName;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (skip.Contains(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            int length = NativeMethods.GetWindowTextLength(hwnd);
            if (length <= 0)
            {
                return true;
            }

            // Same rule as the taskbar: tool windows and owned windows are not listed unless they ask to be.
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            bool wantsTaskbar = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
            bool isOwned = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero;
            if ((isToolWindow || isOwned) && !wantsTaskbar)
            {
                return true;
            }

            // Windows of suspended/hidden UWP apps and other virtual-desktop leftovers are "cloaked".
            if (IsCloaked(hwnd))
            {
                return true;
            }

            string processName = GetProcessName(hwnd);
            if (string.Equals(processName, ownProcess, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hwnd, title, title.Capacity);
            result.Add(new CloneableWindow(hwnd, title.ToString(), processName));
            return true;
        }, IntPtr.Zero);

        result.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        return NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0;
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
