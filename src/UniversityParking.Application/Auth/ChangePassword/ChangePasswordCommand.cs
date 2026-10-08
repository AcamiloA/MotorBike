using UniversityParking.Application.Common.Messaging;

namespace UniversityParking.Application.Auth.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
