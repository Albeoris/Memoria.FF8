using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Provides case-insensitive, read-only access to a Steam configuration section.
/// </summary>
public sealed class SteamConfigurationValues : IReadOnlyDictionary<String, String>
{
    private readonly IReadOnlyDictionary<String, String> _values;

    /// <summary>
    /// Initializes a configuration section by copying the supplied values.
    /// </summary>
    /// <param name="values">The configuration key/value pairs.</param>
    public SteamConfigurationValues(IEnumerable<KeyValuePair<String, String>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<String, String> copy = new(StringComparer.OrdinalIgnoreCase);
        foreach ((String key, String value) in values)
            copy.Add(key, value);
        _values = new ReadOnlyDictionary<String, String>(copy);
    }

    /// <summary>Gets the configured language, if present.</summary>
    public String? Language => GetValueOrDefault("language");

    /// <summary>Gets the selected beta key, if present.</summary>
    public String? BetaKey => GetValueOrDefault("BetaKey");

    /// <summary>
    /// Gets whether high-quality audio is enabled, or <c>null</c> when the setting is absent.
    /// </summary>
    public Boolean? HighQualityAudio => TryGetValue("highqualityaudio", out String? value)
        ? !String.Equals(value, "0", StringComparison.Ordinal)
        : null;

    /// <summary>Gets the number of configuration values.</summary>
    public Int32 Count => _values.Count;

    /// <summary>Gets the configuration keys.</summary>
    public IEnumerable<String> Keys => _values.Keys;

    /// <summary>Gets the configuration values.</summary>
    public IEnumerable<String> Values => _values.Values;

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The associated value.</returns>
    public String this[String key] => _values[key];

    /// <summary>Gets a value by key, or <c>null</c> when the key is absent.</summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The associated value, or <c>null</c>.</returns>
    public String? GetValueOrDefault(String key) => _values.GetValueOrDefault(key);

    /// <summary>Determines whether the section contains the specified key.</summary>
    /// <param name="key">The configuration key.</param>
    /// <returns><c>true</c> when the key is present.</returns>
    public Boolean ContainsKey(String key) => _values.ContainsKey(key);

    /// <summary>Attempts to obtain the value associated with a key.</summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">Receives the value when found.</param>
    /// <returns><c>true</c> when the key is present.</returns>
    public Boolean TryGetValue(String key, [NotNullWhen(true)] out String? value) =>
        _values.TryGetValue(key, out value);

    /// <summary>Returns an enumerator for the configuration values.</summary>
    /// <returns>An enumerator for the key/value pairs.</returns>
    public IEnumerator<KeyValuePair<String, String>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
