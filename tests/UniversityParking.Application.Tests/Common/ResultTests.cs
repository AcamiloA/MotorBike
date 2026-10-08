using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldHaveNoError_AndExposeValue()
    {
        var result = Result<string>.Success("Vehículo registrado");
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Equal("Vehículo registrado", result.Value);
        Assert.True(Result.Success().IsSuccess);
        Assert.Null(Result.Success().Error);
    }

    [Fact]
    public void Failure_ShouldPreserveFunctionalError_AndRejectValueAccess()
    {
        var result = Result<Guid>.Failure(VehicleErrors.StudentCannotRegisterCar);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("STUDENT_CANNOT_REGISTER_CAR", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal(VehicleErrors.StudentCannotRegisterCar, Result.Failure(VehicleErrors.StudentCannotRegisterCar).Error);
    }

    [Fact]
    public void Failure_ShouldRejectMissingError()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void ValidationError_ShouldSnapshotMessages()
    {
        var messages = new[] { "El nombre es obligatorio." };
        var fields = new Dictionary<string, IReadOnlyList<string>> { ["FullName"] = messages };
        var error = CommonErrors.Validation(fields);
        messages[0] = "Changed";
        fields.Clear();
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Equal("El nombre es obligatorio.", Assert.Single(error.ValidationErrors["FullName"]));
    }
}
