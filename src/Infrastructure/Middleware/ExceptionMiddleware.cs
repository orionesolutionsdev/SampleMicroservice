using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SampleMicroservice.Application.Common.Exceptions;

namespace SampleMicroservice.Infrastructure.Middleware;

public class ExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger) =>
        _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var errorId = Guid.NewGuid().ToString();
            var errorResult = new ErrorResult
            {
                Source = exception.TargetSite?.DeclaringType?.FullName,
                Exception = exception.Message.Trim(),
                ErrorId = errorId,
                SupportMessage = $"Provide the Error Id: {errorId} to the support team for further analysis."
            };
            errorResult.Messages.Add(exception.Message);

            if (exception is not CustomException && exception.InnerException is not null)
            {
                while (exception.InnerException is not null)
                    exception = exception.InnerException;
            }

            switch (exception)
            {
                case CustomException e:
                    errorResult.StatusCode = (int)e.StatusCode;
                    if (e.ErrorMessages is not null)
                        errorResult.Messages = e.ErrorMessages;
                    break;

                case KeyNotFoundException:
                    errorResult.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case ValidationException e:
                    errorResult.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResult.Messages = e.Errors.Select(err => err.ErrorMessage).ToList();
                    break;

                default:
                    errorResult.StatusCode = (int)HttpStatusCode.InternalServerError;
                    break;
            }

            using (LogContext.PushProperty("ErrorId", errorId))
            {
                _logger.LogError("{@ExceptionError} Request failed with Status Code {StatusCode} and Error Id {ErrorId}.",
                    errorResult, errorResult.StatusCode, errorId);
            }

            var response = context.Response;
            if (!response.HasStarted)
            {
                response.ContentType = "application/json";
                response.StatusCode = errorResult.StatusCode;
                await response.WriteAsync(JsonSerializer.Serialize(errorResult));
            }
            else
            {
                _logger.LogWarning("Can't write error response. Response has already started.");
            }
        }
    }
}
