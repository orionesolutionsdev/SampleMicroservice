using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SampleMicroservice.Host.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
