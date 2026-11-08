using System.IO;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace IMGViewer;

public partial class App : Application
{
    internal const string BackgroundArgument = "--tray";
    private ViewerInstance? instance;
    private Forms.NotifyIcon? tray;
    private Forms.ContextMenuStrip? trayMenu;
    private Drawing.Icon? trayIcon;
    private bool exitRequested;
    private bool trayTipShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool startInTray = e.Args.Length > 0 && e.Args[0] == BackgroundArgument;
        string path = !startInTray && e.Args.Length > 0 ? e.Args[0] : string.Empty;
        if (!string.IsNullOrEmpty(path))
        {
            try { path = Path.GetFullPath(path); }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { }
        }

        instance = new ViewerInstance();
        if (!instance.IsPrimary)
        {
            if (!startInTray && !instance.Forward(path))
            {
                MessageBox.Show("No se pudo contactar con IMG Viewer. Inténtalo de nuevo.", "IMG Viewer");
            }

            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        instance.Listen(path => Dispatcher.InvokeAsync(() => ShowViewer(path)));
        CreateTray(window);

        if (startInTray)
        {
            new WindowInteropHelper(window).EnsureHandle();
        }
        else
        {
            ShowViewer(path);
        }
    }

    private void CreateTray(MainWindow window)
    {
        trayIcon = Environment.ProcessPath is string executable ? Drawing.Icon.ExtractAssociatedIcon(executable) : null;
        trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Abrir IMG Viewer", null, (_, _) => ShowViewer());
        trayMenu.Items.Add("Capturar pantalla", null, (_, _) =>
        {
            // Let the native tray menu close before taking the desktop snapshot.
            Dispatcher.InvokeAsync(window.CaptureScreen, System.Windows.Threading.DispatcherPriority.Background);
        });
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Salir", null, (_, _) =>
        {
            exitRequested = true;
            Shutdown();
        });

        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon ?? Drawing.SystemIcons.Application,
            Text = "IMG Viewer",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => ShowViewer();
    }

    internal void ShowViewer(string path = "")
    {
        if (exitRequested || MainWindow is not MainWindow window)
        {
            return;
        }

        if (path.Length > 0)
        {
            window.OpenImage(path);
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    internal bool TryHideViewer(MainWindow window)
    {
        if (exitRequested || tray is null || !ViewerPreferences.CloseToTray)
        {
            return false;
        }

        Dispatcher.InvokeAsync(() => HideViewer(window));
        return true;
    }

    private void HideViewer(MainWindow window)
    {
        if (exitRequested)
        {
            return;
        }

        window.Hide();
        if (!trayTipShown)
        {
            trayTipShown = true;
            tray?.ShowBalloonTip(3000, "IMG Viewer sigue activo",
                "Usa Impr Pant para capturar o el ícono junto al reloj para volver al visor.", Forms.ToolTipIcon.Info);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (!e.Cancel)
        {
            exitRequested = true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        exitRequested = true;
        tray?.Dispose();
        trayMenu?.Dispose();
        trayIcon?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
