using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedProcessColorRow(ushort? Factor, byte? Red, byte? Green, byte? Blue);

/// <summary>The four physical process flag bytes, independent of the seven battle graphics rows.</summary>
public sealed record NativeBattleScriptedProcessFlagsState([property: JsonRequired] ImmutableArray<byte> Flags, [property: JsonRequired] ImmutableArray<bool> Known)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleScriptedProcessColorRow>? ColorRows { get; init; }

    public static NativeBattleScriptedProcessFlagsState FromColdProcess() => new([0, 0, 0, 0], [true, true, true, true]) { ColorRows = [new(0, 0, 0, 0), new(0, 0, 0, 0), new(0, 0, 0, 0), new(0, 0, 0, 0)] };

    public void Validate()
    {
        if (Flags.IsDefault || Known.IsDefault || Flags.Length != 4 || Known.Length != 4)
        {
            throw new InvalidDataException("Native cinematic process flags require their four physical bytes and known mask.");
        }
        if (ColorRows is { } rows && (rows.IsDefault || rows.Length != 4 || rows.Any(row => row is null)))
        {
            throw new InvalidDataException("Native cinematic process colors require their four physical rows.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedProcessFlagsState? ScriptedProcessFlags { get; init; }
}

public sealed record NativeBattleScriptedProcessFlagsStep(NativeBattleWaterExecutionFrame Frame, NativeBattleScriptedProcessFlagsState State);

public static class NativeBattleScriptedProcessFlags
{
    public static NativeBattleScriptedProcessFlagsStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedProcessFlagsState state)
    {
        state.Validate();
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x6D)
        {
            throw new NotSupportedException("Native process flag command requires its selected instruction.");
        }
        if (state.Known.Any(known => !known))
        {
            throw new NotSupportedException("Native process flags require their retained physical bytes.");
        }
        byte[] flags = state.Flags.ToArray();
        for (int index = 0; index < flags.Length; index++)
        {
            flags[index] = (instruction & 0x8000) == 0 ? (byte)(flags[index] | 2) : (byte)(flags[index] & 0xFD);
        }
        return new(NativeBattleScriptedCommandReads.Advance(frame, 2), state with { Flags = flags.ToImmutableArray() });
    }
}
