using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Users;

namespace UniversityParking.Mobile.Core;

public enum StartupState { Login, Authenticated, Unavailable }
public sealed record StartupResult(StartupState State, string? Message = null);
public sealed class AuthService(ApiClient api, IAuthSession session, IAppNavigation navigation)
{
    public async Task<ApiResult<LoginResponse>> LoginAsync(string identification, string password, CancellationToken token = default)
    {
        var result = await api.PostAsync<LoginResponse>("api/v1/auth/login", new LoginRequest(identification.Trim(), password), token);
        if (!result.IsSuccess) return result;
        var login = result.Value!;
        try
        {
            await session.SaveAsync(login.AccessToken, null);
        }
        catch (Exception) { return ApiResult<LoginResponse>.Failure("SESSION_STORAGE_ERROR", "No fue posible guardar la sesión de forma segura."); }
        var restored = await RestoreAsync(token);
        if (restored.State != StartupState.Authenticated)
            return ApiResult<LoginResponse>.Failure("SESSION_VALIDATION_FAILED", restored.Message ?? "No fue posible validar la sesión.");
        return result;
    }
    public async Task<StartupResult> RestoreAsync(CancellationToken token = default)
    {
        string? stored;
        try { stored = await session.GetTokenAsync(); }
        catch (Exception) { return new(StartupState.Unavailable, "No fue posible leer la sesión segura. Intenta nuevamente."); }
        if (string.IsNullOrWhiteSpace(stored)) { await navigation.ShowLoginAsync(); return new(StartupState.Login); }
        var result = await api.GetAsync<UserProfileResponse>("api/v1/users/me", token);
        if (!result.IsSuccess)
        {
            if (result.Error!.HttpStatus == 401) return new(StartupState.Login, result.Error.Message);
            return new(StartupState.Unavailable, result.Error.Message);
        }
        // Do not let an older startup request rebuild a session after logout or another login.
        if (!await session.TrySetUserAsync(stored, result.Value!)) return new(StartupState.Login);
        await navigation.ShowAuthenticatedAsync(); return new(StartupState.Authenticated);
    }
    public async Task LogoutAsync() { await session.ClearAsync(); await navigation.ShowLoginAsync(); }
}
