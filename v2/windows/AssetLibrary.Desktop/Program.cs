namespace AssetLibrary.Desktop;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var options = HostOptions.Parse(args);
            ApplicationConfiguration.Initialize();
            using var window = new MainWindow(options);
            Application.Run(window);
            return window.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"启动失败：{exception.Message}");
            return 1;
        }
    }
}
