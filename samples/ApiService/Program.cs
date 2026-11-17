using TypedConfigGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(sp =>
    StorageBinder.Bind(sp.GetRequiredService<IConfiguration>()));

var app = builder.Build();

app.MapGet("/storage", (StorageOptions options) =>
    Results.Ok(new { options.Path, options.Timeout }));

app.Run();

[TypedConfig("Storage")]
public partial class StorageOptions
{
    [ConfigKey("Path")] public string Path { get; init; } = "";
    [ConfigKey("Timeout")] public TimeSpan Timeout { get; init; }
}
