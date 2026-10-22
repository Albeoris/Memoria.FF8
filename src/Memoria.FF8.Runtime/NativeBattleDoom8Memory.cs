using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoom8State? Doom8 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Doom8ColdSourcesPending { get; init; }
}

public static class NativeBattleDoom8Memory
{
    public static (NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging) Prepare(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleDoom8Resources resources)
    {
        if (memory.Doom8ColdSourcesPending == false && memory.Doom8 is { } retained)
        {
            retained.Validate();
            return (memory, staging);
        }
        if (memory.Doom8ColdSourcesPending != true || memory.Doom8 is not null)
        {
            throw new NotSupportedException("Doom requires its actual retained module source initialization state.");
        }
        resources.Validate();
        uint pointer = resources.RequireColdTargetPointer();
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, pointer);
        staging = staging.Write(0x1636C04, bytes);
        return (memory with { Doom8 = NativeBattleDoom8State.FromColdProcess(resources.InitialIoScript.Data), Doom8ColdSourcesPending = false }, staging);
    }
}
