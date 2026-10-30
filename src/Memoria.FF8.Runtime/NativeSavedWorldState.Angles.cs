using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSavedWorldAngleWords([property: JsonRequired] ImmutableArray<ushort> BodyWords, [property: JsonRequired] ushort CameraWord)
{
    public static ushort Normalize(ushort value)
    {
        return (ushort)(value & 4095);
    }

    public void Validate()
    {
        if (BodyWords.IsDefault || BodyWords.Length != 8)
        {
            throw new InvalidDataException("Saved world angles require all eight original body WORDs.");
        }
    }

    public ushort ResolveBodyWord(int slot, ushort current)
    {
        Validate();
        ushort original = BodyWords[slot];
        return current == Normalize(original) ? original : current;
    }

    public ushort ResolveCameraWord(ushort current)
    {
        return current == Normalize(CameraWord) ? CameraWord : current;
    }
}

public sealed partial record NativeSavedWorldState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldAngleWords? OriginalAngleWords { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? EntryMode104 { get; init; }
}
