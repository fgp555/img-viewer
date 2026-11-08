namespace IMGViewer;

internal enum ImageResizeMode { Scale, MaxSide, Width, Height }

internal readonly record struct ImageResizeRequest(ImageResizeMode Mode, double Value)
{
    internal (int Width, int Height) GetSize(int width, int height)
    {
        double scale = Mode switch
        {
            ImageResizeMode.MaxSide => Math.Min(1, Value / Math.Max(width, height)),
            ImageResizeMode.Width => Value / width,
            ImageResizeMode.Height => Value / height,
            _ => Value
        };
        return (RoundPixels(width * scale), RoundPixels(height * scale));
    }

    internal static bool IsAllowed(int width, int height) =>
        width is >= 1 and <= 16384 && height is >= 1 and <= 16384 && (long)width * height <= 100_000_000;

    private static int RoundPixels(double pixels) =>
        (int)Math.Clamp(Math.Round(pixels, MidpointRounding.AwayFromZero), 1, int.MaxValue);
}
