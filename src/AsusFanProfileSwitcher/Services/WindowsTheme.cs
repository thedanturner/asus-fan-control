using System.Runtime.InteropServices;

namespace AsusFanProfileSwitcher.Services;

internal static class WindowsTheme
{
    public static void ApplyDark(Form form, params Control[] scrollableControls)
    {
        try
        {
            var enabled = 1;
            _ = DwmSetWindowAttribute(
                form.Handle,
                20,
                ref enabled,
                Marshal.SizeOf<int>());

            foreach (var control in scrollableControls)
            {
                _ = SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            }
        }
        catch
        {
            // Dark native chrome is cosmetic and is unavailable on older Windows builds.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        nint window,
        string? subAppName,
        string? subIdList);
}
