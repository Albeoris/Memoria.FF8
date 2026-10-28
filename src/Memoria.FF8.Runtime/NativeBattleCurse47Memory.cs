using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCurse47State? Curse47
    {
        get => effectState.Curse47;
        init => effectState = effectState with { Curse47 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Curse47ColdSourcesPending
    {
        get => effectState.Curse47ColdSourcesPending;
        init => effectState = effectState with { Curse47ColdSourcesPending = value };
    }
}

public static class NativeBattleCurse47Memory
{
    public static (NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging) Prepare(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleCurse47Resources resources)
    {
        if (memory.Curse47ColdSourcesPending == false && memory.Curse47 is { } retained)
        {
            retained.Validate();
            return (memory, staging);
        }
        if (memory.Curse47ColdSourcesPending != true || memory.Curse47 is not null)
        {
            throw new NotSupportedException("Curse requires its actual retained module source initialization state.");
        }
        resources.Validate();
        uint pointer = resources.ColdSharedPointer;
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, pointer);
        staging = staging.Write(0x15B8028, bytes);
        return (memory with { Curse47 = NativeBattleCurse47State.FromColdProcess(resources.InitialIoScript.Data), Curse47ColdSourcesPending = false }, staging);
    }
}
