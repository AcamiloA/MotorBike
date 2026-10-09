namespace UniversityParking.Mobile.Core;

// Shared gesture mathematics; the page only applies these values to the original image.
public sealed class EvidenceViewport
{
    public double Scale { get; private set; } = 1;
    public double X { get; private set; }
    public double Y { get; private set; }
    public void Reset() { Scale = 1; X = Y = 0; }
    public void Zoom(double factor, double originX, double originY, double width, double height)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var previous = Scale; Scale = Math.Clamp(Scale * factor, 1, 8);
        var ratio = Scale / previous;
        X = X * ratio - (Math.Clamp(originX, 0, 1) - .5) * width * (ratio - 1);
        Y = Y * ratio - (Math.Clamp(originY, 0, 1) - .5) * height * (ratio - 1);
        Clamp(width, height);
    }
    public void Pan(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        X = x; Y = y; Clamp(width, height);
    }
    private void Clamp(double width, double height)
    {
        X = Math.Clamp(X, -Math.Max(0, width * (Scale - 1) / 2), Math.Max(0, width * (Scale - 1) / 2));
        Y = Math.Clamp(Y, -Math.Max(0, height * (Scale - 1) / 2), Math.Max(0, height * (Scale - 1) / 2));
    }
}
