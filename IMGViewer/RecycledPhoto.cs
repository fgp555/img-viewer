using System.IO;
using System.Runtime.InteropServices;

namespace IMGViewer;

internal sealed class RecycledPhoto(string originalPath, RecycledPhoto.IShellItem item)
{
    public string OriginalPath { get; } = originalPath;

    private const uint RecycleOnDelete = 0x00080000;
    private const uint WantPermanentDeleteWarning = 0x00004000;
    private const uint Silent = 0x00000004;
    private const uint NoErrorUi = 0x00000400;
    private static readonly Guid FileOperationClass = new("3AD05575-8857-4850-9277-11B85BDB8E09");

    public static RecycledPhoto Recycle(string path, nint owner)
    {
        var sink = new DeleteProgress();
        var operation = CreateOperation(owner, RecycleOnDelete | WantPermanentDeleteWarning | Silent | NoErrorUi);
        operation.DeleteItem(CreateItem(path), sink);
        Perform(operation);
        return new RecycledPhoto(path, sink.RecycledItem ?? throw new IOException("No se obtuvo el elemento de la Papelera."));
    }

    public void Restore(nint owner)
    {
        if (Path.Exists(OriginalPath))
        {
            throw new IOException("Ya existe un archivo en la ruta original.");
        }

        var operation = CreateOperation(owner, Silent | NoErrorUi);
        operation.MoveItem(item, CreateItem(Path.GetDirectoryName(OriginalPath)!), Path.GetFileName(OriginalPath), null);
        Perform(operation);
        if (!File.Exists(OriginalPath))
        {
            throw new IOException("No se pudo restaurar el archivo.");
        }
    }

    private static IFileOperation CreateOperation(nint owner, uint flags)
    {
        var operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(FileOperationClass, throwOnError: true)!)!;
        operation.SetOwnerWindow(owner);
        operation.SetOperationFlags(flags);
        return operation;
    }

    private static void Perform(IFileOperation operation)
    {
        operation.PerformOperations();
        operation.GetAnyOperationsAborted(out bool aborted);
        if (aborted)
        {
            throw new OperationCanceledException();
        }
    }

    private static IShellItem CreateItem(string path)
    {
        Guid iid = typeof(IShellItem).GUID;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, ref iid, out IShellItem result));
        return result;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, out IShellItem result);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem { }

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        // Unused methods reserve their slots in the native COM interface.
        void Advise();
        void Unadvise();
        void SetOperationFlags(uint flags);
        void SetProgressMessage();
        void SetProgressDialog();
        void SetProperties();
        void SetOwnerWindow(nint owner);
        void ApplyPropertiesToItem();
        void ApplyPropertiesToItems();
        void RenameItem();
        void RenameItems();
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IProgressSink? sink);
        void MoveItems();
        void CopyItem();
        void CopyItems();
        void DeleteItem(IShellItem item, IProgressSink sink);
        void DeleteItems();
        void NewItem();
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProgressSink
    {
        void StartOperations();
        void FinishOperations(int result);
        void PreRenameItem(uint flags, IShellItem item, nint name);
        void PostRenameItem(uint flags, IShellItem item, nint name, int result, IShellItem? created);
        void PreMoveItem(uint flags, IShellItem item, IShellItem destination, nint name);
        void PostMoveItem(uint flags, IShellItem item, IShellItem destination, nint name, int result, IShellItem? created);
        void PreCopyItem(uint flags, IShellItem item, IShellItem destination, nint name);
        void PostCopyItem(uint flags, IShellItem item, IShellItem destination, nint name, int result, IShellItem? created);
        void PreDeleteItem(uint flags, IShellItem item);
        void PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created);
        void PreNewItem(uint flags, IShellItem destination, nint name);
        void PostNewItem(uint flags, IShellItem destination, nint name, nint template, uint attributes, int result, IShellItem? created);
        void UpdateProgress(uint total, uint completed);
        void ResetTimer();
        void PauseTimer();
        void ResumeTimer();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class DeleteProgress : IProgressSink
    {
        public IShellItem? RecycledItem { get; private set; }
        public void StartOperations() { }
        public void FinishOperations(int result) { }
        public void PreRenameItem(uint flags, IShellItem item, nint name) { }
        public void PostRenameItem(uint flags, IShellItem item, nint name, int result, IShellItem? created) { }
        public void PreMoveItem(uint flags, IShellItem item, IShellItem destination, nint name) { }
        public void PostMoveItem(uint flags, IShellItem item, IShellItem destination, nint name, int result, IShellItem? created) { }
        public void PreCopyItem(uint flags, IShellItem item, IShellItem destination, nint name) { }
        public void PostCopyItem(uint flags, IShellItem item, IShellItem destination, nint name, int result, IShellItem? created) { }

        public void PreDeleteItem(uint flags, IShellItem item)
        {
            const uint recycleIfPossible = 0x80;
            if ((flags & recycleIfPossible) == 0)
            {
                throw new OperationCanceledException("Windows no puede reciclar este archivo.");
            }
        }

        public void PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created)
        {
            Marshal.ThrowExceptionForHR(result);
            RecycledItem = created;
        }

        public void PreNewItem(uint flags, IShellItem destination, nint name) { }
        public void PostNewItem(uint flags, IShellItem destination, nint name, nint template, uint attributes, int result, IShellItem? created) { }
        public void UpdateProgress(uint total, uint completed) { }
        public void ResetTimer() { }
        public void PauseTimer() { }
        public void ResumeTimer() { }
    }
}
