using System;

namespace TypedConfigGen;

/// <summary>
/// Marks a partial options class for which a configuration binder is generated.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TypedConfigAttribute : Attribute
{
    public TypedConfigAttribute(string section) => Section = section;

    /// <summary>Configuration section the binder reads from.</summary>
    public string Section { get; }
}

/// <summary>
/// Maps a property to a configuration key inside the section given by
/// <see cref="TypedConfigAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ConfigKeyAttribute : Attribute
{
    public ConfigKeyAttribute(string key) => Key = key;

    /// <summary>Key inside the section, relative to the section path.</summary>
    public string Key { get; }
}

/// <summary>Thrown when a required configuration key has no value.</summary>
public sealed class ConfigBindingException : Exception
{
    public ConfigBindingException(string path)
        : base($"Configuration key '{path}' is missing.")
        => Path = path;

    /// <summary>Full `section:key` path that could not be resolved.</summary>
    public string Path { get; }
}
