using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnemyMicroMissiles284Resources(NativeBattleGfAnimationSource Missile, NativeBattleGfAnimationSource Impact, NativeBattleGfAnimationSource WideImpact, NativeBattleModuleResource ParticleSprite, NativeBattleModuleResource TrailSprite, NativeBattleModuleResource ImpactSprite, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        RequireProgramme(Missile, MissileAddress, MissileLength, MissileWorkspaceLength, 22, 45);
        RequireProgramme(Impact, ImpactAddress, ImpactLength, ImpactWorkspaceLength, 2, 12);
        RequireProgramme(WideImpact, WideImpactAddress, WideImpactLength, WideImpactWorkspaceLength, 4, 16);
        if (Camera is null || Camera.Identity != "FF8_EN:1094DA4" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 52 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641CB)
        {
            throw new InvalidDataException("Enemy Micro Missiles requires its complete original camera and positioned sound descriptor.");
        }
        RequireSprite(ParticleSprite, ParticleSpriteAddress, ParticleSpriteLength, 12, 8);
        RequireSprite(TrailSprite, TrailSpriteAddress, TrailSpriteLength, 12, 8);
        RequireSprite(ImpactSprite, ImpactSpriteAddress, ImpactSpriteLength, 14, 6);
        foreach (NativeBattleGfAnimationSource source in new[] { Missile, Impact, WideImpact })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        MissileAddress => Missile,
        ImpactAddress => Impact,
        WideImpactAddress => WideImpact,
        _ => throw new NotSupportedException("Enemy Micro Missiles selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { Missile, Impact, WideImpact })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                uint vertices = Dword(source.Data, offset + 4);
                uint shapes = Dword(source.Data, offset + 8);
                int polygon = checked((int)Dword(source.Data, offset));
                if (vertices is 0 or > 64 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Enemy Micro Missiles requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (0 or 1 or 2 or 3 or 4 or 5 or 7))
                    {
                        throw new NotSupportedException("Enemy Micro Missiles selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Enemy Micro Missiles geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Enemy Micro Missiles face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Enemy Micro Missiles selected an unsupported addressed geometry front.");
    }

    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        ParticleSpriteAddress => ParticleSprite,
        TrailSpriteAddress => TrailSprite,
        ImpactSpriteAddress => ImpactSprite,
        _ => throw new NotSupportedException("Enemy Micro Missiles selected an unsupported projected sprite.")
    };

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int frames, byte kind)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != ((uint)kind << 24 | 0x010524) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Enemy Micro Missiles requires its original kind6/8 projected sprite front.");
        }
        for (int frame = 0; frame < frames; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != (address == ImpactAddress ? 2u : 3u))
        {
            throw new InvalidDataException("Enemy Micro Missiles requires both complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Enemy Micro Missiles requires its original selected node counts and forty-five/twelve/sixteen-frame programmes.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Enemy Micro Missiles resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
