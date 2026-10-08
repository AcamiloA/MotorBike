using UniversityParking.Api.E2E.Tests.Auth;
using UniversityParking.Domain.AcademicPeriods;

// Isolated validation host only: random JWT, disposable PostgreSQL and private temporary files.
var fixture = new AuthApiFixture();
try
{
    await fixture.InitializeAsync();
    var account = await fixture.CreateUserAsync("USER", "ADMIN");
    await using (var context = fixture.CreateContext())
    {
        var period = new AcademicPeriod("Swagger validation", new(2026, 1, 1), new(2026, 12, 31), DateTimeOffset.UtcNow);
        period.Activate(); context.AcademicPeriods.Add(period); await context.SaveChangesAsync();
    }
    await using var host = fixture.CreateFactory(settings: new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" });
    host.UseKestrel(5315);
    using var client = host.CreateClient();
    Console.WriteLine("SWAGGER_VALIDATION_READY http://localhost:5315/swagger/index.html");
    Console.WriteLine($"TEST_IDENTIFICATION={account.IdentificationNumber.Value}");
    Console.WriteLine($"TEST_PASSWORD={AuthApiFixture.Password}");
    Console.WriteLine("Disposable test credentials only. Press Enter to stop and clean up.");
    Console.ReadLine();
}
finally { await fixture.DisposeAsync(); }
