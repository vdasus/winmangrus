using System.Runtime.InteropServices;
using static Winmangrus.Native;

namespace Winmangrus;

sealed record LiveWindow(nint Handle, string Exe, string Class, string Title);

/// <summary>Enumerates top-level windows, captures their positions and restores them.</summary>
static unsafe class Layout
{
    static List<LiveWindow>? _windows;
    static List<RECT>? _monitors;

    public static List<LiveWindow> Enumerate()
    {
        _windows = [];
        EnumWindows((nint)(delegate* unmanaged<nint, nint, int>)&EnumWindowProc, 0);
        var list = _windows;
        _windows = null;
        list.Sort((a, b) => string.Compare(a.Exe + a.Title, b.Exe + b.Title, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    [UnmanagedCallersOnly]
    static int EnumWindowProc(nint h, nint _)
    {
        try { if (Describe(h) is { } w) _windows!.Add(w); }
        catch { /* window vanished mid-query */ }
        return 1;
    }

    static LiveWindow? Describe(nint h)
    {
        const uint GW_OWNER = 4, DWMWA_CLOAKED = 14;
        if (IsWindowVisible(h) == 0 || GetWindow(h, GW_OWNER) != 0 || IsHungAppWindow(h) != 0) return null;
        if ((GetWindowLongPtrW(h, -20 /* GWL_EXSTYLE */) & WS_EX_TOOLWINDOW) != 0) return null;
        int cloaked = 0;
        if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, &cloaked, 4) == 0 && cloaked != 0 && !IsOnOtherDesktop(h, cloaked)) return null;

        char* buf = stackalloc char[512];
        // InternalGetWindowText does not send WM_GETTEXT, so a busy app cannot block us.
        var title = new string(buf, 0, InternalGetWindowText(h, buf, 512));
        if (title.Length == 0) return null;
        var cls = new string(buf, 0, GetClassNameW(h, buf, 256));
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return null;

        uint pid;
        GetWindowThreadProcessId(h, &pid);
        if (pid == (uint)Environment.ProcessId) return null;
        return new LiveWindow(h, ExeName(pid), cls, title);
    }

    static nint _desktopManager;

    /// <summary>
    /// Windows on another virtual desktop are cloaked by the shell. Tell them apart from other cloaked windows
    /// (suspended UWP frames and similar) via IVirtualDesktopManager: they have a desktop id that is not the current one.
    /// </summary>
    static bool IsOnOtherDesktop(nint h, int cloaked)
    {
        const int DWM_CLOAKED_SHELL = 2;
        if (cloaked != DWM_CLOAKED_SHELL) return false;
        if (_desktopManager == 0)
        {
            var clsid = new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"); // CLSID_VirtualDesktopManager
            var iid = new Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b");   // IID_IVirtualDesktopManager
            nint vdm;
            if (CoCreateInstance(&clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, &iid, &vdm) != 0) return false;
            _desktopManager = vdm;
        }
        // vtable: IUnknown (3), IsWindowOnCurrentVirtualDesktop, GetWindowDesktopId, MoveWindowToDesktop
        var vtbl = *(nint**)_desktopManager;
        int onCurrent;
        Guid id;
        return ((delegate* unmanaged<nint, nint, int*, int>)vtbl[3])(_desktopManager, h, &onCurrent) == 0 && onCurrent == 0
            && ((delegate* unmanaged<nint, nint, Guid*, int>)vtbl[4])(_desktopManager, h, &id) == 0 && id != Guid.Empty;
    }

    static string ExeName(uint pid)
    {
        nint process = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, 0, pid);
        if (process == 0) return "?";
        char* buf = stackalloc char[1024];
        uint size = 1024;
        var ok = QueryFullProcessImageNameW(process, 0, buf, &size) != 0;
        CloseHandle(process);
        return ok ? Path.GetFileName(new string(buf, 0, (int)size)) : "?";
    }

    public static string MonitorSignature()
    {
        _monitors = [];
        EnumDisplayMonitors(0, null, (nint)(delegate* unmanaged<nint, nint, RECT*, nint, int>)&MonitorProc, 0);
        var sig = string.Join(";", _monitors.OrderBy(r => r.Left).ThenBy(r => r.Top)
            .Select(r => $"{r.Left},{r.Top},{r.Right - r.Left}x{r.Bottom - r.Top}"));
        _monitors = null;
        return sig;
    }

    [UnmanagedCallersOnly]
    static int MonitorProc(nint monitor, nint hdc, RECT* rect, nint _)
    {
        _monitors!.Add(*rect);
        return 1;
    }

    static SavedWindow Capture(LiveWindow w)
    {
        var h = w.Handle;
        RECT r;
        var state = IsZoomed(h) != 0 ? WindowState.Maximized : IsIconic(h) != 0 ? WindowState.Minimized : WindowState.Normal;
        if (state == WindowState.Normal)
        {
            GetWindowRect(h, &r); // screen coordinates, also correct for snapped windows
        }
        else
        {
            var wp = new WINDOWPLACEMENT { Length = sizeof(WINDOWPLACEMENT) };
            GetWindowPlacement(h, &wp);
            r = wp.Normal; // workspace coordinates: shift by the monitor's work-area offset
            var mi = new MONITORINFO { Size = sizeof(MONITORINFO) };
            GetMonitorInfoW(MonitorFromRect(&r, 2 /* MONITOR_DEFAULTTONEAREST */), &mi);
            int dx = mi.Work.Left - mi.Monitor.Left, dy = mi.Work.Top - mi.Monitor.Top;
            r = new RECT { Left = r.Left + dx, Top = r.Top + dy, Right = r.Right + dx, Bottom = r.Bottom + dy };
        }
        return new SavedWindow
        {
            Exe = w.Exe, Class = w.Class, Title = w.Title, State = state,
            Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom,
        };
    }

    public static void SaveProfile(string name, IEnumerable<LiveWindow> windows)
    {
        var s = Settings.Load();
        var p = s.Find(name) ?? new Profile { Name = name };
        s.Profiles.Remove(p);
        p.Monitors = MonitorSignature();
        p.Windows = windows.Select(Capture).ToList();
        s.Profiles.Add(p); // last saved = preferred for auto-restore
        s.Save();
    }

    /// <summary>Most recently saved profile whose monitor layout matches the current one.</summary>
    public static Profile? AutoProfile(Settings s)
    {
        var sig = MonitorSignature();
        return s.Profiles.LastOrDefault(p => p.Monitors == sig);
    }

    public static bool Restore(string name)
    {
        var p = Settings.Load().Find(name);
        if (p is null) return false;
        Restore(p);
        return true;
    }

    public static void Restore(Profile p)
    {
        var live = Enumerate();
        var used = new HashSet<nint>();
        var pending = new List<SavedWindow>(p.Windows);

        // Pass 1: exact title match; pass 2: same program and window class, any title.
        foreach (var exactTitle in new[] { true, false })
            foreach (var s in pending.ToList())
            {
                var w = live.Find(w => !used.Contains(w.Handle) && w.Exe.Equals(s.Exe, StringComparison.OrdinalIgnoreCase)
                                       && w.Class == s.Class && (!exactTitle || w.Title == s.Title));
                if (w is null) continue;
                used.Add(w.Handle);
                pending.Remove(s);
                Apply(w.Handle, s);
            }
    }

    static void Apply(nint h, SavedWindow s)
    {
        // Un-minimize / un-maximize first so the window can be moved to its saved monitor.
        // SetWindowPlacement with SW_SHOWNOACTIVATE instead of SW_RESTORE: activation would switch virtual desktops.
        if (IsIconic(h) != 0 || IsZoomed(h) != 0)
        {
            var wp = new WINDOWPLACEMENT { Length = sizeof(WINDOWPLACEMENT) };
            GetWindowPlacement(h, &wp);
            wp.Flags = 0; // drop WPF_RESTORETOMAXIMIZED
            wp.ShowCmd = 4; // SW_SHOWNOACTIVATE
            SetWindowPlacement(h, &wp);
        }
        // Twice: moving to a monitor with another DPI makes the app rescale itself after the first call.
        for (int i = 0; i < 2; i++)
            SetWindowPos(h, 0, s.Left, s.Top, s.Right - s.Left, s.Bottom - s.Top, SWP_NOZORDER | SWP_NOACTIVATE);
        if (s.State == WindowState.Maximized) ShowWindow(h, SW_MAXIMIZE);
        else if (s.State == WindowState.Minimized) ShowWindow(h, SW_SHOWMINNOACTIVE);
    }
}
