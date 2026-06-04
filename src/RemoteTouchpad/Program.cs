using RemoteTouchpad;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var appContext = new TrayAppContext();
        Application.Run(appContext);
    }
}
