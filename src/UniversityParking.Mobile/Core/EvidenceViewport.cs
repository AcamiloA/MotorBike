namespace UniversityParking.Mobile.Core;

// Viewport coordinates are independent of Android density and image resolution.
public sealed class EvidenceViewport
{
    public double Scale { get; private set; } = 1;
    public double X { get; private set; }
    public double Y { get; private set; }
    public const double InspectionScale = 2.5;
    public bool Pressed { get; private set; }
    public double LimitX { get; private set; }
    public double LimitY { get; private set; }
    private double startX, startY, offsetX, offsetY;
    public void Reset() { Pressed = false; Scale = 1; X = Y = LimitX = LimitY = 0; }
    public void TouchDown(double x, double y, double width, double height, double imageWidth, double imageHeight)
    {
        Reset();
        if (!Valid(x, y) || !Valid(width, height) || !Valid(imageWidth, imageHeight) || width <= 0 || height <= 0 || imageWidth <= 0 || imageHeight <= 0) return;
        var fit = Math.Min(width / imageWidth, height / imageHeight);
        Scale = InspectionScale; Pressed = true;
        LimitX = Math.Max(0, (imageWidth * fit * Scale - width) / 2);
        LimitY = Math.Max(0, (imageHeight * fit * Scale - height) / 2);
        startX = Math.Clamp(x, 0, width); startY = Math.Clamp(y, 0, height);
        offsetX = X = Math.Clamp(-(startX - width / 2) * (Scale - 1), -LimitX, LimitX);
        offsetY = Y = Math.Clamp(-(startY - height / 2) * (Scale - 1), -LimitY, LimitY);
    }
    public void TouchMove(double x, double y)
    {
        if (!Pressed || !Valid(x, y)) return;
        X = Math.Clamp(offsetX + x - startX, -LimitX, LimitX);
        Y = Math.Clamp(offsetY + y - startY, -LimitY, LimitY);
    }
    public void TouchUp() => Reset();
    public void TouchCancel() => Reset();
    private static bool Valid(double x, double y) => double.IsFinite(x) && double.IsFinite(y);
}
