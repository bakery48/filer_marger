using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FilerMerger;

/// <summary>
/// システムトレイ常駐アプリのメインクラス。
/// ホットキー (Win+Shift+F) でエクスプローラー統合を実行する。
/// </summary>
internal sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyWindow _hotkeyWindow;
    private const int HOTKEY_ID_DEDUP  = 1;
    private const int HOTKEY_ID_MERGE  = 2;

    internal TrayApp()
    {
        // コンテキストメニュー
        var menu = new ContextMenuStrip();

        var itemDedup = new ToolStripMenuItem("重複を閉じる  (Win+Shift+F)");
        itemDedup.Click += (_, _) => RunDedup();
        menu.Items.Add(itemDedup);

        var itemMerge = new ToolStripMenuItem("1ウィンドウに統合  (Win+Shift+G)");
        itemMerge.Click += (_, _) => RunMerge();
        menu.Items.Add(itemMerge);

        menu.Items.Add(new ToolStripSeparator());

        var itemAbout = new ToolStripMenuItem("バージョン情報");
        itemAbout.Click += (_, _) => ShowAbout();
        menu.Items.Add(itemAbout);

        var itemExit = new ToolStripMenuItem("終了");
        itemExit.Click += (_, _) => Exit();
        menu.Items.Add(itemExit);

        // トレイアイコン
        _trayIcon = new NotifyIcon
        {
            Icon            = CreateIcon(),
            Text            = "FilerMerger\nファイラ統合ツール",
            ContextMenuStrip = menu,
            Visible         = true,
        };
        _trayIcon.DoubleClick += (_, _) => RunDedup();

        // ホットキー登録ウィンドウ
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyFired += OnHotkeyFired;

        bool ok1 = NativeMethods.RegisterHotKey(
            _hotkeyWindow.Handle, HOTKEY_ID_DEDUP,
            NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_F);

        bool ok2 = NativeMethods.RegisterHotKey(
            _hotkeyWindow.Handle, HOTKEY_ID_MERGE,
            NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            0x47 /* G */);

        if (!ok1 || !ok2)
        {
            _trayIcon.Text = "FilerMerger (ホットキー登録失敗)";
        }
    }

    private void OnHotkeyFired(object? sender, int id)
    {
        if (id == HOTKEY_ID_DEDUP)  RunDedup();
        else if (id == HOTKEY_ID_MERGE) RunMerge();
    }

    private void RunDedup()
    {
        try
        {
            var result = ExplorerConsolidator.DeduplicateWindows();
            if (result.ClosedCount > 0)
                Notify($"{result.ClosedCount} 個の重複ウィンドウを閉じました（残 {result.RemainingCount} 個）");
            else
                Notify("重複するエクスプローラーウィンドウはありません");
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
    }

    private void RunMerge()
    {
        try
        {
            var result = ExplorerConsolidator.MergeAllToOneWindow();
            if (result.ClosedCount > 0)
            {
                string msg = result.UsedTabs
                    ? $"統合完了: {result.RemainingCount} 個のタブを 1 ウィンドウに統合しました"
                    : $"{result.ClosedCount} 個の重複ウィンドウを閉じました（残 {result.RemainingCount} 個）";
                Notify(msg);
            }
            else if (result.RemainingCount > 1 && NativeMethods.IsWindows11())
            {
                Notify("重複はありませんでした（ユニークパスは既に 1 つずつ開かれています）");
            }
            else
            {
                Notify("エクスプローラーウィンドウが見つかりません");
            }
        }
        catch (Exception ex)
        {
            NotifyError(ex.Message);
        }
    }

    private void Notify(string message) =>
        _trayIcon.ShowBalloonTip(3000, "FilerMerger", message, ToolTipIcon.Info);

    private void NotifyError(string message) =>
        _trayIcon.ShowBalloonTip(4000, "FilerMerger - エラー", message, ToolTipIcon.Error);

    private static void ShowAbout()
    {
        string os = NativeMethods.IsWindows11() ? "Windows 11" : "Windows 10 以前";
        MessageBox.Show(
            $"FilerMerger v1.0\n\n" +
            $"エクスプローラーウィンドウの重複を排除し、\n" +
            $"1 つのウィンドウに統合するトレイツールです。\n\n" +
            $"■ ホットキー\n" +
            $"  Win+Shift+F : 重複ウィンドウを閉じる\n" +
            $"  Win+Shift+G : 1 ウィンドウに統合（Win11 はタブ使用）\n\n" +
            $"■ 動作環境: {os}",
            "FilerMerger について",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void Exit() => Application.Exit();

    /// <summary>アプリアイコンをプログラムで生成する</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        // 背景
        g.FillRectangle(new SolidBrush(Color.FromArgb(0x1E, 0x88, 0xE5)), 2, 2, 28, 28);

        // フォルダアイコン風
        g.FillRectangle(Brushes.White, 6, 10, 20, 14);
        g.FillRectangle(Brushes.White, 6, 7, 9, 4);

        // 統合の矢印（↓）
        using var font = new Font("Arial", 10, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString("⇒", font, Brushes.White, 10, 13);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID_DEDUP);
        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HOTKEY_ID_MERGE);
        _hotkeyWindow.DestroyHandle();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    // ─── ホットキー受信用の不可視ウィンドウ ───────────────────────────
    private sealed class HotkeyWindow : NativeWindow
    {
        internal event EventHandler<int>? HotkeyFired;

        internal HotkeyWindow() =>
            CreateHandle(new CreateParams { Caption = "FilerMerger_HotkeyWnd" });

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
                HotkeyFired?.Invoke(this, m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
}
