namespace UniversityParking.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; }

    protected Entity() => Id = Guid.NewGuid();

    // Reference data can supply a stable identity without relying on persistence APIs.
    protected Entity(Guid id) => Id = Guard.Id(id, "entidad");
}
