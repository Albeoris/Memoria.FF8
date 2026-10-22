using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMicroMissiles299Resources(NativeBattleGfAnimationSource Missile, NativeBattleGfAnimationSource Impact, NativeBattleGfAnimationSource WideImpact, NativeBattleModuleResource ParticleSprite, NativeBattleModuleResource TrailSprite, NativeBattleModuleResource ImpactSprite, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation CasterDat, uint Sound)
{
    public void Validate()
    {
        Source(Missile, 0x10490A8, 0x2594, 3, 0xA38, 22, 45);
        Source(Impact, 0x104B63C, 0x12D4, 2, 0x10F0, 2, 12);
        Source(WideImpact, 0x104C910, 0x1658, 3, 0x1268, 4, 16);
        Sprite(ParticleSprite, 0x1048C94, 0x144, 12);
        Sprite(TrailSprite, 0x1048DD8, 0x144, 12);
        Sprite(ImpactSprite, 0x1048F1C, 0x18C, 14);
        if (Camera.Identity != "FF8_EN:104DF68" || Camera.Data.IsDefault || Camera.Data.Length != 916 || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 6) != 916 || CasterDat.Identity != "FF8_EN:1047120" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Sound != 0x55787)
        {
            throw new InvalidDataException("Micro Missiles requires its complete original camera, binding directory and sound.");
        }
        uint[] sections = [3, 0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (Dword(CasterDat.Data, index * 4) != sections[index])
            {
                throw new InvalidDataException("Micro Missiles caster binding directory is incomplete.");
            }
        }
    }

    private static void Source(NativeBattleGfAnimationSource source, uint address, int length, int shapes, int animation, int nodes, int duration)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != shapes || Dword(source.Data, 4) != animation || Dword(source.Data, animation) != nodes || Dword(source.Data, animation + 4) != duration)
        {
            throw new InvalidDataException("Micro Missiles requires its original ACT directory and programme.");
        }
    }
    private static void Sprite(NativeBattleModuleResource resource, uint address, int length, int frames)
    {
        if (resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || Word(resource.Bytes, 8) != frames)
        {
            throw new InvalidDataException("Micro Missiles requires its original sprite directory.");
        }
        for (int frame = 0; frame < frames; frame++)
        {
            _ = resource.ProjectedFrame571C80(frame);
        }
    }
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
}
