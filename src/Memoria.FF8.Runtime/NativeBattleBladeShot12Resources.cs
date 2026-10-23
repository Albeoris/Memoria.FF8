using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBladeShot12Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleStaticMeshResource MeshD, NativeBattleStaticMeshResource MeshE, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleModuleResource SpriteD, NativeBattleModuleResource SpriteE, NativeBattleModuleResource SourceA0, NativeBattleModuleResource SourceA1, NativeBattleModuleResource SourceB0, NativeBattleModuleResource SourceB1, NativeBattleEffectHeapAllocation Camera, uint OpeningSound, uint EndingSound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x5D0, 49, [0, 0, 24, 24, 0, 0, 0, 0]);
        MeshResource(MeshB, MeshBAddress, 0x5D0, 49, [0, 0, 24, 24, 0, 0, 0, 0]);
        MeshResource(MeshC, MeshCAddress, 0x828, 144, [0, 0, 16, 24, 0, 0, 0, 0]);
        MeshResource(MeshD, MeshDAddress, 0x718, 110, [0, 0, 0, 0, 16, 24, 0, 0]);
        MeshResource(MeshE, MeshEAddress, 0x6D0, 69, [0, 0, 0, 36, 0, 0, 0, 8]);
        RequireSprite(SpriteA, SpriteAAddress, 0x1BC, 5);
        RequireSprite(SpriteB, SpriteBAddress, 0x90, 5);
        RequireSprite(SpriteC, SpriteCAddress, 0x194, 15);
        RequireSprite(SpriteD, SpriteDAddress, 0x160, 13);
        RequireSprite(SpriteE, SpriteEAddress, 0x118, 8);
        RequireSource(SourceA0, MorphSourceA0);
        RequireSource(SourceA1, MorphSourceA1);
        RequireSource(SourceB0, MorphSourceB0);
        RequireSource(SourceB1, MorphSourceB1);
        if (Camera is null || Camera.Identity != "FF8_EN:1365634" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || OpeningSound != 0x61AB1 || EndingSound != 0x61AAF)
        {
            throw new InvalidDataException("Blade Shot12 requires its complete original camera and positioned sound descriptors.");
        }
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        MeshCAddress => MeshC,
        MeshDAddress => MeshD,
        MeshEAddress => MeshE,
        _ => throw new NotSupportedException("Blade Shot12 selected an unsupported addressed mesh.")
    };
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        SpriteDAddress => SpriteD,
        SpriteEAddress => SpriteE,
        _ => throw new NotSupportedException("Blade Shot12 selected an unsupported addressed sprite.")
    };
    public NativeBattleModuleResource ReadMorphSource(uint address) => address switch
    {
        MorphSourceA0 => SourceA0,
        MorphSourceA1 => SourceA1,
        MorphSourceB0 => SourceB0,
        MorphSourceB1 => SourceB1,
        _ => throw new NotSupportedException("Blade Shot12 selected an unsupported immutable morph source header.")
    };

    public ImmutableArray<byte> Read(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || new[] { MutableVerticesA, MutableVerticesB }.Any(start => address < start + MutableVerticesLength && end > start))
        {
            throw new InvalidDataException("Blade Shot12 current mutable PE vertices require their State owner.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in Fronts())
        {
            if (address >= start && end <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Blade Shot12 immutable read leaves its complete original resource fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Bytes)> Fronts()
    {
        foreach (NativeBattleStaticMeshResource mesh in new[] { MeshA, MeshB, MeshC, MeshD, MeshE })
        {
            yield return (mesh.Address, mesh.Bytes);
        }
        foreach (NativeBattleModuleResource resource in new[] { SpriteA, SpriteB, SpriteC, SpriteD, SpriteE, SourceA0, SourceA1, SourceB0, SourceB1 })
        {
            yield return (resource.Address, resource.Bytes);
        }
    }
    private static void RequireSource(NativeBattleModuleResource source, uint address)
    {
        if (source is null || source.Address != address || source.Bytes.IsDefault || source.Bytes.Length != MorphSourceLength || BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan()) != MorphSourceLength || BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan().Slice(4)) != MutableVertexCount)
        {
            throw new InvalidDataException("Blade Shot12 requires its complete original 49-vertex immutable morph source.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Blade Shot12 requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
            for (int quad = 0; quad < frame.Quads.Length / 20; quad++)
            {
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan().Slice(quad * 20 + 14));
                if ((flags >> 12) >= 4)
                {
                    throw new InvalidDataException("Blade Shot12 sprite material selector leaves its original four palette bytes.");
                }
            }
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Blade Shot12 requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Blade Shot12 mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Blade Shot12 mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Blade Shot12 mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
