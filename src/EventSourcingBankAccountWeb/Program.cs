using EventSourcingBankAccountWeb.Infrastructure;
using EventSourcingBankAccountWeb.Models;
using EventSourcingBankAccountWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IClock, SystemClock>();
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

app.MapPost("/api/demo/reset", (DemoStateService demo) =>
{
    demo.Reset();
    return Results.Ok(demo.GetState());
});

app.Run();
