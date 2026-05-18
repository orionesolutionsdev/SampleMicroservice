using Microsoft.AspNetCore.Mvc;
using SampleMicroservice.Application.Common.Models;
using SampleMicroservice.Application.SampleItems.Commands;
using SampleMicroservice.Application.SampleItems.DTOs;
using SampleMicroservice.Application.SampleItems.Queries;

namespace SampleMicroservice.Host.Controllers.SampleItems;

public class SampleItemsController : VersionedApiController
{
    [HttpGet]
    public Task<ApiResponse<List<SampleItemDto>>> GetAsync()
    {
        return Mediator.Send(new GetSampleItemsRequest());
    }

    [HttpPost]
    public Task<ApiResponse<SampleItemDto>> CreateAsync(CreateSampleItemRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpDelete("{id:guid}")]
    public Task<ApiResponse<Guid>> DeleteAsync(Guid id)
    {
        return Mediator.Send(new DeleteSampleItemRequest(id));
    }
}
