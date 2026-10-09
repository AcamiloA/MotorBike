using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Universities;

namespace UniversityParking.Mobile.Core;

public interface IPublicAuthNavigation
{
    Task ShowStudentRegistrationAsync();
    Task ReturnToLoginAsync(string? message = null);
    Task ShowPasswordRecoveryAsync(Guid? challengeId=null,string? token=null)=>throw new NotSupportedException();
}

public sealed class StudentRegistrationApiService(ApiClient api)
{
    public Task<ApiResult<UniversityResponse[]>> UniversitiesAsync() => api.GetAsync<UniversityResponse[]>("api/v1/universities");
    public Task<ApiResult<RegisterStudentResponse>> RegisterAsync(RegisterStudentRequest request) => api.PostAsync<RegisterStudentResponse>("api/v1/auth/register/student", request);
}

public static class PasswordPresentation
{
    public static bool IsValid(string? value) => value is { Length: >= 8 }
        && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit);
}
