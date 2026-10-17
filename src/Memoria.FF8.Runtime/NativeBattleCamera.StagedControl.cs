using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStagingBlock(uint Address, int Length);

public sealed partial record NativeBattleCameraState
{
    public NativeBattleStagingBlock? ControlStagingSource { get; init; }

    public NativeBattleCameraState RegisterControlResource(NativeBattleEffectAnimationRegisters? resource)
    {
        return this with { ControlResource = resource, ControlStagingSource = null, ControlCursor = 0 };
    }

    public static NativeBattleEffectAnimationRegisters ReadStagedControlResource(NativeBattleFileStaging staging, NativeBattleStagingBlock block)
    {
        if (block.Length < 6)
        {
            throw new InvalidDataException("Native5099A0 requires its complete staged camera resource header.");
        }
        ImmutableArray<byte> bytes = staging.Read(block.Address, block.Length).ToImmutableArray();
        int stream = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(2));
        int models = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(4));
        if (stream >= bytes.Length || models > bytes.Length - 2)
        {
            throw new InvalidDataException("Native5099A0 staged camera pointers leave the observed resource.");
        }
        return new(new("SharedBattleCamera", bytes), 0, stream, models);
    }
}
