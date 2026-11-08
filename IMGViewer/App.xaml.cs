using System.Windows;

namespace IMGViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;

        if (e.Args.Length > 0)
        {
            window.OpenImage(e.Args[0]);
        }

        window.Show();
    }
}
