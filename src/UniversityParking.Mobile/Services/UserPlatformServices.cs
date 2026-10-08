using UniversityParking.Mobile.Core;
using Microsoft.Extensions.Logging;

namespace UniversityParking.Mobile.Services;

public sealed class UserNavigation : IUserNavigation
{
    public Task GoAsync(string route, IReadOnlyDictionary<string, object>? arguments = null) => MainThread.InvokeOnMainThreadAsync(() =>
        Shell.Current.GoToAsync(route switch { "guard-home" => "//app/guard/home", "my-vehicles" => "//app/user/vehicles", "my-history" => "//app/user/history", "user-news" => "//app/user/news", "user-profile" => "//app/user/profile", _ => route },
            arguments is null ? new Dictionary<string, object>() : new Dictionary<string, object>(arguments)));
    public Task BackAsync() => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
    public Task MessageAsync(string title, string message) => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.CurrentPage.DisplayAlertAsync(title, message, "ACEPTAR"));
    public Task<bool> ConfirmAsync(string title, string message) => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.CurrentPage.DisplayAlertAsync(title, message, "CONFIRMAR", "CANCELAR"));
}
public sealed class AttachmentPicker : IAttachmentPicker
{
    public async Task<PickedAttachment?> PhotoAsync(bool camera)
    {
        try
        {
            FileResult? result;
            if (camera)
            {
                if (!MediaPicker.Default.IsCaptureSupported) throw new UserInputException("La cámara no está disponible. Selecciona una imagen.");
                if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted) throw new UserInputException("Permite el acceso a la cámara para tomar una fotografía.");
                result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "Foto GENERAL del vehículo" });
            }
            else result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Seleccionar foto", FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.Android] = ["image/jpeg", "image/png"] }) });
            return result is null ? null : await ReadAsync(result, true);
        }
        catch (UserInputException) { throw; }
        catch (Exception) { throw new UserInputException("No fue posible seleccionar la fotografía."); }
    }
    public async Task<PickedAttachment?> DocumentAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Seleccionar documento",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.Android] = ["application/pdf", "image/jpeg", "image/png"] }) });
            return result is null ? null : await ReadAsync(result, false);
        }
        catch (UserInputException) { throw; }
        catch (Exception) { throw new UserInputException("No fue posible seleccionar el documento."); }
    }
    private static async Task<PickedAttachment> ReadAsync(FileResult file, bool photo)
    {
        var limit = (photo ? 5 : 10) * 1024 * 1024;
        await using var stream = await file.OpenReadAsync(); using var output = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = await stream.ReadAsync(buffer)) > 0)
        { if (output.Length + count > limit) throw new UserInputException(photo ? "La foto supera 5 MB." : "El documento supera 10 MB."); await output.WriteAsync(buffer.AsMemory(0, count)); }
        return AttachmentValidation.Validate(file.FileName, file.ContentType, output.ToArray(), photo);
    }
}
public sealed class FileViewer(ILogger<FileViewer> logger, IAuthSession session) : IFileViewer
{
    private readonly string root = Path.Combine(FileSystem.CacheDirectory, "motobike-document-preview");
    public async Task OpenAsync(string fileName, string contentType, byte[] bytes)
    {
        var token = await session.GetTokenAsync(); if (token is null || session.User is null) return;
        var name = Path.GetFileName(fileName.Replace('\\', '/')); if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) throw new UserInputException("El nombre del documento no es válido.");
        Directory.CreateDirectory(root); var path = Path.Combine(root, Guid.NewGuid().ToString("N") + "-" + name);
        await File.WriteAllBytesAsync(path, bytes);
        if (session.User is null || await session.GetTokenAsync() != token) { File.Delete(path); return; }
        try { if (!await Launcher.Default.OpenAsync(new OpenFileRequest(name, new ReadOnlyFile(path, contentType)))) throw new UserInputException("No hay una aplicación disponible para abrir el documento."); }
        catch { File.Delete(path); throw; }
    }
    public void ClearCache()
    {
        var full = Path.GetFullPath(root); var parent = Path.GetFullPath(FileSystem.CacheDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(parent, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid private preview directory.");
        try { if (Directory.Exists(full)) Directory.Delete(full, true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { logger.LogWarning("No se pudo limpiar la vista previa privada. ExceptionType {ExceptionType}", exception.GetType().Name); }
    }
}
