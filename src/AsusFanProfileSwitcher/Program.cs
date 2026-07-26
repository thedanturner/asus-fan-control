namespace AsusFanProfileSwitcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"ASUS Fan Profile Switcher could not start.\n\n{exception.Message}",
                "Startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
