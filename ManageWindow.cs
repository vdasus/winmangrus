using System.Runtime.InteropServices;
using static Winmangrus.Native;

namespace Winmangrus;

/// <summary>"Manage profiles" window: pick a profile, check windows, save.</summary>
static unsafe class ManageWindow
{
    const int IdCombo = 101, IdDelete = 102, IdList = 103, IdSave = 104, IdSaveAll = 105, IdRefresh = 106, IdNew = 107, IdClose = 2 /* IDCANCEL, Esc */;

    static nint _hwnd, _font, _label, _combo, _new, _delete, _list, _save, _saveAll, _close, _refresh;
    static bool _registered;
    static List<LiveWindow> _windows = [];

    public static nint Handle => _hwnd;

    public static void Show()
    {
        if (_hwnd != 0)
        {
            ShowWindow(_hwnd, SW_RESTORE);
            SetForegroundWindow(_hwnd);
            return;
        }
        if (!_registered) _registered = RegisterClass("winmangrus.manage", &Proc, Program.Icon) != 0;

        _hwnd = CreateWindowExW(WS_EX_CONTROLPARENT, "winmangrus.manage", Program.About + " - profiles",
            WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, CW_USEDEFAULT, CW_USEDEFAULT, 0, 0, 0, 0, Instance, 0);

        _label = Child("STATIC", "Profile:", 0, 0);
        _combo = Child("COMBOBOX", null, CBS_DROPDOWN | CBS_AUTOHSCROLL | WS_VSCROLL | WS_TABSTOP, IdCombo);
        _new = Child("BUTTON", "New...", WS_TABSTOP, IdNew);
        _delete = Child("BUTTON", "Delete", WS_TABSTOP, IdDelete);
        _list = Child("SysListView32", null, LVS_REPORT | LVS_SINGLESEL | LVS_SHOWSELALWAYS | LVS_NOSORTHEADER | WS_TABSTOP, IdList, WS_EX_CLIENTEDGE);
        _save = Child("BUTTON", "Save checked", BS_DEFPUSHBUTTON | WS_TABSTOP, IdSave);
        _saveAll = Child("BUTTON", "Save all", WS_TABSTOP, IdSaveAll);
        _close = Child("BUTTON", "Close", WS_TABSTOP, IdClose);
        _refresh = Child("BUTTON", "Refresh", WS_TABSTOP, IdRefresh);

        SendMessageW(_list, LVM_SETEXTENDEDLISTVIEWSTYLE, 0, (nint)(LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER));
        AddColumn(0, "Program");
        AddColumn(1, "Title");

        uint dpi = GetDpiForWindow(_hwnd);
        ApplyFont(dpi);
        SetWindowPos(_hwnd, 0, 0, 0, (int)(680 * dpi / 96), (int)(460 * dpi / 96), SWP_NOMOVE | SWP_NOZORDER);
        FillProfiles("Default");
        FillWindows();
        CheckFromProfile("Default");

        ShowWindow(_hwnd, SW_SHOW);
        SetForegroundWindow(_hwnd);
    }

    static nint Child(string cls, string? text, uint style, int id, uint exStyle = 0) =>
        CreateWindowExW(exStyle, cls, text, WS_CHILD | WS_VISIBLE | style, 0, 0, 0, 0, _hwnd, id, Instance, 0);

    static void AddColumn(int index, string text)
    {
        fixed (char* p = text)
        {
            var col = new LVCOLUMNW { Mask = LVCF_TEXT | LVCF_WIDTH, Text = p, Cx = 100 };
            SendMessageW(_list, LVM_INSERTCOLUMNW, index, (nint)(&col));
        }
    }

    static void ApplyFont(uint dpi)
    {
        var ncm = new NONCLIENTMETRICSW { Size = (uint)sizeof(NONCLIENTMETRICSW) };
        SystemParametersInfoForDpi(0x29 /* SPI_GETNONCLIENTMETRICS */, ncm.Size, &ncm, 0, dpi);
        var font = CreateFontIndirectW(&ncm.MessageFont);
        foreach (var c in new[] { _label, _combo, _new, _delete, _list, _save, _saveAll, _close, _refresh })
            SendMessageW(c, WM_SETFONT, font, 1);
        if (_font != 0) DeleteObject(_font);
        _font = font;
    }

    static void DoLayout()
    {
        RECT rc;
        GetClientRect(_hwnd, &rc);
        double k = GetDpiForWindow(_hwnd) / 96.0;
        int S(int v) => (int)(v * k);
        int W = rc.Right, H = rc.Bottom, m = S(10), bh = S(26);

        MoveWindow(_label, m, m + S(5), S(50), S(20), 1);
        MoveWindow(_combo, m + S(55), m + S(1), S(260), S(240), 1);
        MoveWindow(_new, m + S(325), m, S(80), bh, 1);
        MoveWindow(_delete, m + S(410), m, S(80), bh, 1);
        int listTop = m + bh + S(8), by = H - m - bh;
        MoveWindow(_list, m, listTop, W - 2 * m, by - listTop - m, 1);
        MoveWindow(_refresh, m, by, S(90), bh, 1);
        MoveWindow(_close, W - m - S(90), by, S(90), bh, 1);
        MoveWindow(_saveAll, W - m - S(190), by, S(95), bh, 1);
        MoveWindow(_save, W - m - S(305), by, S(110), bh, 1);
        SendMessageW(_list, LVM_SETCOLUMNWIDTH, 0, S(160));
        SendMessageW(_list, LVM_SETCOLUMNWIDTH, 1, -2 /* LVSCW_AUTOSIZE_USEHEADER: fill the rest */);
    }

    static void FillProfiles(string select)
    {
        var names = Settings.Load().Profiles.Select(p => p.Name).Where(n => !n.Equals("Default", StringComparison.OrdinalIgnoreCase)).Prepend("Default").ToList();
        SendMessageW(_combo, CB_RESETCONTENT, 0, 0);
        foreach (var n in names)
            fixed (char* p = n) SendMessageW(_combo, CB_ADDSTRING, 0, (nint)p);
        SendMessageW(_combo, CB_SETCURSEL, Math.Max(0, names.FindIndex(n => n.Equals(select, StringComparison.OrdinalIgnoreCase))), 0);
    }

    static void FillWindows()
    {
        SendMessageW(_list, LVM_DELETEALLITEMS, 0, 0);
        _windows = Layout.Enumerate();
        for (int i = 0; i < _windows.Count; i++)
        {
            fixed (char* exe = _windows[i].Exe)
            fixed (char* title = _windows[i].Title)
            {
                var item = new LVITEMW { Mask = LVIF_TEXT, Item = i, Text = exe };
                SendMessageW(_list, LVM_INSERTITEMW, 0, (nint)(&item));
                item = new LVITEMW { SubItem = 1, Text = title };
                SendMessageW(_list, LVM_SETITEMTEXTW, i, (nint)(&item));
            }
        }
    }

    static bool IsChecked(int i) => ((SendMessageW(_list, LVM_GETITEMSTATE, i, (nint)LVIS_STATEIMAGEMASK) >> 12) & 0xF) == 2;

    static void SetChecked(int i, bool on)
    {
        var item = new LVITEMW { StateMask = LVIS_STATEIMAGEMASK, State = (on ? 2u : 1u) << 12 };
        SendMessageW(_list, LVM_SETITEMSTATE, i, (nint)(&item));
    }

    static void CheckFromProfile(string name)
    {
        var p = Settings.Load().Find(name);
        for (int i = 0; i < _windows.Count; i++)
        {
            var w = _windows[i];
            SetChecked(i, p?.Windows.Any(s => s.Exe.Equals(w.Exe, StringComparison.OrdinalIgnoreCase) && s.Class == w.Class) == true);
        }
    }

    static string SelectedProfile()
    {
        var name = Text(_combo).Trim();
        return name.Length == 0 ? "Default" : name;
    }

    static string ComboItem(int index)
    {
        char* buf = stackalloc char[512];
        int len = (int)SendMessageW(_combo, CB_GETLBTEXT, index, (nint)buf);
        return len > 0 ? new string(buf, 0, len) : "";
    }

    static void OnCommand(int id, int code)
    {
        switch (id)
        {
            case IdCombo when code == CBN_SELCHANGE:
                CheckFromProfile(ComboItem((int)SendMessageW(_combo, CB_GETCURSEL, 0, 0)));
                break;
            case IdRefresh:
                FillWindows();
                CheckFromProfile(SelectedProfile());
                break;
            case IdSave:
            case IdSaveAll:
                if (id == IdSaveAll) for (int i = 0; i < _windows.Count; i++) SetChecked(i, true);
                var name = SelectedProfile();
                var picked = _windows.Where((_, i) => IsChecked(i)).ToList();
                if (picked.Count == 0)
                {
                    MessageBoxW(_hwnd, "Check at least one window.", Program.Title, MB_OK | MB_ICONINFO);
                    break;
                }
                Layout.SaveProfile(name, picked);
                FillProfiles(name);
                MessageBoxW(_hwnd, $"Saved {picked.Count} window(s) to \"{name}\".", Program.Title, MB_OK | MB_ICONINFO);
                break;
            case IdNew:
                var newName = PromptName();
                if (string.IsNullOrEmpty(newName)) break;
                if (Settings.Load().Find(newName) is null)
                    fixed (char* pn = newName) SendMessageW(_combo, CB_SETCURSEL, SendMessageW(_combo, CB_ADDSTRING, 0, (nint)pn), 0);
                else FillProfiles(newName);
                break;
            case IdDelete:
                var s = Settings.Load();
                var p = s.Find(SelectedProfile());
                if (p is null || MessageBoxW(_hwnd, $"Delete profile \"{p.Name}\"?", Program.Title, MB_YESNO | MB_ICONQUESTION) != IDYES) break;
                s.Profiles.Remove(p);
                s.Save();
                FillProfiles("Default");
                CheckFromProfile("Default");
                break;
            case IdClose:
                DestroyWindow(_hwnd);
                break;
        }
    }

    static bool _promptRegistered, _promptDone;
    static nint _promptEdit;
    static string? _promptResult;

    /// <summary>Modal "profile name" input box; returns null on cancel.</summary>
    static string? PromptName()
    {
        const uint WS_POPUP = 0x80000000, WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_EX_DLGMODALFRAME = 1, ES_AUTOHSCROLL = 0x80;
        if (!_promptRegistered) _promptRegistered = RegisterClass("winmangrus.prompt", &PromptProc, Program.Icon) != 0;

        double k = GetDpiForWindow(_hwnd) / 96.0;
        int S(int v) => (int)(v * k);
        RECT owner;
        GetWindowRect(_hwnd, &owner);
        int w = S(340), h = S(150);
        var dlg = CreateWindowExW(WS_EX_DLGMODALFRAME | WS_EX_CONTROLPARENT, "winmangrus.prompt", "New profile", WS_POPUP | WS_CAPTION | WS_SYSMENU,
            (owner.Left + owner.Right - w) / 2, (owner.Top + owner.Bottom - h) / 2, w, h, _hwnd, 0, Instance, 0);
        nint Add(string cls, string? text, uint style, int id, int x, int y, int cx, int cy, uint ex = 0)
        {
            var c = CreateWindowExW(ex, cls, text, WS_CHILD | WS_VISIBLE | style, S(x), S(y), S(cx), S(cy), dlg, id, Instance, 0);
            SendMessageW(c, WM_SETFONT, _font, 1);
            return c;
        }
        Add("STATIC", "Profile name:", 0, 0, 12, 12, 300, 20);
        _promptEdit = Add("EDIT", null, ES_AUTOHSCROLL | WS_TABSTOP, 0, 12, 34, 300, 24, WS_EX_CLIENTEDGE);
        Add("BUTTON", "OK", BS_DEFPUSHBUTTON | WS_TABSTOP, 1 /* IDOK, Enter */, 132, 70, 85, 26);
        Add("BUTTON", "Cancel", WS_TABSTOP, 2 /* IDCANCEL, Esc */, 227, 70, 85, 26);

        EnableWindow(_hwnd, 0);
        ShowWindow(dlg, SW_SHOW);
        SetFocus(_promptEdit);
        _promptDone = false;
        _promptResult = null;
        MSG msg;
        while (!_promptDone)
        {
            if (GetMessageW(&msg, 0, 0, 0) <= 0) { PostQuitMessage(0); break; } // let the main loop see WM_QUIT
            if (IsDialogMessageW(dlg, &msg) != 0) continue;
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        EnableWindow(_hwnd, 1);
        DestroyWindow(dlg);
        SetForegroundWindow(_hwnd);
        return _promptResult?.Trim();
    }

    [UnmanagedCallersOnly]
    static nint PromptProc(nint h, uint m, nint w, nint l)
    {
        switch (m)
        {
            case WM_COMMAND when LoWord(w) is 1 or 2:
                if (LoWord(w) == 1) _promptResult = Text(_promptEdit);
                _promptDone = true;
                return 0;
            case WM_CLOSE:
                _promptDone = true;
                return 0;
        }
        return DefWindowProcW(h, m, w, l);
    }

    [UnmanagedCallersOnly]
    static nint Proc(nint h, uint m, nint w, nint l)
    {
        try
        {
            switch (m)
            {
                case WM_SIZE:
                    if (_list != 0) DoLayout();
                    return 0;
                case WM_COMMAND:
                    OnCommand(LoWord(w), HiWord(w));
                    return 0;
                case WM_GETMINMAXINFO:
                    uint dpi = GetDpiForWindow(h);
                    ((POINT*)l)[3] = new POINT { X = (int)(480 * dpi / 96), Y = (int)(260 * dpi / 96) }; // ptMinTrackSize
                    return 0;
                case WM_DPICHANGED:
                    ApplyFont((uint)HiWord(w));
                    var r = (RECT*)l;
                    SetWindowPos(h, 0, r->Left, r->Top, r->Right - r->Left, r->Bottom - r->Top, SWP_NOZORDER | SWP_NOACTIVATE);
                    return 0;
                case WM_CLOSE:
                    DestroyWindow(h);
                    return 0;
                case WM_DESTROY:
                    _hwnd = _list = 0;
                    DeleteObject(_font);
                    _font = 0;
                    return 0;
            }
        }
        catch (Exception ex)
        {
            MessageBoxW(h, ex.Message, Program.Title, MB_OK | MB_ICONERROR);
        }
        return DefWindowProcW(h, m, w, l);
    }
}
