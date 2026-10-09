using UniversityParking.Mobile.ViewModels;

namespace UniversityParking.Mobile.Services;

public sealed class EvidenceImageValidator : IEvidenceImageValidator
{
    public Task<bool> CanDisplayAsync(byte[] bytes) => Task.Run(() =>
    {
        try
        {
            using var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            using var probe = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
            if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return false;
            var sample = 1;
            while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > 2048) sample *= 2;
            using var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
            using var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
            return bitmap is { Width: > 0, Height: > 0 };
        }
        catch (Exception) { return false; }
    });
}
