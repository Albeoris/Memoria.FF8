using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeFieldRegistrationEntry
{
    FieldEntry,
    BattleReturn
}

/// <summary>Authenticated allocation identity; addresses are derived rather than accepted from saved input.</summary>
public sealed record NativeFieldRegistrationLayout([property: JsonRequired] string ExecutableHash, [property: JsonRequired] NativeFieldRegistrationEntry Entry)
{
    public static NativeFieldRegistrationLayout ForImage(string executableHash, NativeFieldRegistrationEntry entry)
    {
        NativeFieldRegistrationLayout layout = new(executableHash, entry);
        layout.Validate();
        return layout;
    }

    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || !Enum.IsDefined(Entry))
        {
            throw new InvalidDataException("Field registration requires an authenticated allocation profile and an explicit entry mode.");
        }
    }

    [JsonIgnore]
    public uint ArenaAddress => IsRemastered ? 0x0A300000u : 0x018DD018u;

    [JsonIgnore]
    public uint ArenaEnd => IsRemastered ? 0x0A600000u : 0x019F9818u;

    [JsonIgnore]
    public uint Limit => Entry switch
    {
        NativeFieldRegistrationEntry.FieldEntry => ArenaEnd,
        NativeFieldRegistrationEntry.BattleReturn => IsRemastered ? 0x0A5F0000u : 0x019EF018u,
        _ => throw new InvalidDataException("Unknown field registration entry mode.")
    };

    [JsonIgnore]
    internal uint MemoryStart => Math.Min(ArenaAddress, NativeBattlePartyCpuLayout.ForImage(ExecutableHash).BaseAddress);

    [JsonIgnore]
    internal uint MemoryEnd => Math.Max(ArenaEnd, 0x01DCD6E4u);

    private bool IsRemastered => ExecutableHash switch
    {
        NativeFieldRuntimeProfile.FF8EnExecutableHash => false,
        NativeFieldRuntimeProfile.RemasteredEnImageHash => true,
        _ => throw new InvalidDataException("Unknown field registration allocation profile.")
    };
}
