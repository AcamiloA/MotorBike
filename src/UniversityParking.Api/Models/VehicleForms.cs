using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using UniversityParking.Application.Files;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Api.Models;

public sealed class RegisterVehicleForm
{
    [Required] public string Type { get; set; } = "";
    public string? Plate { get; set; }
    public string? FrameNumber { get; set; }
    [Required, StringLength(100)] public string Brand { get; set; } = "";
    [Required, StringLength(100)] public string Model { get; set; } = "";
    [Required, StringLength(100)] public string Color { get; set; } = "";
    public List<VehiclePhotoForm> Photos { get; set; } = [];
    public List<VehicleDocumentForm> Documents { get; set; } = [];
}
public sealed class RenewVehicleForm
{
    public List<VehicleDocumentForm> Documents { get; set; } = [];
}
public sealed class VehiclePhotoForm
{
    [Required] public string Type { get; set; } = "";
    [Required] public IFormFile File { get; set; } = null!;
}
public sealed class VehicleDocumentForm
{
    [Required] public string Type { get; set; } = "";
    [StringLength(100)] public string? DocumentNumber { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    [Required] public IFormFile File { get; set; } = null!;
}
internal static class VehicleMultipart
{
    public static bool ValidFields(IFormCollection form, bool renewal)
    {
        var keys = form.Keys.Concat(form.Files.Select(x => x.Name)).ToArray();
        foreach (var key in keys)
        {
            if (!renewal && new[] { "type", "plate", "frameNumber", "brand", "model", "color" }.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
            var match = Regex.Match(key, @"^(Photos|Documents)\[(\d+)\]\.(Type|File|DocumentNumber|IssuedOn|ExpiresOn)$", RegexOptions.IgnoreCase);
            if (!match.Success || renewal && match.Groups[1].Value.Equals("Photos", StringComparison.OrdinalIgnoreCase) ||
                match.Groups[1].Value.Equals("Photos", StringComparison.OrdinalIgnoreCase) && !new[] { "Type", "File" }.Contains(match.Groups[3].Value, StringComparer.OrdinalIgnoreCase)) return false;
        }
        foreach (var category in new[] { "Photos", "Documents" })
        {
            var indexes = keys.Select(key => Regex.Match(key, $@"^{category}\[(\d+)\]", RegexOptions.IgnoreCase))
                .Where(x => x.Success).Select(x => x.Groups[1].Value).Distinct().ToArray();
            if (indexes.Any(x => !int.TryParse(x, out _))) return false;
            var ordered = indexes.Select(int.Parse).Order().ToArray();
            if (!ordered.SequenceEqual(Enumerable.Range(0, ordered.Length))) return false;
        }
        return true;
    }
    public static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var result) && Enum.IsDefined(result) && result.ToString() == value ? result : (T)Enum.ToObject(typeof(T), 999);
    public static UploadSource File(IFormFile file) => new(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
    public static DocumentUpload Document(VehicleDocumentForm form) => new(Parse<VehicleDocumentType>(form.Type), File(form.File), form.DocumentNumber, form.IssuedOn, form.ExpiresOn);
}
