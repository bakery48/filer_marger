using System;
using System.Runtime.InteropServices;

namespace FilerMerger;

internal static class NativeMethods
{
    // ホットキー登録
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ウィンドウ操作
    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    // キー入力送信
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    // OS バージョン
    [DllImport("ntdll.dll")]
    internal static extern int RtlGetVersion(ref OSVERSIONINFOEX lpVersionInformation);

    // ウィンドウメッセージ定数
    internal const int WM_HOTKEY = 0x0312;
    internal const int SW_RESTORE = 9;

    // ホットキー修飾キー
    internal const uint MOD_WIN   = 0x0008;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_CTRL  = 0x0002;
    internal const uint MOD_NOREPEAT = 0x4000;

    // 仮想キーコード
    internal const uint VK_F = 0x46;  // Win+Shift+F をホットキーに使用

    // SendInput 構造体
    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint type;
        internal INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] internal MOUSEINPUT mi;
        [FieldOffset(0)] internal KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int dx, dy, mouseData, dwFlags, time;
        internal IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort wVk;
        internal ushort wScan;
        internal uint dwFlags;
        internal uint time;
        internal IntPtr dwExtraInfo;
    }

    internal const uint INPUT_KEYBOARD  = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct OSVERSIONINFOEX
    {
        internal uint dwOSVersionInfoSize;
        internal uint dwMajorVersion;
        internal uint dwMinorVersion;
        internal uint dwBuildNumber;
        internal uint dwPlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string szCSDVersion;
        internal ushort wServicePackMajor, wServicePackMinor;
        internal ushort wSuiteMask;
        internal byte wProductType;
        internal byte wReserved;
    }

    /// <summary>Windows 11 かどうかを判定 (ビルド番号 22000 以上)</summary>
    internal static bool IsWindows11()
    {
        var osvi = new OSVERSIONINFOEX { dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOEX>() };
        RtlGetVersion(ref osvi);
        return osvi.dwMajorVersion >= 10 && osvi.dwBuildNumber >= 22000;
    }

    /// <summary>Ctrl+T (新しいタブ) を送信する</summary>
    internal static void SendCtrlT()
    {
        var inputs = new INPUT[4];

        // Ctrl down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0x11; // VK_CONTROL

        // T down
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0x54; // T

        // T up
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = 0x54;
        inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = 0x11;
        inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput(4, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Ctrl+L (アドレスバーにフォーカス) を送信する</summary>
    internal static void SendCtrlL()
    {
        var inputs = new INPUT[4];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0x11;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0x4C; // L

        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = 0x4C;
        inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = 0x11;
        inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput(4, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Enter キーを送信する</summary>
    internal static void SendEnter()
    {
        var inputs = new INPUT[2];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0x0D; // VK_RETURN

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0x0D;
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }
}
