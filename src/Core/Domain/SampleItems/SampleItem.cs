using SampleMicroservice.Domain.Common;

namespace SampleMicroservice.Domain.SampleItems;

public class SampleItem : AuditableEntity<Guid>, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public DateTime CreatedDate { get; private set; }

    private SampleItem() { }

    public static SampleItem Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CreatedDate = DateTime.UtcNow
    };

    public void Update(string name)
    {
        Name = name;
    }
}
