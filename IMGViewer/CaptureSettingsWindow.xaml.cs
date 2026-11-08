using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace IMGViewer;

public partial class CaptureSettingsWindow : Window
{
    public CaptureSettingsWindow()
    {
        InitializeComponent();
        AppVersion.Text = $"Versión {typeof(App).Assembly.GetName().Version?.ToString(3)}";
        GridColumnPicker.ItemsSource = Enumerable.Range(1, ViewerPreferences.MaximumGridColumns);
        GridColumnPicker.SelectedItem = ViewerPreferences.GridColumns;
        GridColumnPicker.SelectionChanged += GridColumnPicker_SelectionChanged;
        HideIdleCursor.IsChecked = ViewerPreferences.HideIdleCursor;
        CursorDelayPicker.ItemsSource = Enumerable.Range(1, ViewerPreferences.MaximumCursorIdleSeconds);
        CursorDelayPicker.SelectedItem = ViewerPreferences.CursorIdleSeconds;
        CursorDelayPicker.SelectionChanged += CursorDelayPicker_SelectionChanged;
        CursorScopePicker.SelectedIndex = ViewerPreferences.CursorAcrossWindows ? 1 : 0;
        CursorScopePicker.SelectionChanged += CursorScopePicker_SelectionChanged;
        CopyCapturesToClipboard.IsChecked = ViewerPreferences.CopyCapturesToClipboard;
        CloseToTray.IsChecked = ViewerPreferences.CloseToTray;
        StartWithWindows.IsChecked = ViewerPreferences.StartWithWindows;
        FolderPath.Text = CapturePreferences.CaptureFolder;
    }

    private void AppPage_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            SettingsStatus.Text = "No se pudo abrir la página de IMG Viewer.";
        }
    }

    private void GridColumnPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GridColumnPicker.SelectedItem is not int columns)
        {
            return;
        }

        try
        {
            ViewerPreferences.GridColumns = columns;
            SettingsStatus.Text = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            GridColumnPicker.SelectionChanged -= GridColumnPicker_SelectionChanged;
            GridColumnPicker.SelectedItem = ViewerPreferences.GridColumns;
            GridColumnPicker.SelectionChanged += GridColumnPicker_SelectionChanged;
            SettingsStatus.Text = "No se pudo guardar la cantidad de imágenes por fila.";
        }
    }

    private void CursorDelayPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CursorDelayPicker.SelectedItem is not int seconds)
        {
            return;
        }

        try
        {
            ViewerPreferences.CursorIdleSeconds = seconds;
            ApplyCursorSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            CursorDelayPicker.SelectionChanged -= CursorDelayPicker_SelectionChanged;
            CursorDelayPicker.SelectedItem = ViewerPreferences.CursorIdleSeconds;
            CursorDelayPicker.SelectionChanged += CursorDelayPicker_SelectionChanged;
            SettingsStatus.Text = "No se pudo guardar el tiempo de espera del cursor.";
        }
    }

    private void CursorScopePicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CursorScopePicker.SelectedIndex < 0)
        {
            return;
        }

        try
        {
            ViewerPreferences.CursorAcrossWindows = CursorScopePicker.SelectedIndex == 1;
            ApplyCursorSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            CursorScopePicker.SelectionChanged -= CursorScopePicker_SelectionChanged;
            CursorScopePicker.SelectedIndex = ViewerPreferences.CursorAcrossWindows ? 1 : 0;
            CursorScopePicker.SelectionChanged += CursorScopePicker_SelectionChanged;
            SettingsStatus.Text = "No se pudo guardar el alcance del cursor.";
        }
    }

    private void ApplyCursorSettings()
    {
        SettingsStatus.Text = (Application.Current as App)?.RefreshCursorSettings() == false
            ? "El cursor solo se puede ocultar dentro de IMG Viewer en este equipo."
            : string.Empty;
    }

    private void ToggleSetting_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender == HideIdleCursor)
            {
                ViewerPreferences.HideIdleCursor = HideIdleCursor.IsChecked == true;
                ApplyCursorSettings();
                return;
            }
            else if (sender == CopyCapturesToClipboard)
            {
                ViewerPreferences.CopyCapturesToClipboard = CopyCapturesToClipboard.IsChecked == true;
            }
            else if (sender == CloseToTray)
            {
                ViewerPreferences.CloseToTray = CloseToTray.IsChecked == true;
            }
            else
            {
                ViewerPreferences.StartWithWindows = StartWithWindows.IsChecked == true;
            }

            SettingsStatus.Text = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   System.Security.SecurityException or InvalidOperationException)
        {
            if (sender is System.Windows.Controls.CheckBox option)
            {
                option.IsChecked = option.IsChecked != true;
            }

            SettingsStatus.Text = "No se pudo guardar la opción.";
        }
    }

    private void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Seleccionar carpeta de capturas" };
        if (Directory.Exists(FolderPath.Text))
        {
            dialog.InitialDirectory = FolderPath.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            UpdateFolder(dialog.FolderName);
        }
    }

    private void ResetFolder_Click(object sender, RoutedEventArgs e) => UpdateFolder(null);

    private void UpdateFolder(string? folder)
    {
        try
        {
            CapturePreferences.SetFolder(folder);
            FolderPath.Text = CapturePreferences.CaptureFolder;
            SettingsStatus.Text = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsStatus.Text = "No se pudo guardar la carpeta de capturas.";
        }
    }
}
