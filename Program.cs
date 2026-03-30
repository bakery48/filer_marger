using System;
using System.Threading;
using System.Windows.Forms;

namespace FilerMerger;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "FilerMerger_SingleInstance_v1", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "FilerMerger はすでに起動しています。\nタスクバーの通知領域をご確認ください。",
                "FilerMerger",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
            MessageBox.Show($"予期しないエラー: {e.Exception.Message}", "FilerMerger",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        using var trayApp = new TrayApp();
        Application.Run();

        _mutex.ReleaseMutex();
    }
}
