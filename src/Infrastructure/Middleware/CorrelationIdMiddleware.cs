using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SampleMicroservice.Infrastructure.Middleware;

public class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
