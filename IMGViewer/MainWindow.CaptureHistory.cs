using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private sealed class SessionCapture
    {
        public string Name { get; init; } = "";
        public BitmapSource Preview { get; set; } = null!;
        public BitmapSource Photo { get; set; } = null!;
        public BitmapSource? SavedPhoto;
        public BitmapSource? RotationBase;
        public int Rotation;
        public bool PixelEdit;
        public List<StyledMarker> Marks = [];
        public string? FilePath;
    }
    private readonly ObservableCollection<SessionCapture> captureHistory = [];
    private SessionCapture? activeCapture;
    private bool switchingCapture;
    private bool historyView;

    private void RememberActiveCapture()
    {
        if (activeCapture is null || Photo.Source is not BitmapSource bitmap) return;
        activeCapture.Photo = bitmap;
        activeCapture.SavedPhoto = lastSavedPhoto;
        activeCapture.RotationBase = rotationBasePhoto;
        activeCapture.Rotation = rotationDegrees;
        activeCapture.PixelEdit = hasPixelEdit;
        activeCapture.Marks = markers.ToList();
        activeCapture.Preview = FlattenMarkers(bitmap);
        activeCapture.FilePath = CurrentGridImage;
    }
    private void AddCaptureToHistory(DateTime timestamp)
    {
        if (Photo.Source is not BitmapSource bitmap) return;
        activeCapture = new SessionCapture { Name = "Captura " + timestamp.ToString("HH:mm:ss"), Photo = bitmap, Preview = bitmap };
        captureHistory.Insert(0, activeCapture);
        RememberActiveCapture();
        CaptureHistoryList.ItemsSource = captureHistory;
        historyView = true;
        galleryOpen = true;
        UpdateHistoryView();
    }
    private void ShowSessionCapture(SessionCapture capture)
    {
        imageOpenVersion++;
        deferImageDetails = false;
        if (ReferenceEquals(activeCapture,capture)) return;
        RememberActiveCapture();
        switchingCapture = true;
        try
        {
            CloseFolderGrid(); StopGridPhotoAnimation(); HideCaptureFrame();
            activeCapture = capture;
            captureExportPath = null;
            Photo.Source = capture.Photo;
            lastSavedPhoto = capture.SavedPhoto;
            rotationBasePhoto = capture.RotationBase;
            rotationDegrees = capture.Rotation;
            hasPixelEdit = capture.PixelEdit;
            folderImages = capture.FilePath is null ? [] : [capture.FilePath];
            currentIndex = capture.FilePath is null ? -1 : 0;
            markers.Clear(); markers.AddRange(capture.Marks);
            RefreshMarkerOverlay(); ResetPhotoView();
            EmptyState.Visibility = Visibility.Collapsed;
            CropButton.Visibility = Visibility.Visible;
            Title = "IMG Viewer — " + capture.Name;
            UpdatePendingSaveIndicator(); UpdateMetadataFooter(); UpdateSlideShowAvailability();
        }
        finally { switchingCapture = false; }
    }
    private void CaptureHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SessionCapture capture }) ShowSessionCapture(capture);
    }
    private void RemoveCapture_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SessionCapture capture }) return;
        bool current = ReferenceEquals(activeCapture,capture);
        captureHistory.Remove(capture);
        if (current)
        {
            activeCapture = null;
            if (captureHistory.Count > 0) ShowSessionCapture(captureHistory[0]);
            else ClearHistoryCanvas();
        }
        e.Handled = true;
    }
    private void ClearCaptureHistory_Click(object sender, RoutedEventArgs e)
    {
        bool clearCanvas = activeCapture is not null;
        activeCapture = null; captureHistory.Clear();
        if (clearCanvas) ClearHistoryCanvas();
    }
    private void ClearHistoryCanvas()
    {
        captureExportPath = null;
        ClearMarkers(); Photo.Source = null; lastSavedPhoto = rotationBasePhoto = null;
        folderImages = []; currentIndex = -1; rotationDegrees = 0; hasPixelEdit = false;
        EmptyState.Visibility = Visibility.Visible;
        CropButton.Visibility = Visibility.Collapsed;
        UpdatePendingSaveIndicator(); UpdateMetadataFooter(); UpdateSlideShowAvailability();
    }
    private void GalleryMode_Click(object sender, RoutedEventArgs e)
    {
        historyView = ReferenceEquals(sender, GalleryCapturesTab);
        UpdateHistoryView();
    }
    private void UpdateHistoryView()
    {
        if (CaptureHistoryPanel is null) return;
        GalleryFolderTab.Tag = historyView ? null : "active";
        GalleryCapturesTab.Tag = historyView ? "active" : null;
        CaptureHistoryPanel.Visibility = historyView ? Visibility.Visible : Visibility.Collapsed;
        GalleryList.Visibility = historyView ? Visibility.Collapsed : Visibility.Visible;
        GalleryFolderControls.Visibility = historyView ? Visibility.Collapsed : Visibility.Visible;
        GalleryMessage.Visibility = historyView ? Visibility.Collapsed : Visibility.Visible;
    }
}
