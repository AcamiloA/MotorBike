namespace UniversityParking.Application.Universities.Integration;

public sealed class UniversityIntegrationsOptions
{
    public const string SectionName = "UniversityIntegrations";
    public List<UniversityIntegrationOptions> Universities { get; set; } = [];
}

public sealed class UniversityIntegrationOptions
{
    public Guid UniversityId { get; set; }
    public bool Enabled { get; set; } = false;
}
