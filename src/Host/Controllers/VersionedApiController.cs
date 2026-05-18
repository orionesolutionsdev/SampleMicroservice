using Microsoft.AspNetCore.Mvc;

namespace SampleMicroservice.Host.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public abstract class VersionedApiController : BaseApiController
{
}
