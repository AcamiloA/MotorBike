using UniversityParking.Domain.Common;

namespace UniversityParking.Domain.Users;

public sealed class Role : Entity
{
    public string Code { get; private set; }

    public Role(string code)
    {
        if (code is not (RoleCodes.User or RoleCodes.Guard or RoleCodes.Admin))
            throw new DomainException("VALIDATION_ERROR", "El rol no es válido.");
        Code = code;
    }
}
