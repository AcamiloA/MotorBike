using UniversityParking.Mobile.Core;

namespace UniversityParking.Mobile.Services;

public sealed class GuardSelectionStore : IGuardSelectionStore
{
    private static string Key(Guid user) => $"motobike.guard-lot.{user:D}";
    public Guid? Get(Guid userId) => Guid.TryParse(Preferences.Default.Get(Key(userId), ""), out var id) ? id : null;
    public void Set(Guid userId, Guid? lotId) { if (lotId is null) Preferences.Default.Remove(Key(userId)); else Preferences.Default.Set(Key(userId), lotId.Value.ToString("D")); }
}
public sealed class ScannerPermission : IScannerPermission
{
    public async Task<bool> RequestAsync()
    {
        try { return ZXing.Net.Maui.BarcodeScanning.IsSupported && await Permissions.RequestAsync<Permissions.Camera>() == PermissionStatus.Granted; }
        catch (Exception) { return false; }
    }
}
