using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedInitializationDirectories([property: JsonRequired] NativeBattleEffectHeapAllocation InitializationDirectory, [property: JsonRequired] NativeBattleEffectHeapAllocation OwnerDirectory)
{
    public void Validate()
    {
        if (InitializationDirectory is null || InitializationDirectory.Data.IsDefault || InitializationDirectory.Data.Length != 0x1C || OwnerDirectory is null || OwnerDirectory.Data.IsDefault || OwnerDirectory.Data.Length != 0x14)
        {
            throw new InvalidDataException("Native scripted initialization requires its complete initialization and physical-owner directories.");
        }
    }

    public uint InitializationOffset(int byteOffset)
    {
        Validate();
        if (byteOffset < 0 || byteOffset > 0x18 || (byteOffset & 3) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(InitializationDirectory.Data.AsSpan().Slice(byteOffset));
    }

    public uint OwnerAddress(int index)
    {
        Validate();
        if ((uint)index >= 5)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(OwnerDirectory.Data.AsSpan().Slice(index * 4));
    }
}
