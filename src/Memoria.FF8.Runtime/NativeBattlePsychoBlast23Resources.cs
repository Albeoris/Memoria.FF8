using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePsychoBlast23Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, uint Sound, NativeBattleFullscreenOverlayProgram Overlay, NativeBattleModuleResource Sprite, NativeBattleGfAnimationSource ActPrimary, NativeBattleGfAnimationSource ActDelayed, NativeBattleGfAnimationSource ActFollowup)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 832 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 832 || Executable is null || Executable.ResourceAddress != 0x17EA4F0 || Executable.Data.IsDefault || Executable.Data.Length != 15716 || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0 || Sound != 0x6427D || Overlay is null || Overlay.Address != 0x16146C8 || !Overlay.Commands.SequenceEqual(new uint[] { 0xC0C0C0, 0x808080, 0x404040, 0xFF000000 }))
        {
            throw new InvalidDataException("Psycho Blast requires its complete original camera, node bank, sound and overlay.");
        }
        if (Sprite is null || Sprite.Address != 0x161428C || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 248 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 9)
        {
            throw new InvalidDataException("Psycho Blast requires its original nine-frame debris sprite.");
        }
        for (int frame = 0; frame < 9; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
        RequireAct(ActPrimary, 0x17EE254, 8028, 4, 32);
        RequireAct(ActDelayed, 0x17F01B0, 5524, 2, 40);
        RequireAct(ActFollowup, 0x17F1744, 8028, 4, 32);
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, Sound) { Effect = 23, Executable = Executable };
    }

    public NativeBattleGfAnimationSource Act(uint address)
    {
        return address switch
        {
            0x17EE254 => ActPrimary,
            0x17F01B0 => ActDelayed,
            0x17F1744 => ActFollowup,
            _ => throw new NotSupportedException("Psycho Blast ACT source leaves its original resource directory.")
        };
    }

    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length, int count, int duration)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Psycho Blast ACT requires its original source address and complete bytes.");
        }
        int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        if (table < 0 || table > length - 8 || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)) != count || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)) != duration)
        {
            throw new InvalidDataException("Psycho Blast ACT requires its original node count and duration.");
        }
    }
}
