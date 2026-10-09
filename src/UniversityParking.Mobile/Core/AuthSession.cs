using UniversityParking.Contracts.Users;

namespace UniversityParking.Mobile.Core;

public interface ISecretStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    void Remove(string key);
}
public interface IAuthSession
{
    UserProfileResponse? User { get; }
    Task<string?> GetTokenAsync();
    Task SaveAsync(string token, UserProfileResponse? user);
    Task<bool> TrySetUserAsync(string expectedToken, UserProfileResponse user);
    Task ClearAsync();
    Task<bool> InvalidateAsync(string expectedToken);
}
public interface IAuthSessionNotifications { event EventHandler? Changed; }
public sealed class AuthSession(ISecretStorage storage) : IAuthSession, IAuthSessionNotifications
{
    public event EventHandler? Changed;
    private const string Key = "motobike.access-token";
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private bool loaded;
    public UserProfileResponse? User { get; private set; }
    public async Task<string?> GetTokenAsync()
    {
        await gate.WaitAsync();
        try { if (!loaded) { token = await storage.GetAsync(Key); loaded = true; } return token; }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(string value, UserProfileResponse? user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        await gate.WaitAsync();
        try { await storage.SetAsync(Key, value); token = value; loaded = true; User = user; }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public async Task<bool> TrySetUserAsync(string expectedToken, UserProfileResponse user)
    {
        await gate.WaitAsync();
        try { if (!loaded || token != expectedToken) return false; User = user; }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
    public async Task ClearAsync()
    {
        await gate.WaitAsync();
        try { storage.Remove(Key); token = null; loaded = true; User = null; }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public async Task<bool> InvalidateAsync(string expectedToken)
    {
        await gate.WaitAsync();
        try
        {
            if (!loaded || token != expectedToken) return false;
            storage.Remove(Key); token = null; User = null; loaded = true;
        }
        finally { gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
public interface IAppNavigation
{
    Task ShowLoginAsync(string? message = null);
    Task ShowAuthenticatedAsync();
}
public static class RoleNavigation
{
    public static IReadOnlyList<string> Areas(IReadOnlyCollection<string> roles) =>
        new[] { "USER", "GUARD", "ADMIN" }.Where(roles.Contains).ToArray();
    public static string Title(string role) => role switch { "GUARD" => "Portería", "ADMIN" => "Administración", _ => "Mi cuenta" };
}
