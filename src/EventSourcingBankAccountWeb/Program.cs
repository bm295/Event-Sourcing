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


app.Run();
