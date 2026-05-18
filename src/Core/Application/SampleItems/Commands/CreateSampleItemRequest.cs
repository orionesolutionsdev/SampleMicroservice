using FluentValidation;
using Mapster;
using MediatR;
using SampleMicroservice.Application.Common.Models;
using SampleMicroservice.Application.Common.Persistence;
using SampleMicroservice.Application.Common.Validation;
using SampleMicroservice.Application.SampleItems.DTOs;
using SampleMicroservice.Domain.SampleItems;

namespace SampleMicroservice.Application.SampleItems.Commands;

public class CreateSampleItemRequest : IRequest<ApiResponse<SampleItemDto>>
{
    public string Name { get; set; } = default!;
}

public class CreateSampleItemRequestValidator : CustomValidator<CreateSampleItemRequest>
{
    public CreateSampleItemRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(256);
    }
}

public class CreateSampleItemRequestHandler : IRequestHandler<CreateSampleItemRequest, ApiResponse<SampleItemDto>>
{
    private readonly IRepository<SampleItem> _repository;

    public CreateSampleItemRequestHandler(IRepository<SampleItem> repository) =>
        _repository = repository;

    public async Task<ApiResponse<SampleItemDto>> Handle(CreateSampleItemRequest request, CancellationToken cancellationToken)
    {
        var item = SampleItem.Create(request.Name);
        await _repository.AddAsync(item, cancellationToken);
        return new ApiResponse<SampleItemDto>("Sample item created successfully.", item.Adapt<SampleItemDto>());
    }
}
