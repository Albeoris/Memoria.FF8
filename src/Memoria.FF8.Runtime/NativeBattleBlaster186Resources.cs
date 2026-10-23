using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlaster186VertexShape(uint HeaderAddress, uint VertexAddress, uint PolygonOffset, int VertexCount, ImmutableArray<byte> Bytes);

public sealed record NativeBattleBlaster186Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleModuleResource SpriteD, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<NativeBattleBlaster186VertexShape> SourceShapes, ImmutableArray<uint> VertexPointers, ImmutableArray<uint> Colors, ImmutableArray<int> Timings, ImmutableArray<int> Forward)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x808, 102, [0, 0, 0, 50, 0, 0, 0, 0]);
        MeshResource(MeshB, MeshBAddress, 0xAB0, 81, [0, 0, 16, 48, 0, 0, 0, 16]);
        MeshResource(MeshC, MeshCAddress, 0x5F0, 49, [0, 0, 0, 0, 16, 32, 0, 0]);
        RequireSprite(SpriteA, SpriteAAddress, 0x74, 4);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        RequireSprite(SpriteC, SpriteCAddress, 0x1AC, 16);
        RequireSprite(SpriteD, SpriteDAddress, 0x144, 12);
        ValidateSources();
        uint[] expectedPointers = Enumerable.Range(0, 20).Select(index => index % 5 == 0 ? SourceAddresses[index / 5] : MutableFrameAddresses[index / 5 * 4 + index % 5 - 1]).ToArray();
        if (VertexPointers.IsDefault || !VertexPointers.AsSpan().SequenceEqual(expectedPointers) || Colors.IsDefault || !Colors.AsSpan().SequenceEqual(new uint[] { 0, 0x4000, 0x8000, 0xC000, 0x80, 0x4080, 0x8080, 0xC080 }) || Timings.IsDefault || !Timings.AsSpan().SequenceEqual(new int[] { 0, 8, 12, 16, 18, 22, -1 }) || Forward.IsDefault || !Forward.AsSpan().SequenceEqual(new int[] { 0, 0, -4096 }))
        {
            throw new InvalidDataException("Blaster requires its complete original vertex selection, color, timing and direction tables.");
        }
        if (Camera is null || Camera.Identity != "FF8_EN:D1DA60" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61B44)
        {
            throw new InvalidDataException("Blaster requires its complete original camera and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        MeshCAddress => MeshC,
        _ => throw new NotSupportedException("Blaster selected an unsupported addressed mesh.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        SpriteDAddress => SpriteD,
        _ => throw new NotSupportedException("Blaster selected an unsupported addressed sprite.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public void ValidateSources()
    {
        if (SourceShapes.IsDefault || SourceShapes.Length != SourceAddresses.Length)
        {
            throw new InvalidDataException("Blaster requires four original addressed vertex-only shapes.");
        }
        for (int index = 0; index < SourceAddresses.Length; index++)
        {
            NativeBattleBlaster186VertexShape source = SourceShapes[index];
            if (source is null || source.HeaderAddress != SourceAddresses[index] - 8 || source.VertexAddress != SourceAddresses[index] || source.PolygonOffset != VertexFrameBytes + 8 || source.VertexCount != VertexCount || source.Bytes.IsDefault || source.Bytes.Length != VertexFrameBytes)
            {
                throw new InvalidDataException("Blaster vertex-only shape leaves its original header and packed source extent.");
            }
        }
    }

    public NativeBattleEffectArena ReadSourceVertices(uint address)
    {
        ValidateSources();
        NativeBattleBlaster186VertexShape source = SourceShapes.FirstOrDefault(source => source.VertexAddress == address) ?? throw new NotSupportedException("Blaster selected an unsupported immutable vertex source.");
        return NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length).Write(0, source.Bytes.AsSpan());
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Blaster requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Blaster requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Blaster mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Blaster mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Blaster mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
