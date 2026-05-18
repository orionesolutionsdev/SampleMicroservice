using Mapster;
using MediatR;
using SampleMicroservice.Application.Common.Models;
using SampleMicroservice.Application.Common.Persistence;
using SampleMicroservice.Application.SampleItems.DTOs;
using SampleMicroservice.Domain.SampleItems;

namespace SampleMicroservice.Application.SampleItems.Queries;

public class GetSampleItemsRequest : IRequest<ApiResponse<List<SampleItemDto>>>
{
}

public class GetSampleItemsRequestHandler : IRequestHandler<GetSampleItemsRequest, ApiResponse<List<SampleItemDto>>>
{
    private readonly IReadRepository<SampleItem> _repository;

    public GetSampleItemsRequestHandler(IReadRepository<SampleItem> repository) =>
        _repository = repository;

    public async Task<ApiResponse<List<SampleItemDto>>> Handle(GetSampleItemsRequest request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAsync(cancellationToken);
        var dtos = items.Adapt<List<SampleItemDto>>();
        return new ApiResponse<List<SampleItemDto>>("Sample items retrieved successfully.", dtos);
    }
}
