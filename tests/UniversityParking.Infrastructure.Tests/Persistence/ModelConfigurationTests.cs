using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Infrastructure.Tests.Persistence;

public sealed class ModelConfigurationTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=model_test;Username=model_test").Options);

    [Fact]
    public void NpgsqlModel_ShouldBuildAllSpecifiedTables_WithoutConnectingToDatabase()
    {
        using var context = CreateContext();
        var expected = new[] { "users", "user_credentials", "roles", "user_roles", "vehicles", "vehicle_ownerships",
            "vehicle_registrations", "vehicle_photos", "vehicle_documents", "academic_periods", "parking_lots",
            "parking_zones", "parking_movements", "incidents", "incident_attachments", "news", "audit_logs" };
        Assert.Equal(expected.Order(), context.Model.GetEntityTypes().Select(x => x.GetTableName()!).Order());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void Columns_ShouldUseSnakeCase_AndNotIntroduceShadowState()
    {
        using var context = CreateContext();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            Assert.NotNull(entity.FindPrimaryKey());
            foreach (var property in entity.GetProperties())
            {
                Assert.False(property.IsShadowProperty(), $"Unexpected shadow property: {entity.Name}.{property.Name}");
                Assert.Matches("^[a-z][a-z0-9_]*$", property.GetColumnName());
            }
        }
        Assert.Null(context.Model.FindEntityType(typeof(Vehicle))!.FindProperty("UserId"));
        Assert.Null(context.Model.FindEntityType(typeof(Vehicle))!.FindProperty("IsInside"));
    }

    [Fact]
    public void Identifiers_ShouldConvertToNormalizedStrings_AndKeepNullableVehicleFields()
    {
        using var context = CreateContext();
        var user = context.Model.FindEntityType(typeof(User))!;
        var identification = user.FindProperty(nameof(User.IdentificationNumber))!;
        var card = user.FindProperty(nameof(User.CardCode))!;
        Assert.Equal("001A", identification.GetTypeMapping().Converter!.ConvertToProvider(new IdentificationNumber(" 001A ")));
        Assert.Equal(new IdentificationNumber("001A"), identification.GetTypeMapping().Converter!.ConvertFromProvider("001A"));
        Assert.Equal("Ab-12", card.GetTypeMapping().Converter!.ConvertToProvider(new CardCode(" Ab-12 ")));
        var vehicle = context.Model.FindEntityType(typeof(Vehicle))!;
        var plate = vehicle.FindProperty(nameof(Vehicle.Plate))!;
        var frame = vehicle.FindProperty(nameof(Vehicle.FrameNumber))!;
        Assert.Equal("ABC123", plate.GetTypeMapping().Converter!.ConvertToProvider(new VehiclePlate("abc-123")));
        Assert.Equal("AB-12", frame.GetTypeMapping().Converter!.ConvertToProvider(new FrameNumber(" ab-12 ")));
        Assert.True(plate.IsNullable);
        Assert.True(frame.IsNullable);
        Assert.Null(plate.GetTypeMapping().Converter!.ConvertToProvider(null));
    }

    [Fact]
    public void EnumsAndTemporalFields_ShouldUseSpecifiedPostgresRepresentations()
    {
        using var context = CreateContext();
        foreach (var property in context.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type.IsEnum)
            {
                Assert.Equal(typeof(string), property.GetTypeMapping().Converter!.ProviderClrType);
                Assert.NotNull(property.GetMaxLength());
            }
            else if (type == typeof(DateTimeOffset)) Assert.Equal("timestamp with time zone", property.GetColumnType());
            else if (type == typeof(DateOnly)) Assert.Equal("date", property.GetColumnType());
            else if (type == typeof(TimeOnly)) Assert.Equal("time without time zone", property.GetColumnType());
            else if (type == typeof(Guid)) Assert.Equal("uuid", property.GetColumnType());
        }
    }

    [Fact]
    public void HistoricalForeignKeys_ShouldRestrictDeletion_AndCredentialsShouldBeOneToOne()
    {
        using var context = CreateContext();
        foreach (var foreignKey in context.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        var credentials = context.Model.FindEntityType(typeof(UserCredential))!;
        Assert.True(Assert.Single(credentials.GetForeignKeys()).IsUnique);
        Assert.Equal(nameof(UserCredential.UserId), Assert.Single(credentials.FindPrimaryKey()!.Properties).Name);
    }

    [Fact]
    public void ImmutableAuditProperties_ShouldMapToJsonbAndNullableActor()
    {
        using var context = CreateContext();
        var audit = context.Model.FindEntityType(typeof(AuditLog))!;
        Assert.Equal("jsonb", audit.FindProperty(nameof(AuditLog.OldValues))!.GetColumnType());
        Assert.Equal("jsonb", audit.FindProperty(nameof(AuditLog.NewValues))!.GetColumnType());
        Assert.True(audit.FindProperty(nameof(AuditLog.ActorUserId))!.IsNullable);
        Assert.Equal(10, audit.GetProperties().Count());
    }

    [Fact]
    public void RegistrationMapping_ShouldUseOwnerTriplet_WithoutGlobalActiveIndex()
    {
        using var context = CreateContext();
        var registration = context.Model.FindEntityType(typeof(VehicleRegistration))!;
        var unique = Assert.Single(registration.GetIndexes(), x => x.IsUnique);
        Assert.Equal(new[] { "VehicleId", "UserId", "AcademicPeriodId" }, unique.Properties.Select(x => x.Name));
        Assert.Null(unique.GetFilter());
    }

    [Fact]
    public void Queries_ShouldTranslateConvertedIdentifiers_AndImmutableAuditShape()
    {
        using var context = CreateContext();
        var number = new IdentificationNumber(" 001A ");
        var sql = context.Users.Where(user => user.IdentificationNumber == number).ToQueryString();
        Assert.Contains("identification_number", sql);
        Assert.Contains("001A", sql);
        Assert.Contains("audit_logs", context.AuditLogs.ToQueryString());
        Assert.Contains("password_hash", context.UserCredentials.ToQueryString());
    }
}
