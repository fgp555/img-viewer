using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private sealed class SessionCapture : INotifyPropertyChanged
    {
        public Guid DraftId {get;init;}=Guid.NewGuid();
        public DateTime Created {get;init;}=DateTime.Now;
        public string Name { get; init; } = "";
        private BitmapSource preview = null!;
        public BitmapSource Preview
        {
            get => preview;
            set
            {
                if(ReferenceEquals(preview,value))return;
                preview=value;
                PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Preview)));
            }
        }
        private bool isActive;
        public bool IsActive
        {
            get => isActive;
            set
            {
                if(isActive==value)return;
                isActive=value;
                PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public BitmapSource Photo { get; set; } = null!;
        public BitmapSource? SavedPhoto;
        public BitmapSource? RotationBase;
        public int Rotation;
        public bool PixelEdit;
        public List<StyledMarker> Marks = [];
        public string? FilePath;
    }
    private readonly ObservableCollection<SessionCapture> captureHistory = [];
    private SessionCapture? currentCapture;
    private SessionCapture? activeCapture
    {
        get => currentCapture;
        set
        {
            if(ReferenceEquals(currentCapture,value))return;
            if(currentCapture is not null)currentCapture.IsActive=false;
            currentCapture=value;
            if(currentCapture is not null)currentCapture.IsActive=true;
        }
    }
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
        PersistCaptureDraft(activeCapture);
    }
    private void AddCaptureToHistory(DateTime timestamp)
    {
        if (Photo.Source is not BitmapSource bitmap) return;
        activeCapture = new SessionCapture { Name = "Captura " + timestamp.ToString("HH:mm:ss"), Created=timestamp, Photo = bitmap, Preview = bitmap };
        captureHistory.Insert(0, activeCapture);
        RememberActiveCapture();
        CaptureHistoryList.ItemsSource = captureHistory;
        cloudView=false;
        historyView = true;
        galleryOpen = true;
        UpdateHistoryView();
    }
    private void ShowSessionCapture(SessionCapture capture)
    {
        imageOpenVersion++;
        deferImageDetails = false;
        EndInlineText(true);
        bool same=ReferenceEquals(activeCapture,capture);
        if(same && Photo.Source is BitmapSource && EmptyState.Visibility==Visibility.Collapsed && !IsFolderGridOpen)
        {
            RefreshMarkerOverlay();UpdateMetadataFooter();UpdatePendingSaveIndicator();return;
        }
        if(!same)RememberActiveCapture();
        // Copy before resetting the outgoing editor; the history model owns editable layers.
        var restoredMarks=capture.Marks.ToArray();
        draftSaveTimer.Stop();
        switchingCapture = true;
        try
        {
            CancelInlineCrop();
            if(pastedImageGesture is {} gesture)gesture.Thumb.CancelDrag();
            CloseFolderGrid(); StopGridPhotoAnimation(); HideCaptureFrame();
            ClearMarkers();
            PastedImagesCanvas.Children.Clear();pastedImageBodies.Clear();pastedImageCorners.Clear();
            activeCapture = capture;
            captureExportPath = null;
            Photo.Source = capture.Photo;
            lastSavedPhoto = capture.SavedPhoto;
            rotationBasePhoto = capture.RotationBase;
            rotationDegrees = capture.Rotation;
            hasPixelEdit = capture.PixelEdit;
            folderImages = capture.FilePath is null ? [] : [capture.FilePath];
            currentIndex = capture.FilePath is null ? -1 : 0;
            markers.AddRange(restoredMarks);
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
        if(ReferenceEquals(widgetCapture,capture))captureWidget?.Close();
        DeleteCaptureDraft(capture);
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
        if(captureHistory.Count==0)return;
        if(ViewerDialog.Show(this,"¿Eliminar todos los borradores y vaciar el historial de capturas? Los archivos que guardaste se conservarán.","Limpiar capturas",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        discardRecoveredDrafts=true;
        captureWidget?.Close();
        foreach(var capture in captureHistory)DeleteCaptureDraft(capture);
        QueueDraftOperation(()=> {
            if(System.IO.Directory.Exists(CaptureDraftFolder))
                foreach(string draft in System.IO.Directory.EnumerateFiles(CaptureDraftFolder,"*.draft"))System.IO.File.Delete(draft);
        });
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
        cloudView=false;
        historyView = ReferenceEquals(sender, GalleryCapturesTab);
        UpdateHistoryView();
        if(!historyView)RefreshGallery();
    }
    private void UpdateHistoryView()
    {
        if (CaptureHistoryPanel is null) return;
        GalleryCloudTab.Tag=cloudView?"active":null;
        CloudPanel.Visibility=cloudView?Visibility.Visible:Visibility.Collapsed;
        GalleryFolderTab.Tag = historyView || cloudView ? null : "active";
        GalleryCapturesTab.Tag = historyView && !cloudView ? "active" : null;
        CaptureHistoryPanel.Visibility = historyView && !cloudView ? Visibility.Visible : Visibility.Collapsed;
        GalleryList.Visibility = historyView || cloudView ? Visibility.Collapsed : Visibility.Visible;
        GalleryFolderControls.Visibility = historyView || cloudView ? Visibility.Collapsed : Visibility.Visible;
        GalleryMessage.Visibility = historyView || cloudView ? Visibility.Collapsed : Visibility.Visible;
    }
}
