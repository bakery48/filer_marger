using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace FilerMerger;

/// <summary>
/// エクスプローラーウィンドウの列挙・統合を担当するクラス
/// </summary>
internal static class ExplorerConsolidator
{
    /// <summary>
    /// 同一パスのエクスプローラーウィンドウを重複排除して閉じる。
    /// 各パスにつき最初に見つかったウィンドウのみ残す。
    /// </summary>
    /// <returns>閉じたウィンドウ数</returns>
    internal static ConsolidateResult DeduplicateWindows()
    {
        var allWindows = GetExplorerWindows();
        if (allWindows.Count == 0)
            return new ConsolidateResult(0, 0, false);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int closedCount = 0;

        foreach (var (path, window) in allWindows)
        {
            if (!seen.Add(path))
            {
                try
                {
                    window.Quit();
                    closedCount++;
                }
                catch { /* ウィンドウが既に閉じられていた場合は無視 */ }
            }
        }

        int remaining = allWindows.Count - closedCount;
        return new ConsolidateResult(closedCount, remaining, false);
    }

    /// <summary>
    /// 全エクスプローラーウィンドウを1つに統合する（Windows 11 タブ使用）。
    /// 重複パスを除去し、全ユニークパスを1ウィンドウのタブとして開く。
    /// Windows 10 以下では重複排除のみ実施。
    /// </summary>
    /// <returns>統合結果</returns>
    internal static ConsolidateResult MergeAllToOneWindow()
    {
        var allWindows = GetExplorerWindows();
        if (allWindows.Count == 0)
            return new ConsolidateResult(0, 0, false);

        // ユニークパスを順序を保って収集
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniquePaths = new List<string>();
        var windowsToClose = new List<dynamic>();
        IntPtr mainHwnd = IntPtr.Zero;

        foreach (var (path, window) in allWindows)
        {
            if (seen.Add(path))
            {
                uniquePaths.Add(path);
                if (mainHwnd == IntPtr.Zero)
                {
                    try { mainHwnd = (IntPtr)(int)window.HWND; }
                    catch { }
                }
            }
            else
            {
                windowsToClose.Add(window);
            }
        }

        // 重複ウィンドウを閉じる
        foreach (var w in windowsToClose)
            try { w.Quit(); } catch { }

        bool usedTabs = false;

        // Windows 11 ではタブで統合
        if (NativeMethods.IsWindows11() && uniquePaths.Count > 1 && mainHwnd != IntPtr.Zero)
        {
            usedTabs = OpenPathsAsTabs(mainHwnd, uniquePaths);
        }

        return new ConsolidateResult(windowsToClose.Count, uniquePaths.Count, usedTabs);
    }

    /// <summary>
    /// 指定された全パスを1つのエクスプローラーウィンドウのタブとして開く。
    /// paths[0] は既に mainHwnd で開かれている前提。
    /// </summary>
    private static bool OpenPathsAsTabs(IntPtr mainHwnd, List<string> paths)
    {
        try
        {
            NativeMethods.SetForegroundWindow(mainHwnd);
            NativeMethods.ShowWindow(mainHwnd, NativeMethods.SW_RESTORE);
            Thread.Sleep(300);

            for (int i = 1; i < paths.Count; i++)
            {
                // 新しいタブを開く
                NativeMethods.SetForegroundWindow(mainHwnd);
                Thread.Sleep(100);
                NativeMethods.SendCtrlT();
                Thread.Sleep(400);

                // アドレスバーにフォーカスしてパスを入力
                NativeMethods.SetForegroundWindow(mainHwnd);
                Thread.Sleep(100);
                NativeMethods.SendCtrlL();
                Thread.Sleep(200);

                // パスを文字として送信
                SendPathToAddressBar(paths[i]);
                Thread.Sleep(100);
                NativeMethods.SendEnter();
                Thread.Sleep(300);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>アドレスバーにパス文字列をキー入力として送信する</summary>
    private static void SendPathToAddressBar(string path)
    {
        // パスの各文字を SendInput で送信
        var inputs = new List<NativeMethods.INPUT>();

        foreach (char c in path)
        {
            // Unicode 文字として送信 (KEYEVENTF_UNICODE)
            var keyDown = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD
            };
            keyDown.u.ki.wVk = 0;
            keyDown.u.ki.wScan = (ushort)c;
            keyDown.u.ki.dwFlags = 0x0004; // KEYEVENTF_UNICODE

            var keyUp = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD
            };
            keyUp.u.ki.wVk = 0;
            keyUp.u.ki.wScan = (ushort)c;
            keyUp.u.ki.dwFlags = 0x0004 | NativeMethods.KEYEVENTF_KEYUP;

            inputs.Add(keyDown);
            inputs.Add(keyUp);
        }

        var arr = inputs.ToArray();
        NativeMethods.SendInput((uint)arr.Length, arr, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// 現在開いているエクスプローラーウィンドウを全て列挙する。
    /// </summary>
    /// <returns>(正規化されたパス, COMウィンドウオブジェクト) のリスト</returns>
    private static List<(string Path, dynamic Window)> GetExplorerWindows()
    {
        var result = new List<(string, dynamic)>();

        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType == null) return result;

        dynamic? shell = null;
        dynamic? windows = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            windows = shell!.Windows();
            int count = windows.Count;

            for (int i = 0; i < count; i++)
            {
                dynamic? window = null;
                try
                {
                    window = windows.Item(i);
                    if (window == null) continue;

                    string? path = ExtractPath(window);
                    if (path != null)
                        result.Add((path, window));
                }
                catch { /* ウィンドウへのアクセス失敗は無視 */ }
            }
        }
        finally
        {
            if (windows != null)
                try { Marshal.ReleaseComObject(windows); } catch { }
            if (shell != null)
                try { Marshal.ReleaseComObject(shell); } catch { }
        }

        return result;
    }

    /// <summary>
    /// エクスプローラーウィンドウから現在のパスを取得する。
    /// エクスプローラー以外のウィンドウ（IEなど）は null を返す。
    /// </summary>
    private static string? ExtractPath(dynamic window)
    {
        try
        {
            // エクスプローラーウィンドウの識別
            string? name = window.Name as string;
            if (name == null) return null;

            bool isExplorer =
                name.IndexOf("Explorer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("エクスプローラー", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isExplorer) return null;

            string? locationUrl = window.LocationURL as string;
            if (string.IsNullOrEmpty(locationUrl)) return null;

            // file:/// URL → ローカルパスに変換
            if (locationUrl.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                string decoded = Uri.UnescapeDataString(locationUrl[8..]);
                // / → \ に変換し、末尾のスラッシュを除去
                return decoded.Replace('/', '\\').TrimEnd('\\');
            }

            // その他 URL (OneDrive 等) はそのまま使用
            return locationUrl.TrimEnd('/');
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>統合処理の結果を保持する</summary>
internal readonly record struct ConsolidateResult(
    int ClosedCount,
    int RemainingCount,
    bool UsedTabs);
