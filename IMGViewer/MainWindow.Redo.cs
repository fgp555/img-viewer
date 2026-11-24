using System.Windows;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private sealed record EditState(BitmapSource? Photo,BitmapSource? Saved,BitmapSource? RotationBase,int Rotation,bool PixelEdit,
        StyledMarker[] Marks,int Selected,PastedTransformUndo[] Transforms,PastedDeleteUndo[] Deletes,CropUndoState[] Crops,ElementEditUndo[] Properties);
    private sealed record RedoEntry(EditState Before,EditState After);
    private readonly Stack<RedoEntry> redoEdits=[];
    private bool replayingEdit;
    private EditState CaptureEditState()=>new(Photo.Source as BitmapSource,lastSavedPhoto,rotationBasePhoto,rotationDegrees,hasPixelEdit,
        markers.ToArray(),selectedPastedImage,pastedTransformUndo.ToArray(),pastedDeleteUndo.ToArray(),cropUndo.ToArray(),elementEdits.ToArray());
    private bool MatchesEditState(EditState state)=>ReferenceEquals(Photo.Source,state.Photo)&&rotationDegrees==state.Rotation&&hasPixelEdit==state.PixelEdit&&SameMarks(state.Marks);
    private bool CanRedoEdit=>!IsFolderGridOpen&&redoEdits.TryPeek(out var entry)&&MatchesEditState(entry.Before);
    private static void RestoreStack<T>(Stack<T> stack,T[] entries){stack.Clear();foreach(var entry in entries.Reverse())stack.Push(entry);}
    private void RestoreEditState(EditState state)
    {
        EndInlineText(false);CancelInlineCrop();if(IsMouseCaptured)ReleaseMouseCapture();
        Photo.Source=state.Photo;pastedAdornerBase=state.Photo;lastSavedPhoto=state.Saved;rotationBasePhoto=state.RotationBase;
        rotationDegrees=state.Rotation;hasPixelEdit=state.PixelEdit;
        markers.Clear();markers.AddRange(state.Marks);selectedPastedImage=state.Selected;
        RestoreStack(pastedTransformUndo,state.Transforms);RestoreStack(pastedDeleteUndo,state.Deletes);RestoreStack(cropUndo,state.Crops);RestoreStack(elementEdits,state.Properties);
        ResetPhotoView();RefreshMarkerOverlay();SyncSelectedElementProperties();UpdateMetadataFooter();UpdatePendingSaveIndicator();
    }
    private void UndoWorkspace()
    {
        if(IsFolderGridOpen){RestorePhoto();return;}
        bool editable=CanUndoElementEdit||CanUndoPastedDelete||CanUndoPastedTransform||markers.Count>0||CanUndoCrop;
        if(!editable){RestorePhoto();return;}
        var before=CaptureEditState();replayingEdit=true;
        try{UndoWorkspaceCore();}
        finally{replayingEdit=false;}
        if(!MatchesEditState(before))
        {
            if(redoEdits.Count>=32)redoEdits.Clear();
            redoEdits.Push(new(CaptureEditState(),before));
        }
        UpdateToolbarActions();
    }
    private void RedoWorkspace()
    {
        if(!CanRedoEdit)return;
        var entry=redoEdits.Pop();replayingEdit=true;
        try{RestoreEditState(entry.After);}
        finally{replayingEdit=false;}
        StatusText.Text="Acción rehecha.";UpdateToolbarActions();
    }
    private void InvalidateRedoBranch()
    {
        if(!replayingEdit&&redoEdits.TryPeek(out var entry)&&!MatchesEditState(entry.Before))redoEdits.Clear();
    }
}
