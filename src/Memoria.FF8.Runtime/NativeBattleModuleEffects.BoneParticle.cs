using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    /// <summary>570E60 writes placement3 and retains the original packed four-byte resource parameters.</summary>
    public NativeBattleModuleState AppendBoneParticle(int model, byte bone, short fraction, NativeBattleModuleResource resource, uint packedParameters)
    {
        if ((uint)model >= 7 || (byte)packedParameters != resource.Id)
        {
            throw new InvalidDataException("Native570E60 requires its actual model and resource parameters.");
        }
        resource.Validate();
        if (resource.WrapperCallback != 0x570CC0)
        {
            throw new NotSupportedException("Native570E60 resource requires its original wrapper callback.");
        }
        (NativeBattleModuleState state, int slot) = Append(NativeBattleModuleKind.Placement570CC0);
        byte[] payload = state.Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), ModelAddress(model));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), bone);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14), fraction);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), packedParameters);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), resource.Address);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }
}
