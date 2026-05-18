namespace SampleMicroservice.Application.SampleItems.DTOs;

public class SampleItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public DateTime CreatedDate { get; set; }
}
