using System.Text.Json.Serialization;
using BankCore.Api.Authentication;
using BankCore.Api.Extensions;
using BankCore.Api.Middleware;
using BankCore.Application;
using BankCore.Application.Abstractions;
using BankCore.Application.Options;
using BankCore.Infrastructure;
using BankCore.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Layers ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// ---------- Options (validated at startup: fail fast on bad config) ----------
builder.Services.AddOptions<TransferLimitsOptions>()
    .BindConfiguration(TransferLimitsOptions.SectionName)
    .Validate(o => o.SingleTransferLimit > 0 && o.DailyTransferLimit >= o.SingleTransferLimit,
        "TransferLimits: SingleTransferLimit must be > 0 and DailyTransferLimit >= SingleTransferLimit.")
    .ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SectionName);

// ---------- Security ----------
builder.Services.AddJwtAuthentication();
builder.Services.AddBankAuthorization();
builder.Services.AddLoginRateLimiting();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// ---------- Web ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<BankDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await app.InitialiseDatabaseAsync();
await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory integration tests.</summary>
public partial class Program
{
}
