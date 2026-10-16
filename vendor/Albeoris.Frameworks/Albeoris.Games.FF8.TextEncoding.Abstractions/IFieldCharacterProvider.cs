namespace Albeoris.Games.FF8.TextEncoding;

/// <summary>
/// Resolves the built-in <see cref="FieldCharacterSet"/> for a Japanese field (map),
/// looked up by its name or id.
/// </summary>
public interface IFieldCharacterProvider
{
    /// <summary>
    /// Returns the field characters registered for <paramref name="fieldName"/>, or throws
    /// <see cref="KeyNotFoundException"/> if none are registered.
    /// </summary>
    FieldCharacterSet Get(String fieldName);

    /// <summary>
    /// Returns the field characters registered for <paramref name="fieldId"/>, or throws
    /// <see cref="KeyNotFoundException"/> if none are registered.
    /// </summary>
    FieldCharacterSet Get(Int32 fieldId);

    /// <summary>
    /// Returns the field characters registered for <paramref name="fieldName"/>, or
    /// <see langword="null"/> if none are registered.
    /// </summary>
    FieldCharacterSet? TryGet(String fieldName);

    /// <summary>
    /// Returns the field characters registered for <paramref name="fieldId"/>, or
    /// <see langword="null"/> if none are registered.
    /// </summary>
    FieldCharacterSet? TryGet(Int32 fieldId);
}
