using IMGViewer.Services.Updates;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace IMGViewer;

public partial class App : Application
{
    internal const string BackgroundArgument = "--tray";
    private UpdateCoordinator? updates;
    internal UpdateCoordinator Updates => updates ??= new();
    private ViewerInstance? instance;
    private IdleCursor? idleCursor;
    private Forms.NotifyIcon? tray;
    private Forms.ContextMenuStrip? trayMenu;
    private Drawing.Icon? trayIcon;
    private bool exitRequested;
    private bool trayTipShown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == "--apply-update")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (e.Args.Length != 2) throw new InvalidOperationException("Solicitud de actualización incompleta.");
                await UpdateInstaller.Apply(e.Args[1]); Shutdown(0);
            }
            catch (Exception error)
            {
                ViewerDialog.Show("No se pudo completar la actualización. La versión anterior se conserva cuando es posible.\n\n" + error.Message, "IMG Viewer · Actualización");
                Shutdown(1);
            }
            return;
        }
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
                ViewerDialog.Show("No se pudo contactar con IMG Viewer. Inténtalo de nuevo.", "IMG Viewer");
            }

            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        instance.Listen(path => Dispatcher.InvokeAsync(() => ShowViewer(path)));
        if (!startInTray)
        {
            ShowViewer(path);
            _ = Dispatcher.InvokeAsync(() => InitializeBackgroundServices(window), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        else InitializeBackgroundServices(window);
    }

    private void InitializeBackgroundServices(MainWindow window)
    {
        if (exitRequested) return;
        CreateTray(window);
        window.BindUpdates(Updates);
        if (Updates.Enabled) Dispatcher.InvokeAsync(async () => {
            await Task.Run(UpdateInstaller.CleanupOldJobs);
            if (!exitRequested) await Updates.Check();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        nint handle = new WindowInteropHelper(window).EnsureHandle();
        idleCursor = new IdleCursor(HwndSource.FromHwnd(handle));
        Activated += (_, _) => idleCursor?.ResetLocal();
        Deactivated += (_, _) => idleCursor?.ResetLocal();
        if (!RefreshCursorSettings())
        {
            tray?.ShowBalloonTip(3000, "IMG Viewer",
                "El cursor solo se puede ocultar dentro de IMG Viewer en este equipo.", Forms.ToolTipIcon.Info);
        }

    }

    internal bool RefreshCursorSettings() =>
        idleCursor?.Configure(ViewerPreferences.HideIdleCursor, ViewerPreferences.CursorIdleSeconds,
            ViewerPreferences.CursorAcrossWindows) ?? true;

    private void CreateTray(MainWindow window)
    {
        trayIcon = Environment.ProcessPath is string executable ? Drawing.Icon.ExtractAssociatedIcon(executable) : null;
        trayMenu = new TrayContextMenu
        {
            Renderer = new TrayMenuRenderer(),
            BackColor = Drawing.Color.FromArgb(32, 32, 32),
            ForeColor = Drawing.Color.FromArgb(238, 238, 238),
            ShowImageMargin = false,
            Padding = new Forms.Padding(6),
            Font = new Drawing.Font("Segoe UI", 9.5F)
        };
        trayMenu.ItemAdded += (_, e) =>
        {
            if (e.Item is { } item) { item.ForeColor = trayMenu.ForeColor; item.Padding = new Forms.Padding(12, 7, 12, 7); }
        };
        trayMenu.Items.Add("Abrir IMG Viewer", null, (_, _) => ShowViewer());
        trayMenu.Items.Add("Capturar pantalla", null, (_, _) =>
        {
            // Let the native tray menu close before taking the desktop snapshot.
            Dispatcher.InvokeAsync(window.CaptureScreen, System.Windows.Threading.DispatcherPriority.Background);
        });
        trayMenu.Items.Add("Abrir carpeta…", null, (_, _) => Dispatcher.InvokeAsync(() =>
        {
            ShowViewer();
            window.OpenFolderFromTray();
        }));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Configuración…", null, (_, _) => Dispatcher.InvokeAsync(() =>
        {
            ShowViewer();
            window.OpenSettingsFromTray();
        }));
        trayMenu.Items.Add("Acerca de IMG Viewer", null, (_, _) => Dispatcher.InvokeAsync(() =>
        {
            ShowViewer();
            ViewerDialog.Show(window, "IMG Viewer\nVersión 2026.11.1\nVisor, capturas y marcación de imágenes.\nfrankgp.com/img-viewer", "Acerca de IMG Viewer");
        }));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Salir", null, (_, _) => ExitViewer());

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
            window.OpenExplorerImage(path);
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

    internal void ExitViewer()
    {
        exitRequested = true;
        Shutdown();
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
        updates?.Dispose();
        idleCursor?.Dispose();
        tray?.Dispose();
        trayMenu?.Dispose();
        trayIcon?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
