using Microsoft.AspNetCore.Http;
using SampleMicroservice.Infrastructure.Middleware;

namespace SampleMicroservice.Infrastructure.Http;

/// <summary>
/// Forwards the X-Correlation-Id header from the incoming request to any
/// outbound HTTP calls this service makes, keeping the trace chain intact.
/// Register via: services.AddHttpClient("...").AddHttpMessageHandler&lt;CorrelationIdDelegatingHandler&gt;()
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?
            .Request.Headers[CorrelationIdMiddleware.HeaderName]
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
