# typed-config-gen

A Roslyn source generator that produces `IConfiguration` binders for annotated
options classes. Binding code is emitted at compile time, so startup does not
walk the configuration tree with reflection and the result stays trim- and
AOT-friendly.

```csharp
[TypedConfig("Storage")]
public partial class StorageOptions
{
    [ConfigKey("Path")] public string Path { get; init; } = "";
    [ConfigKey("Timeout")] public TimeSpan Timeout { get; init; }
}

// generated
StorageOptions options = StorageOptionsBinder.Bind(configuration);
```

Each accessor goes through `IConfiguration.GetSection(...)` exactly once per
section and throws `ConfigBindingException` naming the missing key, so a
misconfigured deployment fails at first use instead of returning `null`.

## Why

`Microsoft.Extensions.Configuration.Binder` is reflection based: it links the
whole binding machinery, has no compile-time key checking, and misses keys
silently. A generator keeps the familiar section/key mapping but moves the work
to build time.

## Install

```xml
<PackageReference Include="TypedConfigGen" Version="0.1.0" PrivateAssets="all" />
```

The generator targets `netstandard2.0` (Roslyn 4.8) and runs on any SDK from
.NET 6 up. Generated code needs `net8.0` or later, where `IConfiguration` and
`init` accessors are available to the analyzer host.

## Usage

1. Mark the options class `partial` and add `[TypedConfig("Section")]`.
2. Add one `[ConfigKey("Name")]` per bindable property.
3. Configure the project to treat the generator assembly as an analyzer:

```xml
<ItemGroup>
  <Analyzer Include="$(OutputPath)TypedConfigGen.dll" />
</ItemGroup>
```

Supported property types: `string`, `bool`, `int`, `TimeSpan`, `Uri`.
Anything else is reported as `TCG0002` and left unbound.

## Analyzer rules

| Id | Severity | Meaning |
|----|----------|---------|
| TCG0001 | Error | Options class must be `partial` so the binder can be added to it |
| TCG0002 | Warning | Property type is not supported by the emitter |

## Layout

```
src/TypedConfigGen/Attributes.cs           TypedConfig / ConfigKey attributes
src/TypedConfigGen/TypedConfigGenerator.cs incremental generator and emitter
samples/ApiService/                       minimal API sample wiring the binder
```

## License

MIT, see [LICENSE](LICENSE).
