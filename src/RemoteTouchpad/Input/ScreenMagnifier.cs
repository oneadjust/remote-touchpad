using System.Drawing.Imaging;

namespace RemoteTouchpad.Input;

public sealed class ScreenMagnifier
{
    public byte[] CaptureAroundCursor(int size, int scale)
    {
        var captureSize = Math.Clamp(size, 80, 420);
        var outputScale = Math.Clamp(scale, 1, 4);
        var cursor = Cursor.Position;
        var bounds = GetVirtualScreenBounds();
        var source = new Rectangle(
            cursor.X - captureSize / 2,
            cursor.Y - captureSize / 2,
            captureSize,
            captureSize);
        source = ClampRectangle(source, bounds);

        using var sourceBitmap = new Bitmap(source.Width, source.Height);
        using (var graphics = Graphics.FromImage(sourceBitmap))
        {
            graphics.CopyFromScreen(source.Location, Point.Empty, source.Size);
        }

        using var scaledBitmap = new Bitmap(source.Width * outputScale, source.Height * outputScale);
        using (var graphics = Graphics.FromImage(scaledBitmap))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            graphics.DrawImage(sourceBitmap, new Rectangle(Point.Empty, scaledBitmap.Size));
        }

        using var stream = new MemoryStream();
        scaledBitmap.Save(stream, ImageFormat.Jpeg);
        return stream.ToArray();
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        return SystemInformation.VirtualScreen;
    }

    private static Rectangle ClampRectangle(Rectangle rectangle, Rectangle bounds)
    {
        var width = Math.Min(rectangle.Width, bounds.Width);
        var height = Math.Min(rectangle.Height, bounds.Height);
        var left = Math.Clamp(rectangle.Left, bounds.Left, bounds.Right - width);
        var top = Math.Clamp(rectangle.Top, bounds.Top, bounds.Bottom - height);
        return new Rectangle(left, top, width, height);
    }
}
