using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SampleMicroservice.Application.Common.Interfaces;

namespace SampleMicroservice.Infrastructure.Auth;

public class CurrentUser : ICurrentUser, IScopedService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? Name => User?.FindFirstValue(ClaimTypes.Name);

    public Guid GetUserId()
    {
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }

    public string? GetUserEmail() => User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated() => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
