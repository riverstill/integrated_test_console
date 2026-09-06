using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Console.WPF.Services;

/// <summary>窗口 chrome：深色主题时把 OS 标题栏/边框也切暗，
/// 否则内容再黑、四周仍是浅色标题栏。</summary>
public static class WindowChrome
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int value, int size);

    public static void SetDarkTitle(Window w, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int v = dark ? 1 : 0;
            // Win11 20 / Win10旧版 19，逐个试，失败静默
            if (DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref v, sizeof(int));
        }
        catch { }
    }
}
