using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect60Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource PrimaryNodes, NativeBattleCuragaExecutableSource SecondaryNodes, NativeBattleGfAnimationSource ActPrimary, NativeBattleGfAnimationSource ActSecondary, NativeBattleModuleResource Sprite, uint Sound, ImmutableArray<short> TargetMotionRatios)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 788 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 788 || Sound != 0x64280 || TargetMotionRatios.IsDefault || !TargetMotionRatios.SequenceEqual(new short[] { 0, 2407, 3476, 3706, 3883, 4007, 4077, 4096 }))
        {
            throw new InvalidDataException("Effect60 requires its complete original camera, sound and target motion ratios.");
        }
        RequireNodes(PrimaryNodes, 0x1797CC0, 19672);
        RequireNodes(SecondaryNodes, 0x179C998, 17260);
        RequireAct(ActPrimary, 0x17A0D04, 9292, 4, 32);
        RequireAct(ActSecondary, 0x17A3150, 3200, 1, 24);
        if (Sprite is null || Sprite.Address != 0x1597360 || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 428 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Effect60 requires its complete original sixteen-frame debris sprite.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
    }

    public NativeBattleCuragaResources GenericNodes(uint sourceAddress)
    {
        NativeBattleCuragaExecutableSource source = sourceAddress switch
        {
            0x1797CC0 => PrimaryNodes,
            0x179C998 => SecondaryNodes,
            _ => throw new NotSupportedException("Effect60 generic task source leaves its two original addressed banks.")
        };
        return new(Animation, Sound) { Effect = 60, Executable = source };
    }

    public NativeBattleGfAnimationSource Act(uint address)
    {
        return address switch
        {
            0x17A0D04 => ActPrimary,
            0x17A3150 => ActSecondary,
            _ => throw new NotSupportedException("Effect60 ACT source leaves its original resource directory.")
        };
    }

    private static void RequireNodes(NativeBattleCuragaExecutableSource source, uint address, int length)
    {
        if (source is null || source.ResourceAddress != address || source.Data.IsDefault || source.Data.Length != length || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Effect60 requires both independently addressed original uninitialized node banks.");
        }
    }

    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length, int count, int duration)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Effect60 ACT requires its complete original source and physical address.");
        }
        int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        if (table < 0 || table > length - 8 || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)) != count || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)) != duration)
        {
            throw new InvalidDataException("Effect60 ACT requires its original duration and node directory.");
        }
    }
}
