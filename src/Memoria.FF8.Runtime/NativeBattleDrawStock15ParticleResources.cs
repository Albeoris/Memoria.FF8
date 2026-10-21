using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15ParticleResources(ImmutableArray<byte> Trajectory, ImmutableArray<byte> Sparkle)
{
    public void Validate()
    {
        ValidateSprite(Trajectory, 0x162B520, 528, 6);
        ValidateSprite(Sparkle, 0x162B730, 288, 0);
    }

    public NativeBattleModuleResource Resource(uint address)
    {
        ImmutableArray<byte> bytes = address switch
        {
            0x162B520 => Trajectory,
            0x162B730 => Sparkle,
            _ => throw new NotSupportedException("Draw Stock particle sprite leaves its original source directory.")
        };
        return new(0, address, 0, 0, bytes);
    }

    private static void ValidateSprite(ImmutableArray<byte> bytes, uint address, int length, byte kind)
    {
        if (bytes.IsDefault || bytes.Length != length || bytes[3] != kind || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(8)) != 6)
        {
            throw new InvalidDataException("Draw Stock particles require their original complete six-frame sprite sources.");
        }
        NativeBattleModuleResource directory = new(0, address, 0, 0, bytes);
        for (int phase = 0; phase < 6; phase++)
        {
            _ = directory.ProjectedFrame571C80(phase);
        }
    }
}
