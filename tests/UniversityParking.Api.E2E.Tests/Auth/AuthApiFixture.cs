using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Infrastructure.Persistence;

namespace UniversityParking.Api.E2E.Tests.Auth;

public sealed class AuthApiFixture : IAsyncLifetime
{
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), "MotorBike-e2e-" + Guid.NewGuid().ToString("N"));
    public const string Password = "TestPassword1";
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("auth_e2e_tests").WithUsername("test_user").WithPassword("ContainerOnlyPassword1").Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(container.GetConnectionString()).Options);
    public WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "Local", ["Storage:LocalRootPath"] = storageRoot
        };
        if (settings is not null) foreach (var pair in settings) configuration[pair.Key] = pair.Value;
        return new AuthFactory(container.GetConnectionString(), configureServices, configuration);
    }

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        Factory = CreateFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async Task ResetAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        RemoveTestFiles();
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE audit_logs, incident_attachments, incidents, news, parking_movements, parking_zones, parking_lots, vehicle_documents, vehicle_photos, vehicle_registrations, vehicle_ownerships, vehicles, academic_periods, user_credentials, user_roles, users, roles CASCADE");
        Factory = CreateFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async Task<User> CreateUserAsync(params string[] roleCodes)
    {
        await using var context = CreateContext();
        var user = new User(new IdentificationNumber(Guid.NewGuid().ToString("N")), "Usuario E2E", "ETITC", null,
            MemberType.STAFF, new CardCode(Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow);
        context.Users.Add(user);
        var hasher = Factory.Services.GetRequiredService<IPasswordHasher>();
        context.UserCredentials.Add(new UserCredential(user.Id, hasher.Hash(Password), user.CreatedAt));
        foreach (var code in roleCodes)
        {
            var role = await context.Roles.FirstOrDefaultAsync(x => x.Code == code);
            if (role is null)
            {
                role = new Role(code);
                context.Roles.Add(role);
            }
            context.UserRoles.Add(new UserRole(user.Id, role.Id));
        }
        await context.SaveChangesAsync();
        return user;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        await container.DisposeAsync();
        RemoveTestFiles();
    }

    private void RemoveTestFiles()
    {
        var full = Path.GetFullPath(storageRoot);
        var prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "MotorBike-e2e-");
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected E2E directory.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }

    private sealed class AuthFactory(string connectionString, Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["Jwt:Issuer"] = "UniversityParking.Api",
                ["Jwt:Audience"] = "UniversityParking.Mobile",
                ["Jwt:ExpirationMinutes"] = "480"
            };
            if (settings is not null)
                foreach (var pair in settings) values[pair.Key] = pair.Value;
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(values));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UniversityParking.sln"))) directory = directory.Parent;
            if (directory is null) throw new InvalidOperationException("MotorBike solution root was not found.");
            builder.UseContentRoot(Path.Combine(directory.FullName, "src", "UniversityParking.Api"));
            if (configureServices is not null) builder.ConfigureServices(configureServices);
        }
    }
}

[CollectionDefinition("Authentication API", DisableParallelization = true)]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFixture>;

