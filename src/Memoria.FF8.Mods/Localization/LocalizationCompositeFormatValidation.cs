using System.Text;
using System.Buffers;

namespace Memoria.FF8.Mods.Localization;

/// <summary>Validates the runtime owner's BCL argument contract without expanding unbounded user-supplied formatting.</summary>
public static class LocalizationCompositeFormatValidation
{
    private const int MaximumArguments = 256;
    private const int MaximumProbeCharacters = 64 * 1024;
    private sealed record Argument(int Index);
    private sealed class Probe : IFormatProvider, ICustomFormatter
    {
        public Dictionary<(int Index, string Format), int> Uses { get; } = [];
        public object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : null;
        public string Format(string? format, object? argument, IFormatProvider? provider)
        {
            Argument value = (Argument)argument!;
            var identity = (value.Index, format ?? "");
            Uses[identity] = Uses.GetValueOrDefault(identity) + 1;
            return "x";
        }
    }

    public static bool TryReadSource(string source, out CompositeFormat? format)
    {
        if (source.Length > MaximumProbeCharacters)
        {
            throw new InvalidDataException("The System source format exceeds the supported validation length.");
        }
        try
        {
            format = CompositeFormat.Parse(source);
            return true;
        }
        catch (FormatException)
        {
            format = null;
            return false;
        }
    }

    public static void Validate(CompositeFormat source, string target, string key)
    {
        try
        {
            if (target.Length > MaximumProbeCharacters)
            {
                throw new FormatException("The System format exceeds the supported validation length.");
            }
            Dictionary<(int Index, string Format), int> required = Read(source);
            Dictionary<(int Index, string Format), int> translated = Read(CompositeFormat.Parse(target));
            if (required.Count != translated.Count || required.Any(pair => translated.GetValueOrDefault(pair.Key) != pair.Value))
            {
                throw new InvalidDataException($"{key}: translated System text must preserve its runtime argument IDs, occurrence counts and owner-defined format components.");
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"{key}: invalid or oversized BCL composite format: {exception.Message}", exception);
        }
    }

    private static Dictionary<(int Index, string Format), int> Read(CompositeFormat format)
    {
        if (format.MinimumArgumentCount > MaximumArguments)
        {
            throw new FormatException($"System text exceeds the supported {MaximumArguments} runtime argument slots.");
        }
        object?[] arguments = Enumerable.Range(0, format.MinimumArgumentCount).Select(index => (object?)new Argument(index)).ToArray();
        Probe probe = new();
        char[] output = ArrayPool<char>.Shared.Rent(MaximumProbeCharacters);
        try
        {
            // BCL TryWrite cannot grow the supplied span, including for adversarial alignment widths.
            if (!output.AsSpan(0, MaximumProbeCharacters).TryWrite(probe, format, out _, arguments))
            {
                throw new FormatException("The System format expands beyond the supported validation length.");
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(output);
        }
        return probe.Uses;
    }
}
