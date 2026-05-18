using MediatR;
using SampleMicroservice.Application.Common.Exceptions;
using SampleMicroservice.Application.Common.Models;
using SampleMicroservice.Application.Common.Persistence;
using SampleMicroservice.Domain.SampleItems;

namespace SampleMicroservice.Application.SampleItems.Commands;

public class DeleteSampleItemRequest : IRequest<ApiResponse<Guid>>
{
    public Guid Id { get; set; }

    public DeleteSampleItemRequest(Guid id) => Id = id;
}

public class DeleteSampleItemRequestHandler : IRequestHandler<DeleteSampleItemRequest, ApiResponse<Guid>>
{
    private readonly IRepository<SampleItem> _repository;

    public DeleteSampleItemRequestHandler(IRepository<SampleItem> repository) =>
        _repository = repository;

    public async Task<ApiResponse<Guid>> Handle(DeleteSampleItemRequest request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken);
        _ = item ?? throw new NotFoundException($"SampleItem {request.Id} not found.");

        await _repository.DeleteAsync(item, cancellationToken);
        return new ApiResponse<Guid>("Sample item deleted successfully.", request.Id);
    }
}
