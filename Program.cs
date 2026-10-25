using System.Runtime.InteropServices;
using static Winmangrus.Native;

namespace Winmangrus;

static unsafe class Program
{
    const uint WM_TRAY = WM_APP + 1;
    const int IdManage = 1, IdAutoRestore = 2, IdStartup = 3, IdExit = 4, IdReleases = 5, IdHotkeyBase = 100, IdProfileBase = 1000;
    const int MaxDesktops = 9;
    const nint TimerAutoRestore = 1;
    public const string Title = "winmangrus";
    const string ReleasesUrl = "https://github.com/vdasus/winmangrus/releases";
    public static readonly string Version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "?";
    public static readonly string About = $"{Title} {Version} by vdasus";

    const string Help = """
        Saves window positions into profiles and restores them.
        Without switches, runs in the system tray.

        Switches (prefix - or /):
          -saveall            Save all open windows to the "Default" profile
          -restore            Restore the "Default" profile
          -restore=<Name>     Restore profile <Name> (quote names with spaces)
          -autoexit           Exit after processing switches instead of staying in the tray
          -h, --help          Show this help

        Exit code: 1 if a requested profile does not exist, 2 on an unknown switch.
        Example: winmangrus.exe -restore="Home layout" -autoexit
        """;

    static nint _hwnd;
    static uint _taskbarCreated;
    static readonly HashSet<int> _hotkeys = []; // desktop numbers whose Ctrl+N is registered
    public static nint Icon { get; private set; }

    static int Main(string[] args)
    {
        SetProcessDpiAwarenessContext(-4); // PerMonitorV2, in case the manifest is ignored
        CoInitializeEx(0, 2 /* COINIT_APARTMENTTHREADED */); // IVirtualDesktopManager

        var unknown = args.FirstOrDefault(a => !IsKnownSwitch(a.TrimStart('-', '/')));
        if (unknown != null)
        {
            bool isHelp = unknown.TrimStart('-', '/') is "h" or "H" or "?" || unknown.TrimStart('-', '/').Equals("help", StringComparison.OrdinalIgnoreCase);
            MessageBoxW(0, (isHelp ? "" : $"Unknown switch: {unknown}\n\n") + Help, About, MB_OK | (isHelp ? MB_ICONINFO : MB_ICONERROR));
            return isHelp ? 0 : 2;
        }

        bool autoExit = false;
        int exitCode = 0;
        foreach (var raw in args)
        {
            var arg = raw.TrimStart('-', '/');
            if (arg.Equals("saveall", StringComparison.OrdinalIgnoreCase)) Layout.SaveProfile("Default", Layout.Enumerate());
            else if (arg.Equals("restore", StringComparison.OrdinalIgnoreCase)) exitCode |= Layout.Restore("Default") ? 0 : 1;
            else if (arg.StartsWith("restore=", StringComparison.OrdinalIgnoreCase)) exitCode |= Layout.Restore(arg[8..].Trim('"')) ? 0 : 1;
            else if (arg.Equals("autoexit", StringComparison.OrdinalIgnoreCase)) autoExit = true;
        }
        if (autoExit) return exitCode;

        using var mutex = new Mutex(true, "winmangrus-single-instance", out bool firstInstance);
        if (!firstInstance) return exitCode;

        long icc = 8 | (1L /* ICC_LISTVIEW_CLASSES */ << 32); // INITCOMMONCONTROLSEX { dwSize = 8, dwICC }
        InitCommonControlsEx(&icc);
        Icon = LoadTrayIcon();
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");

        RegisterClass("winmangrus.main", &MainProc, Icon);
        // Hidden top-level window (not message-only) so it receives WM_DISPLAYCHANGE and WM_POWERBROADCAST.
        _hwnd = CreateWindowExW(0, "winmangrus.main", Title, 0, 0, 0, 0, 0, 0, 0, Instance, 0);
        AddTrayIcon();
        RegisterSavedHotkeys();

        MSG msg;
        while (GetMessageW(&msg, 0, 0, 0) > 0)
        {
            if (ManageWindow.Handle != 0 && IsDialogMessageW(ManageWindow.Handle, &msg) != 0) continue;
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return exitCode;
    }

    static bool IsKnownSwitch(string a) =>
        a.Equals("saveall", StringComparison.OrdinalIgnoreCase) || a.Equals("restore", StringComparison.OrdinalIgnoreCase)
        || a.StartsWith("restore=", StringComparison.OrdinalIgnoreCase) || a.Equals("autoexit", StringComparison.OrdinalIgnoreCase);

    static nint LoadTrayIcon()
    {
        uint dpi = GetDpiForSystem();
        int size = GetSystemMetricsForDpi(49 /* SM_CXSMICON */, dpi);
        // 32512 is the icon group id the SDK assigns to <ApplicationIcon>.
        var icon = LoadImageW(Instance, 32512, 1 /* IMAGE_ICON */, size, size, 0);
        if (icon == 0) ExtractIconExW(Environment.ProcessPath!, 0, null, &icon, 1);
        return icon;
    }

    static void AddTrayIcon()
    {
        var nid = new NOTIFYICONDATAW
        {
            Size = (uint)sizeof(NOTIFYICONDATAW), Hwnd = _hwnd, Id = 1,
            Flags = NIF_MESSAGE | NIF_ICON | NIF_TIP, CallbackMessage = WM_TRAY, Icon = Icon,
        };
        About.AsSpan().CopyTo(new Span<char>(nid.Tip, 127));
        Shell_NotifyIconW(NIM_ADD, &nid);
    }

    static void RemoveTrayIcon()
    {
        var nid = new NOTIFYICONDATAW { Size = (uint)sizeof(NOTIFYICONDATAW), Hwnd = _hwnd, Id = 1 };
        Shell_NotifyIconW(NIM_DELETE, &nid);
    }

    [UnmanagedCallersOnly]
    static nint MainProc(nint h, uint m, nint w, nint l)
    {
        try
        {
            switch (m)
            {
                case WM_TRAY when (uint)l == WM_LBUTTONUP:
                    ManageWindow.Show();
                    return 0;
                case WM_TRAY when (uint)l == WM_RBUTTONUP:
                    ShowMenu(h);
                    return 0;
                case WM_DISPLAYCHANGE:
                    SetTimer(h, TimerAutoRestore, 3000, 0); // debounce: several changes arrive in a burst
                    return 0;
                case WM_POWERBROADCAST when w == PBT_APMRESUMEAUTOMATIC:
                    SetTimer(h, TimerAutoRestore, 5000, 0);
                    return 1;
                case WM_HOTKEY:
                    GoToDesktopNumber((int)w - 1); // hotkey id is the 1-based desktop number
                    return 0;
                case WM_TIMER when w == TimerAutoRestore:
                    KillTimer(h, TimerAutoRestore);
                    AutoRestore();
                    return 0;
                case WM_DESTROY:
                    RemoveTrayIcon();
                    PostQuitMessage(0);
                    return 0;
            }
            if (m == _taskbarCreated && m != 0) AddTrayIcon(); // Explorer restarted
        }
        catch (Exception ex)
        {
            MessageBoxW(0, ex.Message, Title, MB_OK | MB_ICONERROR);
        }
        return DefWindowProcW(h, m, w, l);
    }

    static bool RegisterDesktopHotkey(int n) =>
        RegisterHotKey(_hwnd, n, MOD_CONTROL | MOD_NOREPEAT, (uint)('0' + n)) != 0 && _hotkeys.Add(n);

    static void RegisterSavedHotkeys()
    {
        var failed = Settings.Load().DesktopHotkeys.Where(n => n is >= 1 and <= MaxDesktops && !RegisterDesktopHotkey(n)).ToList();
        if (failed.Count > 0)
            MessageBoxW(0, $"Already in use by another program: {string.Join(", ", failed.Select(n => $"Ctrl+{n}"))}", About, MB_OK | MB_ICONERROR);
    }

    static void ToggleDesktopHotkey(Settings s, int n)
    {
        if (_hotkeys.Remove(n))
        {
            UnregisterHotKey(_hwnd, n);
            s.DesktopHotkeys.Remove(n);
        }
        else
        {
            try
            {
                GetDesktopCount(); // fail now rather than on the first key press
            }
            catch (DllNotFoundException)
            {
                MessageBoxW(0, "VirtualDesktopAccessor.dll not found next to winmangrus.exe.", About, MB_OK | MB_ICONERROR);
                return;
            }
            if (!RegisterDesktopHotkey(n))
            {
                MessageBoxW(0, $"Ctrl+{n} is already in use by another program.", About, MB_OK | MB_ICONERROR);
                return;
            }
            if (!s.DesktopHotkeys.Contains(n)) s.DesktopHotkeys.Add(n);
        }
        s.Save();
    }

    static void AutoRestore()
    {
        var s = Settings.Load();
        if (s.AutoRestore && Layout.AutoProfile(s) is { } p) Layout.Restore(p);
    }

    static void ShowMenu(nint h)
    {
        var s = Settings.Load();
        var auto = Layout.AutoProfile(s);
        nint menu = CreatePopupMenu();
        int cmd;
        try
        {
            AppendMenuW(menu, MF_GRAYED, 0, About);
            AppendMenuW(menu, MF_SEPARATOR, 0, null);

            for (int i = 0; i < s.Profiles.Count; i++)
            {
                AppendMenuW(menu, MF_STRING, IdProfileBase + i, "Restore: " + s.Profiles[i].Name.Replace("&", "&&"));
                if (s.Profiles[i] == auto) SetMenuDefaultItem(menu, (uint)(IdProfileBase + i), 0); // bold = matches current monitors
            }
            if (s.Profiles.Count == 0) AppendMenuW(menu, MF_GRAYED, 0, "No saved profiles");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, IdManage, "Manage profiles...");
            AppendMenuW(menu, s.AutoRestore ? MF_CHECKED : MF_STRING, IdAutoRestore, "Auto-restore on display change / wake");
            AppendMenuW(menu, Startup.Enabled ? MF_CHECKED : MF_STRING, IdStartup, "Start with Windows");
            nint hotkeys = CreatePopupMenu(); // destroyed together with the parent menu
            for (int n = 1; n <= MaxDesktops; n++)
                AppendMenuW(hotkeys, _hotkeys.Contains(n) ? MF_CHECKED : MF_STRING, IdHotkeyBase + n, $"Ctrl+{n}: desktop {n}");
            AppendMenuW(menu, MF_POPUP, hotkeys, "Desktop hotkeys");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, IdReleases, "Releases page...");
            AppendMenuW(menu, MF_STRING, IdExit, "Exit");

            POINT pt;
            GetCursorPos(&pt);
            SetForegroundWindow(h); // required, otherwise the menu does not close on outside click
            cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, pt.X, pt.Y, h, 0);
            PostMessageW(h, 0, 0, 0);
        }
        finally
        {
            DestroyMenu(menu);
        }

        switch (cmd)
        {
            case >= IdProfileBase: Layout.Restore(s.Profiles[cmd - IdProfileBase]); break;
            case > IdHotkeyBase and <= IdHotkeyBase + MaxDesktops: ToggleDesktopHotkey(s, cmd - IdHotkeyBase); break;
            case IdManage: ManageWindow.Show(); break;
            case IdAutoRestore: s.AutoRestore = !s.AutoRestore; s.Save(); break;
            case IdStartup: Startup.Enabled = !Startup.Enabled; break;
            case IdReleases: ShellExecuteW(0, null, ReleasesUrl, null, null, SW_SHOW); break;
            case IdExit: DestroyWindow(h); break;
        }
    }
}
