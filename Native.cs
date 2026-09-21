using System.Runtime.InteropServices;

namespace Winmangrus;

[StructLayout(LayoutKind.Sequential)]
struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
struct WINDOWPLACEMENT { public int Length, Flags, ShowCmd; public POINT MinPosition, MaxPosition; public RECT Normal; }

[StructLayout(LayoutKind.Sequential)]
struct MONITORINFO { public int Size; public RECT Monitor, Work; public uint Flags; }

[StructLayout(LayoutKind.Sequential)]
struct MSG { public nint Hwnd; public uint Message; public nint WParam, LParam; public uint Time; public POINT Pt; public uint Private; }

[StructLayout(LayoutKind.Sequential)]
unsafe struct WNDCLASSEXW
{
    public uint Size, Style;
    public nint WndProc;
    public int ClsExtra, WndExtra;
    public nint Instance, Icon, Cursor, Background;
    public char* MenuName, ClassName;
    public nint IconSmall;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct NOTIFYICONDATAW
{
    public uint Size;
    public nint Hwnd;
    public uint Id, Flags, CallbackMessage;
    public nint Icon;
    public fixed char Tip[128];
    public uint State, StateMask;
    public fixed char Info[256];
    public uint Version;
    public fixed char InfoTitle[64];
    public uint InfoFlags;
    public Guid GuidItem;
    public nint BalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct LVCOLUMNW { public uint Mask; public int Fmt, Cx; public char* Text; public int TextMax, SubItem; }

[StructLayout(LayoutKind.Sequential)]
unsafe struct LVITEMW
{
    public uint Mask;
    public int Item, SubItem;
    public uint State, StateMask;
    public char* Text;
    public int TextMax, Image;
    public nint LParam;
    public int Indent, GroupId;
    public uint Columns;
    public nint PuColumns, PiColFmt;
    public int Group;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct LOGFONTW
{
    public int Height, Width, Escapement, Orientation, Weight;
    public byte Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
    public fixed char FaceName[32];
}

[StructLayout(LayoutKind.Sequential)]
struct NONCLIENTMETRICSW
{
    public uint Size;
    public int BorderWidth, ScrollWidth, ScrollHeight, CaptionWidth, CaptionHeight;
    public LOGFONTW CaptionFont;
    public int SmCaptionWidth, SmCaptionHeight;
    public LOGFONTW SmCaptionFont;
    public int MenuWidth, MenuHeight;
    public LOGFONTW MenuFont, StatusFont, MessageFont;
    public int PaddedBorderWidth;
}

static unsafe partial class Native
{
    // Messages
    public const uint WM_DESTROY = 0x0002, WM_SIZE = 0x0005, WM_CLOSE = 0x0010, WM_GETMINMAXINFO = 0x0024,
        WM_SETFONT = 0x0030, WM_DISPLAYCHANGE = 0x007E, WM_COMMAND = 0x0111, WM_TIMER = 0x0113,
        WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_POWERBROADCAST = 0x0218, WM_DPICHANGED = 0x02E0, WM_APP = 0x8000;
    public const nint PBT_APMRESUMEAUTOMATIC = 0x12;

    // Styles
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000, WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000,
        WS_TABSTOP = 0x00010000, WS_CLIPCHILDREN = 0x02000000, WS_VSCROLL = 0x00200000;
    public const uint WS_EX_TOOLWINDOW = 0x80, WS_EX_CLIENTEDGE = 0x200, WS_EX_CONTROLPARENT = 0x10000;
    public const uint BS_DEFPUSHBUTTON = 1, CBS_DROPDOWN = 2, CBS_AUTOHSCROLL = 0x40;
    public const uint LVS_REPORT = 1, LVS_SINGLESEL = 4, LVS_SHOWSELALWAYS = 8, LVS_NOSORTHEADER = 0x8000;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);

    // ShowWindow / SetWindowPos
    public const int SW_SHOW = 5, SW_MAXIMIZE = 3, SW_SHOWMINNOACTIVE = 7, SW_RESTORE = 9;
    public const uint SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;

    // Menus
    public const uint MF_STRING = 0, MF_GRAYED = 1, MF_CHECKED = 8, MF_SEPARATOR = 0x800;
    public const uint TPM_RIGHTBUTTON = 2, TPM_NONOTIFY = 0x80, TPM_RETURNCMD = 0x100;

    // Combo box / list view
    public const uint CB_ADDSTRING = 0x143, CB_GETCURSEL = 0x147, CB_GETLBTEXT = 0x148, CB_RESETCONTENT = 0x14B, CB_SETCURSEL = 0x14E;
    public const int CBN_SELCHANGE = 1;
    public const uint LVM_DELETEALLITEMS = 0x1009, LVM_SETCOLUMNWIDTH = 0x101E, LVM_SETITEMSTATE = 0x102B, LVM_GETITEMSTATE = 0x102C,
        LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1036, LVM_INSERTITEMW = 0x104D, LVM_INSERTCOLUMNW = 0x1061, LVM_SETITEMTEXTW = 0x1074;
    public const uint LVS_EX_CHECKBOXES = 4, LVS_EX_FULLROWSELECT = 0x20, LVS_EX_DOUBLEBUFFER = 0x10000;
    public const uint LVIF_TEXT = 1, LVCF_WIDTH = 2, LVCF_TEXT = 4, LVIS_STATEIMAGEMASK = 0xF000;

    // Tray
    public const uint NIM_ADD = 0, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    // MessageBox
    public const uint MB_OK = 0, MB_YESNO = 4, MB_ICONERROR = 0x10, MB_ICONQUESTION = 0x20, MB_ICONINFO = 0x40;
    public const int IDYES = 6;

    public static int LoWord(nint v) => (int)(v & 0xFFFF);
    public static int HiWord(nint v) => (int)((v >> 16) & 0xFFFF);

    // user32
    [LibraryImport("user32.dll")] public static partial ushort RegisterClassExW(WNDCLASSEXW* wc);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(uint exStyle, string cls, string? name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [LibraryImport("user32.dll")] public static partial nint DefWindowProcW(nint h, uint m, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial int GetMessageW(MSG* msg, nint h, uint min, uint max);
    [LibraryImport("user32.dll")] public static partial int TranslateMessage(MSG* msg);
    [LibraryImport("user32.dll")] public static partial nint DispatchMessageW(MSG* msg);
    [LibraryImport("user32.dll")] public static partial int IsDialogMessageW(nint h, MSG* msg);
    [LibraryImport("user32.dll")] public static partial void PostQuitMessage(int code);
    [LibraryImport("user32.dll")] public static partial int PostMessageW(nint h, uint m, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial nint SendMessageW(nint h, uint m, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial int DestroyWindow(nint h);
    [LibraryImport("user32.dll")] public static partial int ShowWindow(nint h, int cmd);
    [LibraryImport("user32.dll")] public static partial int SetForegroundWindow(nint h);
    [LibraryImport("user32.dll")] public static partial int SetWindowPos(nint h, nint after, int x, int y, int w, int cy, uint flags);
    [LibraryImport("user32.dll")] public static partial int MoveWindow(nint h, int x, int y, int w, int cy, int repaint);
    [LibraryImport("user32.dll")] public static partial int GetClientRect(nint h, RECT* r);
    [LibraryImport("user32.dll")] public static partial int GetWindowRect(nint h, RECT* r);
    [LibraryImport("user32.dll")] public static partial int GetWindowPlacement(nint h, WINDOWPLACEMENT* wp);
    [LibraryImport("user32.dll")] public static partial int SetWindowPlacement(nint h, WINDOWPLACEMENT* wp);
    [LibraryImport("user32.dll")] public static partial int IsIconic(nint h);
    [LibraryImport("user32.dll")] public static partial int IsZoomed(nint h);
    [LibraryImport("user32.dll")] public static partial int IsWindowVisible(nint h);
    [LibraryImport("user32.dll")] public static partial int IsHungAppWindow(nint h);
    [LibraryImport("user32.dll")] public static partial nint GetWindow(nint h, uint cmd);
    [LibraryImport("user32.dll")] public static partial nint GetWindowLongPtrW(nint h, int index);
    [LibraryImport("user32.dll")] public static partial uint GetWindowThreadProcessId(nint h, uint* pid);
    [LibraryImport("user32.dll")] public static partial int InternalGetWindowText(nint h, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial int GetWindowTextW(nint h, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial int GetClassNameW(nint h, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial int EnumWindows(nint proc, nint param);
    [LibraryImport("user32.dll")] public static partial int EnumDisplayMonitors(nint hdc, RECT* clip, nint proc, nint param);
    [LibraryImport("user32.dll")] public static partial nint MonitorFromRect(RECT* r, uint flags);
    [LibraryImport("user32.dll")] public static partial int GetMonitorInfoW(nint mon, MONITORINFO* mi);
    [LibraryImport("user32.dll")] public static partial nint CreatePopupMenu();
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int AppendMenuW(nint menu, uint flags, nint id, string? text);
    [LibraryImport("user32.dll")] public static partial int SetMenuDefaultItem(nint menu, uint item, uint byPos);
    [LibraryImport("user32.dll")] public static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint h, nint tpm);
    [LibraryImport("user32.dll")] public static partial int DestroyMenu(nint menu);
    [LibraryImport("user32.dll")] public static partial int GetCursorPos(POINT* pt);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBoxW(nint h, string text, string caption, uint type);
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessageW(string name);
    [LibraryImport("user32.dll")] public static partial nint SetTimer(nint h, nint id, uint ms, nint proc);
    [LibraryImport("user32.dll")] public static partial int KillTimer(nint h, nint id);
    [LibraryImport("user32.dll")] public static partial nint LoadCursorW(nint instance, nint name);
    [LibraryImport("user32.dll")] public static partial nint LoadImageW(nint instance, nint name, uint type, int cx, int cy, uint flags);
    [LibraryImport("user32.dll")] public static partial int GetSystemMetricsForDpi(int index, uint dpi);
    [LibraryImport("user32.dll")] public static partial uint GetDpiForWindow(nint h);
    [LibraryImport("user32.dll")] public static partial uint GetDpiForSystem();
    [LibraryImport("user32.dll")] public static partial int SetProcessDpiAwarenessContext(nint ctx);
    [LibraryImport("user32.dll")] public static partial int SystemParametersInfoForDpi(uint action, uint param, void* data, uint winIni, uint dpi);

    // shell32 / kernel32 / gdi32 / dwmapi / comctl32
    [LibraryImport("shell32.dll")] public static partial int Shell_NotifyIconW(uint msg, NOTIFYICONDATAW* data);
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint ExtractIconExW(string file, int index, nint* large, nint* small, uint count);
    [LibraryImport("kernel32.dll")] public static partial nint GetModuleHandleW(char* name);
    [LibraryImport("kernel32.dll")] public static partial nint OpenProcess(uint access, int inherit, uint pid);
    [LibraryImport("kernel32.dll")] public static partial int QueryFullProcessImageNameW(nint process, uint flags, char* buf, uint* size);
    [LibraryImport("kernel32.dll")] public static partial int CloseHandle(nint h);
    [LibraryImport("gdi32.dll")] public static partial nint CreateFontIndirectW(LOGFONTW* lf);
    [LibraryImport("gdi32.dll")] public static partial int DeleteObject(nint obj);
    [LibraryImport("dwmapi.dll")] public static partial int DwmGetWindowAttribute(nint h, uint attr, void* value, uint size);
    [LibraryImport("comctl32.dll")] public static partial int InitCommonControlsEx(long* icc);
    [LibraryImport("ole32.dll")] public static partial int CoInitializeEx(nint reserved, uint coInit);
    [LibraryImport("ole32.dll")] public static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* obj);
    [LibraryImport("user32.dll")] public static partial int EnableWindow(nint h, int enable);
    [LibraryImport("user32.dll")] public static partial nint SetFocus(nint h);

    public static nint Instance => GetModuleHandleW(null);

    public static string Text(nint h)
    {
        char* buf = stackalloc char[512];
        return new string(buf, 0, GetWindowTextW(h, buf, 512));
    }

    public static nint RegisterClass(string name, delegate* unmanaged<nint, uint, nint, nint, nint> proc, nint icon)
    {
        fixed (char* p = name)
        {
            var wc = new WNDCLASSEXW
            {
                Size = (uint)sizeof(WNDCLASSEXW), WndProc = (nint)proc, Instance = Instance, Icon = icon, IconSmall = icon,
                Cursor = LoadCursorW(0, 32512), Background = 16 /* COLOR_BTNFACE + 1 */, ClassName = p,
            };
            return RegisterClassExW(&wc);
        }
    }
}
