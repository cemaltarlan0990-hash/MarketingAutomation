using CrmRegistrationGateway.Configuration;
using CrmRegistrationGateway.Endpoints;
using CrmRegistrationGateway.Middleware;
using CrmRegistrationGateway.Services;
using CrmRegistrationGateway.Validation;
using Microsoft.AspNetCore.Server.IIS;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var crmOptions = CrmOptions.FromConfiguration(builder.Configuration);
var fieldMapping = CrmFieldMapping.FromConfiguration(builder.Configuration);
var apiSecurityOptions = ApiSecurityOptions.FromConfiguration(builder.Configuration);
crmOptions.Validate();
fieldMapping.Validate();
apiSecurityOptions.Validate();

builder.Services.AddSingleton(crmOptions);
builder.Services.AddSingleton(fieldMapping);
builder.Services.AddSingleton(apiSecurityOptions);
builder.Services.AddSingleton<EventRegistrationValidator>();
builder.Services.AddSingleton<ICrmAuthenticationService, CrmAuthenticationService>();
builder.Services.AddScoped<ICrmService, CrmService>();
builder.Services.AddScoped<LeadRegistrationProcessor>();

builder.Services.AddHttpClient(CrmService.HttpClientName, client =>
{
    client.BaseAddress = crmOptions.GetApiBaseUri();
    client.Timeout = TimeSpan.FromSeconds(crmOptions.RequestTimeoutSeconds);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("OData-MaxVersion", "4.0");
    client.DefaultRequestHeaders.TryAddWithoutValidation("OData-Version", "4.0");
});

const long maximumRequestBodySize = 64 * 1024;
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maximumRequestBodySize);
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = maximumRequestBodySize);

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    await next(context).ConfigureAwait(false);
});

app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");
app.MapCrmLeadEndpoints();

app.Run();

public partial class Program
{
}
