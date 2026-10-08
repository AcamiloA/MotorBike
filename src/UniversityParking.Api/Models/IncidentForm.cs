using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace UniversityParking.Api.Models;

public sealed class IncidentForm
{
    public Guid ParkingLotId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? ParkingMovementId { get; set; }
    [Required] public string Type { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    public DateTimeOffset? OccurredAt { get; set; }
}
internal static class IncidentMultipart
{
    public static bool Valid(IFormCollection form)
    {
        var allowed = new[] { "ParkingLotId", "UserId", "VehicleId", "ParkingMovementId", "Type", "Description", "OccurredAt" };
        if (form.Keys.Any(x => !allowed.Contains(x, StringComparer.OrdinalIgnoreCase) || form[x].Count != 1)) return false;
        var indexes = new List<int>();
        foreach (var file in form.Files)
        {
            var match = Regex.Match(file.Name, @"^Attachments\[(0|[1-9]\d*)\]$", RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var index)) return false;
            indexes.Add(index);
        }
        return indexes.Order().SequenceEqual(Enumerable.Range(0, indexes.Count));
    }
}
