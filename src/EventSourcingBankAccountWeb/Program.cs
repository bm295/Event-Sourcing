using EventSourcingBankAccountWeb.Infrastructure;
using EventSourcingBankAccountWeb.Models;
using EventSourcingBankAccountWeb.Services;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IEventDataSigner>(_ =>
{
    var configuredKey = builder.Configuration["EventSigning:Key"];
    var key = string.IsNullOrWhiteSpace(configuredKey)
        ? RandomNumberGenerator.GetBytes(32)
        : Convert.FromBase64String(configuredKey);
    return new HmacEventDataSigner(key);
});
builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();
builder.Services.AddSingleton<DemoStateService>();
builder.Services.AddHttpClient<IEventExplanationModel, HttpEventExplanationModel>();
builder.Services.AddScoped<AiEventIntelligenceService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/demo/state", (DemoStateService demo) => Results.Ok(demo.GetState()));

app.MapPost("/api/demo/commands", (ExecuteCommandRequest request, DemoStateService demo) =>
{
    var result = demo.ExecuteCommand(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/demo/replay", (DemoStateService demo) => Results.Ok(demo.Replay()));


app.MapPost("/api/ai/events/explain", async (ExplainEventsRequest request, AiEventIntelligenceService ai, CancellationToken ct) =>
{
    try
    {
        var result = await ai.ExplainAsync(request, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (HttpRequestException) { return Results.Problem("Model provider unavailable.", statusCode: 503); }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Configure EventAI:"))
    {
        return Results.Problem("Model provider is not configured.", statusCode: 503);
    }
    catch (InvalidOperationException) { return Results.Problem("Model evidence validation failed.", statusCode: 502); }
    catch (System.Text.Json.JsonException) { return Results.Problem("Invalid model response.", statusCode: 502); }
});

app.Run();
