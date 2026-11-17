using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TypedConfigGen;

/// <summary>
/// Emits <c>{Type}Binder.Bind(IConfiguration)</c> for every class marked with
/// <see cref="TypedConfigAttribute"/>. Only the configured section is read, so
/// no reflection or generic binder is needed at run time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TypedConfigGenerator : IIncrementalGenerator
{
    private const string TypedConfigName = "TypedConfigGen.TypedConfigAttribute";
    private const string ConfigKeyName = "TypedConfigGen.ConfigKeyAttribute";

    private static readonly DiagnosticDescriptor NotPartial = new(
        id: "TCG0001",
        title: "Options class must be partial",
        messageFormat: "'{0}' is annotated with [TypedConfig] but is not partial; the binder cannot be added",
        category: "TypedConfigGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedType = new(
        id: "TCG0002",
        title: "Unsupported configuration property type",
        messageFormat: "'{0}' is not a supported property type and is left unbound",
        category: "TypedConfigGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var options = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TypedConfigName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => Build(ctx))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(options, static (spc, model) => Emit(spc, model));
    }

    private sealed class Model
    {
        public string TypeName { get; init; } = "";
        public string BinderName { get; init; } = "";
        public string Namespace { get; init; } = "";
        public string Section { get; init; } = "";
        public string HintName { get; init; } = "";
        public List<(string Property, string KeyExpr)> Bindings { get; } = new();
    }

    private static Model? Build(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var section = ctx.Attributes[0].ConstructorArguments[0].Value as string ?? "";
        var hint = $"{type.ToDisplayString()}.binder.g.cs".Replace('<', '_').Replace('>', '_');

        if (!IsPartial(type))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                NotPartial, type.Locations.FirstOrDefault(), type.Name));
        }

        var model = new Model
        {
            TypeName = type.ToDisplayString(),
            BinderName = type.Name + "Binder",
            Namespace = type.ContainingNamespace.IsGlobalNamespace
                ? ""
                : type.ContainingNamespace.ToDisplayString(),
            Section = section,
            HintName = hint,
        };

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer)
            {
                continue;
            }

            var keyAttribute = property.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ConfigKeyName);

            if (keyAttribute is null)
            {
                continue;
            }

            var key = keyAttribute.ConstructorArguments[0].Value as string ?? property.Name;
            var expression = BindExpression(property.Type, key, section);

            if (expression is null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedType, property.Locations.FirstOrDefault(), property.Name));
                continue;
            }

            model.Bindings.Add((property.Name, expression));
        }

        return model;
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.ValueText == "partial"));

    /// <summary>
    /// Returns the assignment expression for a supported property type, or
    /// <c>null</c> for types the emitter does not parse.
    /// </summary>
    private static string? BindExpression(ITypeSymbol type, string key, string section)
    {
        const string required = "Required(section, ";

        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return $"{required}\"{key}\")";
            case SpecialType.System_Boolean:
                return $"bool.Parse({required}\"{key}\"))";
            case SpecialType.System_Int32:
                return $"int.Parse({required}\"{key}\"), CultureInfo.InvariantCulture)";
        }

        return type.ToDisplayString() switch
        {
            "System.TimeSpan" => $"TimeSpan.Parse({required}\"{key}\"), CultureInfo.InvariantCulture)",
            "System.Uri" => $"new Uri({required}\"{key}\"), UriKind.RelativeOrAbsolute)",
            _ => null,
        };
    }

    private static void Emit(SourceProductionContext spc, Model model)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
        code.AppendLine("using System;");
        code.AppendLine("using System.Globalization;");
        code.AppendLine("using Microsoft.Extensions.Configuration;");
        code.AppendLine();

        if (model.Namespace.Length > 0)
        {
            code.AppendLine($"namespace {model.Namespace};");
            code.AppendLine();
        }

        code.AppendLine($"public static class {model.BinderName}");
        code.AppendLine($"{{");
        code.AppendLine($"    public static {model.TypeName} Bind(IConfiguration configuration)");
        code.AppendLine($"    {{");
        code.AppendLine($"        ArgumentNullException.ThrowIfNull(configuration);");
        code.AppendLine($"        var section = configuration.GetSection(\"{model.Section}\");");
        code.AppendLine($"        return new {model.TypeName}");
        code.AppendLine($"        {{");

        foreach (var (property, expression) in model.Bindings)
        {
            code.AppendLine($"            {property} = {expression},");
        }

        code.AppendLine($"        }};");
        code.AppendLine($"    }}");
        code.AppendLine();
        code.AppendLine($"    private static string Required(IConfigurationSection section, string key)");
        code.AppendLine($"    {{");
        code.AppendLine($"        var value = section[key];");
        code.AppendLine($"        return value ?? throw new global::TypedConfigGen.ConfigBindingException(section.Path + \":\" + key);");
        code.AppendLine($"    }}");
        code.AppendLine($"}}");

        spc.AddSource(model.HintName, SourceText.From(code.ToString(), Encoding.UTF8));
    }
}
