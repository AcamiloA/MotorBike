namespace UniversityParking.Mobile.Core;

public readonly record struct CaptureRect(double X, double Y, double Width, double Height);
public readonly record struct PixelCrop(int X, int Y, int Width, int Height);
public enum PreviewScaling { AspectFill, AspectFit }

public static class DocumentCaptureGeometry
{
    public const double CardAspect = 85.6 / 53.98;
    public static CaptureRect Guide(double width, double height)
    {
        Size(width, height);
        var w = Math.Min(width * .82, height * .82 * CardAspect);
        var h = w / CardAspect;
        return new((width - w) / 2, (height - h) / 2, w, h);
    }
    // Both preview dimensions and the guide use the same coordinate units (native pixels on Android).
    // Source is already upright and cropped to CameraX's shared ViewPort.
    public static PixelCrop Map(int imageWidth, int imageHeight, double previewWidth, double previewHeight,
        CaptureRect guide, PreviewScaling scaling = PreviewScaling.AspectFill)
    {
        Size(imageWidth, imageHeight); Size(previewWidth, previewHeight);
        if (!double.IsFinite(guide.X + guide.Y + guide.Width + guide.Height) || guide.Width <= 0 || guide.Height <= 0) throw new ArgumentException("Recuadro inválido.");
        var scale = scaling == PreviewScaling.AspectFill ? Math.Max(previewWidth / imageWidth, previewHeight / imageHeight)
            : Math.Min(previewWidth / imageWidth, previewHeight / imageHeight);
        var left = (previewWidth - imageWidth * scale) / 2;
        var top = (previewHeight - imageHeight * scale) / 2;
        var x = Math.Clamp((guide.X - left) / scale, 0, imageWidth);
        var y = Math.Clamp((guide.Y - top) / scale, 0, imageHeight);
        var right = Math.Clamp((guide.X + guide.Width - left) / scale, 0, imageWidth);
        var bottom = Math.Clamp((guide.Y + guide.Height - top) / scale, 0, imageHeight);
        if (right <= x || bottom <= y) throw new ArgumentException("El recuadro no intersecta la imagen.");
        var ix = Math.Clamp((int)Math.Ceiling(x), 0, imageWidth - 1);
        var iy = Math.Clamp((int)Math.Ceiling(y), 0, imageHeight - 1);
        var iw = Math.Min(imageWidth - ix, (int)Math.Floor(right) - ix);
        var ih = Math.Min(imageHeight - iy, (int)Math.Floor(bottom) - iy);
        if (iw <= 0 || ih <= 0) throw new ArgumentException("El recuadro no intersecta la imagen.");
        return new(ix, iy, iw, ih);
    }
    public static (int Width, int Height) UprightSize(int width, int height, int rotation)
    {
        Size(width, height);
        return Rotation(rotation) is 90 or 270 ? (height, width) : (width, height);
    }
    public static PixelCrop Rotate(PixelCrop rectangle, int width, int height, int rotation)
    {
        Size(width, height);
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.Width <= 0 || rectangle.Height <= 0 ||
            (long)rectangle.X + rectangle.Width > width || (long)rectangle.Y + rectangle.Height > height) throw new ArgumentException("Crop fuera de bounds.");
        return Rotation(rotation) switch
        {
            90 => new(height - rectangle.Y - rectangle.Height, rectangle.X, rectangle.Height, rectangle.Width),
            180 => new(width - rectangle.X - rectangle.Width, height - rectangle.Y - rectangle.Height, rectangle.Width, rectangle.Height),
            270 => new(rectangle.Y, width - rectangle.X - rectangle.Width, rectangle.Height, rectangle.Width),
            _ => rectangle
        };
    }
    public static (int Width, int Height) OutputSize(int width, int height, int maxEdge = 2048)
    {
        Size(width,height); if (maxEdge <= 0) throw new ArgumentOutOfRangeException(nameof(maxEdge));
        var scale = Math.Min(1d, (double)maxEdge / Math.Max(width,height));
        return (Math.Max(1,(int)Math.Floor(width * scale)),Math.Max(1,(int)Math.Floor(height * scale)));
    }
    public static int Rotation(int degrees) => degrees is 0 or 90 or 180 or 270 ? degrees : throw new ArgumentException("Rotación inválida.");
    private static void Size(double width,double height)
    { if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) throw new ArgumentException("Dimensiones inválidas."); }
}

public enum DocumentCaptureState { CAPTURING, PREVIEW, CONFIRMED, CANCELLED }
public sealed class DocumentCaptureSession
{
    public DocumentCaptureState State { get; private set; } = DocumentCaptureState.CAPTURING;
    public PickedAttachment? Preview { get; private set; }
    public void ShowCrop(PickedAttachment crop)
    {
        if (State != DocumentCaptureState.CAPTURING) throw new InvalidOperationException("La captura ya no está activa.");
        Preview = AttachmentValidation.Validate(crop.FileName,crop.ContentType,crop.Bytes,true); State = DocumentCaptureState.PREVIEW;
    }
    public PickedAttachment Confirm()
    {
        if (State != DocumentCaptureState.PREVIEW || Preview is null) throw new InvalidOperationException("Confirma primero el recorte.");
        State = DocumentCaptureState.CONFIRMED; return Preview;
    }
    public void Repeat()
    {
        if (State != DocumentCaptureState.PREVIEW) throw new InvalidOperationException("No hay preview para repetir.");
        Preview = null; State = DocumentCaptureState.CAPTURING;
    }
    public void Cancel() { Preview = null; State = DocumentCaptureState.CANCELLED; }
}
