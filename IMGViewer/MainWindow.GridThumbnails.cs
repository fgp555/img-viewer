using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private const long ThumbnailCacheLimit = 24 * 1024 * 1024;
    private readonly SemaphoreSlim thumbnailSlots = new(2);
    private readonly Dictionary<string, LinkedListNode<ThumbnailEntry>> thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<ThumbnailEntry> thumbnailRecency = new();
    private long thumbnailCacheBytes;

    private sealed record ThumbnailRequest(GridPhoto Photo, CancellationTokenSource Lifetime);
    private sealed record ThumbnailEntry(string Path, long Length, DateTime LastWrite, BitmapSource Bitmap, long Bytes);

    private void GridThumbnail_Loaded(object sender, RoutedEventArgs e) => RefreshGridThumbnail((Image)sender);

    private void GridThumbnail_Unloaded(object sender, RoutedEventArgs e) => CancelGridThumbnail((Image)sender);

    private void GridThumbnail_Changed(object sender, DependencyPropertyChangedEventArgs e) => RefreshGridThumbnail((Image)sender);

    private static void CancelGridThumbnail(Image image)
    {
        if (image.Tag is ThumbnailRequest request)
        {
            image.Tag = null;
            request.Lifetime.Cancel();
        }

        image.Source = null;
    }

    private async void RefreshGridThumbnail(Image image)
    {
        if (!image.IsLoaded || !image.IsVisible || image.DataContext is not GridPhoto photo ||
            gridLoad is not { IsCancellationRequested: false } load)
        {
            CancelGridThumbnail(image);
            return;
        }

        if (image.Tag is ThumbnailRequest existing && ReferenceEquals(existing.Photo, photo))
        {
            return;
        }

        CancelGridThumbnail(image);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(load.Token);
        var request = new ThumbnailRequest(photo, lifetime);
        image.Tag = request;
        CancellationToken token = lifetime.Token;
        try
        {
            await thumbnailSlots.WaitAsync(token);
            try
            {
                BitmapSource bitmap = await Task.Run(() => ReadCachedThumbnail(photo.Path, token), token);
                if (!token.IsCancellationRequested && ReferenceEquals(image.Tag, request))
                {
                    image.Source = bitmap;
                }
            }
            finally
            {
                thumbnailSlots.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (IsBatchImageError(ex)) { }
        finally
        {
            if (ReferenceEquals(image.Tag, request))
            {
                image.Tag = null;
            }
        }
    }

    private BitmapSource ReadCachedThumbnail(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        long length = file.Length;
        DateTime lastWrite = file.LastWriteTimeUtc;
        lock (thumbnailCache)
        {
            if (thumbnailCache.TryGetValue(path, out var existing))
            {
                if (existing.Value.Length == length && existing.Value.LastWrite == lastWrite)
                {
                    thumbnailRecency.Remove(existing);
                    thumbnailRecency.AddFirst(existing);
                    return existing.Value.Bitmap;
                }

                RemoveCachedThumbnail(existing);
            }
        }

        BitmapSource bitmap;
        if (IsSvg(path) || IsWebP(path))
        {
            bitmap = ReadPhotoBitmap(path, 384);
        }
        else
        {
        using var stream = File.OpenRead(path);
        BitmapFrame header = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        bool portrait = header.PixelHeight > header.PixelWidth;
        stream.Position = 0;

        var raster = new BitmapImage();
        raster.BeginInit();
        raster.CacheOption = BitmapCacheOption.OnLoad;
        if (portrait)
        {
            raster.DecodePixelHeight = 384;
        }
        else
        {
            raster.DecodePixelWidth = 384;
        }

        raster.StreamSource = stream;
        raster.EndInit();
        raster.Freeze();
        bitmap = raster;


        }
        token.ThrowIfCancellationRequested();
        long bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * Math.Max(4, (bitmap.Format.BitsPerPixel + 7) / 8);
        lock (thumbnailCache)
        {
            if (thumbnailCache.TryGetValue(path, out var existing))
            {
                RemoveCachedThumbnail(existing);
            }

            var entry = new ThumbnailEntry(path, length, lastWrite, bitmap, bytes);
            thumbnailCache[path] = thumbnailRecency.AddFirst(entry);
            thumbnailCacheBytes += bytes;
            while (thumbnailCache.Count > 128 || thumbnailCacheBytes > ThumbnailCacheLimit)
            {
                RemoveCachedThumbnail(thumbnailRecency.Last!);
            }
        }

        return bitmap;
    }

    private void RemoveCachedThumbnail(LinkedListNode<ThumbnailEntry> node)
    {
        thumbnailCacheBytes -= node.Value.Bytes;
        thumbnailCache.Remove(node.Value.Path);
        thumbnailRecency.Remove(node);
    }
}
