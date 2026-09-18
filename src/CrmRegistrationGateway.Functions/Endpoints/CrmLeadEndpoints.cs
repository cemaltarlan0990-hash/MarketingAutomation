using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CrmRegistrationGateway.Infrastructure;
using CrmRegistrationGateway.Models;
using CrmRegistrationGateway.Services;
using CrmRegistrationGateway.Validation;

namespace CrmRegistrationGateway.Endpoints;

public static class CrmLeadEndpoints
{
    public static IEndpointRouteBuilder MapCrmLeadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/crm/leads", HandleAsync)
            .WithName("CreateCrmLead");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        EventRegistrationValidator validator,
        LeadRegistrationProcessor processor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CrmLeadEndpoint");
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.TraceIdentifier;
        logger.LogInformation("CreateCrmLead endpoint invoked. CorrelationId={CorrelationId}", correlationId);

        try
        {
            if (!HasJsonContentType(context.Request.ContentType))
            {
                logger.LogWarning("Invalid content type. CorrelationId={CorrelationId}", correlationId);
                return Json(
                    HttpStatusCode.UnsupportedMediaType,
                    Error("invalid_request", "Content-Type must be application/json.", correlationId));
            }

            EventRegistrationRequest? registration;
            try
            {
                registration = await JsonSerializer.DeserializeAsync<EventRegistrationRequest>(
                    context.Request.Body,
                    JsonSerializerOptions.Web,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                logger.LogWarning("Invalid JSON request. CorrelationId={CorrelationId}", correlationId);
                return Json(
                    HttpStatusCode.BadRequest,
                    Error("invalid_request", "Request body contains invalid JSON.", correlationId));
            }

            var validationResult = validator.Validate(registration);
            if (!validationResult.IsValid)
            {
                logger.LogWarning(
                    "Request validation failed. CorrelationId={CorrelationId}; ErrorCount={ErrorCount}",
                    correlationId,
                    validationResult.Errors.Count);
                return Json(
                    HttpStatusCode.BadRequest,
                    new ApiResponse
                    {
                        Success = false,
                        Status = "invalid_request",
                        Message = "Request validation failed.",
                        Errors = validationResult.Errors,
                        CorrelationId = correlationId
                    });
            }

            logger.LogInformation("Request validation succeeded. CorrelationId={CorrelationId}", correlationId);
            var result = await processor.ProcessAsync(registration!, cancellationToken).ConfigureAwait(false);

            return result.Status == CrmOperationStatus.AlreadyExists
                ? Json(
                    HttpStatusCode.OK,
                    Success("already_exists", result.CrmId, "Lead already exists.", correlationId))
                : Json(
                    HttpStatusCode.Created,
                    Success("created", result.CrmId, "Lead successfully created.", correlationId));
        }
        catch (CrmAuthenticationException exception)
        {
            logger.LogError("CRM authentication failed. CorrelationId={CorrelationId}", correlationId);
            return Json(
                HttpStatusCode.BadGateway,
                new ApiResponse
                {
                    Success = false,
                    Status = "authentication_error",
                    Message = "CRM authentication failed.",
                    CorrelationId = correlationId,
                    Authentication = exception.Diagnostic
                });
        }
        catch (CrmApiException exception) when (
            exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogError(
                "CRM authorization failed. CorrelationId={CorrelationId}; StatusCode={StatusCode}; CrmErrorCode={CrmErrorCode}",
                correlationId,
                (int)exception.StatusCode,
                exception.CrmErrorCode ?? "unavailable");
            return Json(
                HttpStatusCode.BadGateway,
                Error("authentication_error", "CRM authentication or authorization failed.", correlationId));
        }
        catch (CrmTimeoutException exception)
        {
            logger.LogError(exception, "CRM request timed out. CorrelationId={CorrelationId}", correlationId);
            return Json(
                HttpStatusCode.GatewayTimeout,
                Error("timeout", "CRM request timed out.", correlationId));
        }
        catch (CrmApiException exception)
        {
            logger.LogError(
                exception,
                "CRM API request failed. CorrelationId={CorrelationId}; StatusCode={StatusCode}; CrmErrorCode={CrmErrorCode}",
                correlationId,
                (int)exception.StatusCode,
                exception.CrmErrorCode ?? "unavailable");
            return Json(
                HttpStatusCode.BadGateway,
                Error("crm_api_error", "CRM operation failed.", correlationId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled. CorrelationId={CorrelationId}", correlationId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected system error. CorrelationId={CorrelationId}", correlationId);
            return Json(
                HttpStatusCode.InternalServerError,
                Error("system_error", "An unexpected error occurred.", correlationId));
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "CreateCrmLead endpoint completed. CorrelationId={CorrelationId}; DurationMs={DurationMs}",
                correlationId,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool HasJsonContentType(string? contentType) =>
        contentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private static IResult Json(HttpStatusCode statusCode, ApiResponse payload) =>
        Results.Json(payload, statusCode: (int)statusCode);

    private static ApiResponse Success(
        string status,
        Guid crmId,
        string message,
        string correlationId) =>
        new()
        {
            Success = true,
            Status = status,
            CrmId = crmId.ToString(),
            Message = message,
            CorrelationId = correlationId
        };

    private static ApiResponse Error(string status, string message, string correlationId) =>
        new()
        {
            Success = false,
            Status = status,
            Message = message,
            CorrelationId = correlationId
        };
}
