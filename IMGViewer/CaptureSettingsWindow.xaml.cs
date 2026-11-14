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
        Loaded += (_,_) =>
        {
            if (Owner is MainWindow viewer)
            {
                CaptureShortcutLabel.Text = viewer.CaptureShortcutLabel;
                if (!viewer.CaptureShortcutAvailable) CaptureShortcutStatus.Text = "El atajo no está disponible. Selecciona otro para capturar desde cualquier aplicación.";
            }
        };
        foreach (string extension in ViewerPreferences.AvailableExtensions)
        {
            var option = new System.Windows.Controls.CheckBox { Content = extension.ToUpperInvariant(), Tag = extension, IsChecked = ViewerPreferences.EnabledExtensions.Contains(extension), Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0,8,20,8) };
            option.Click += FileType_Click;
            FileTypeOptions.Children.Add(option);
        }
        CaptureShortcutLabel.Text = (Owner as MainWindow)?.CaptureShortcutLabel ?? CaptureShortcut.Load().Label;
        AutoSaveCaptures.IsChecked = CapturePreferences.AutoSave;
        CaptureFormatPicker.SelectedItem = CaptureFormatPicker.Items.OfType<System.Windows.Controls.ComboBoxItem>().First(item => (string)item.Tag == CapturePreferences.FormatExtension);
        CaptureFormatPicker.SelectionChanged += CaptureFormatPicker_SelectionChanged;
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
        MinimizeAfterCopy.IsChecked = ViewerPreferences.MinimizeAfterCopy;
        CloseToTray.IsChecked = ViewerPreferences.CloseToTray;
        StartWithWindows.IsChecked = ViewerPreferences.StartWithWindows;
        FolderPath.Text = CapturePreferences.CaptureFolder;
    }

    private void AutoSaveCaptures_Click(object sender, RoutedEventArgs e)
    {
        try { CapturePreferences.AutoSave = AutoSaveCaptures.IsChecked == true; SettingsStatus.Text = ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { AutoSaveCaptures.IsChecked = CapturePreferences.AutoSave; SettingsStatus.Text = "No se pudo guardar la preferencia."; }
    }
    private void CaptureFormatPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CaptureFormatPicker.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        try { CapturePreferences.FormatExtension = (string)item.Tag; SettingsStatus.Text = ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { SettingsStatus.Text = "No se pudo guardar el formato."; }
    }

    private void ConfigureDefaults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DefaultAppRegistration.RegisterCurrentExecutable();
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString("IMGViewer")) { UseShellExecute = true });
            DefaultAppsStatus.Text = "Selecciona IMG Viewer y confirma los formatos en Windows. Conserva el ejecutable en esta ubicación; si lo mueves, vuelve a pulsar este botón.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or Win32Exception or InvalidOperationException)
        {
            DefaultAppsStatus.Text = "No se pudo configurar el visor. " + ex.Message;
        }
    }

    private void FileType_Click(object sender, RoutedEventArgs e) => SaveFileTypes();
    private void SelectAllFileTypes_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in FileTypeOptions.Children.OfType<System.Windows.Controls.CheckBox>()) option.IsChecked = true;
        SaveFileTypes();
    }
    private void SaveFileTypes()
    {
        try
        {
            ViewerPreferences.SetEnabledExtensions(FileTypeOptions.Children.OfType<System.Windows.Controls.CheckBox>().Where(option => option.IsChecked == true).Select(option => (string)option.Tag));
            FileTypeStatus.Text = ViewerPreferences.EnabledExtensions.Count == 0 ? "No hay formatos seleccionados." : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            FileTypeStatus.Text = "No se pudo guardar la selección.";
            foreach (var option in FileTypeOptions.Children.OfType<System.Windows.Controls.CheckBox>()) option.IsChecked = ViewerPreferences.EnabledExtensions.Contains((string)option.Tag);
        }
    }

    private void SettingsNavigation_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GeneralSettings is null || SettingsNavigation.SelectedItem is not System.Windows.Controls.ListBoxItem item) return;
        GeneralSettings.Visibility = (string)item.Tag == "general" ? Visibility.Visible : Visibility.Collapsed;
        CaptureSettings.Visibility = (string)item.Tag == "captures" ? Visibility.Visible : Visibility.Collapsed;
        GallerySettings.Visibility = (string)item.Tag == "gallery" ? Visibility.Visible : Visibility.Collapsed;
        FormatSettings.Visibility = (string)item.Tag == "formats" ? Visibility.Visible : Visibility.Collapsed;
        CursorSettings.Visibility = (string)item.Tag == "cursor" ? Visibility.Visible : Visibility.Collapsed;
        AboutSettings.Visibility = (string)item.Tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        SettingsContentScroll.ScrollToTop();
    }
    private void SettingsSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (SettingsNavigation is null || SettingsNoResults is null) return;
        string query = SettingsSearch.Text.Trim();
        var keywords = new Dictionary<string,string> { ["general"]="General inicio Windows bandeja cerrar segundo plano minimizar copiar portapapeles", ["captures"]="Capturas carpeta guardar descargas portapapeles copiar atajo teclado Impr Pant", ["gallery"]="Galería columnas cuadrícula imágenes fila", ["formats"]="Formatos tipos archivos jpg jpeg png bmp svg webp predeterminado default Windows asociaciones", ["cursor"]="Cursor mouse ocultar inactividad segundos alcance", ["about"]="Acerca versión página web IMG Viewer" };
        foreach (var item in SettingsNavigation.Items.OfType<System.Windows.Controls.ListBoxItem>())
            item.Visibility = keywords[(string)item.Tag].Contains(query, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        var visible = SettingsNavigation.Items.OfType<System.Windows.Controls.ListBoxItem>().Where(item => item.Visibility == Visibility.Visible).ToArray();
        SettingsNoResults.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (visible.Length == 0)
            GeneralSettings.Visibility = CaptureSettings.Visibility = GallerySettings.Visibility = FormatSettings.Visibility = CursorSettings.Visibility = AboutSettings.Visibility = Visibility.Collapsed;
        else if (SettingsNavigation.SelectedItem is not System.Windows.Controls.ListBoxItem selected || selected.Visibility != Visibility.Visible)
            SettingsNavigation.SelectedItem = visible[0];
        else SettingsNavigation_SelectionChanged(SettingsNavigation, null!);
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
            else if (sender == MinimizeAfterCopy)
            {
                ViewerPreferences.MinimizeAfterCopy = MinimizeAfterCopy.IsChecked == true;
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
