using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRestorationMeshAssetsTests
{
    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 90, 2)]
    [InlineData(2, 90, 2)]
    [InlineData(4, 48, 0)]
    [InlineData(5, 48, 0)]
    public void Original_Esuna_mesh_groups_emit_exact_faces_and_source_selected_windows_after_restore(int index, int faces, ushort vertical)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationExecutableState bank = NativeBattleRestorationExecutableState.FromColdProcess(24).Initialize(game.ReadNativeRestorationResources(24).Executable!);
        NativeBattleRestorationExecutableState restored = JsonSerializer.Deserialize<NativeBattleRestorationExecutableState>(JsonSerializer.Serialize(bank))!;
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleEsunaMeshContext context = new(0x30, null, null, 0, 0, vertical, new(0, 0, 256, 256), new(0, 0, vertical == 0 ? (short)256 : (short)128, vertical == 0 ? (short)256 : (short)128), 17, 2);
        ImmutableArray<byte> mesh = ReadMesh(bank, index);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, context);
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleRestorationMeshPackets.EmitBatch88E240(ReadMesh(restored, index), matrix, projection, context)));
        Assert.Equal(faces, batch.Attempts.Count(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24) != 0xE2));
        Assert.Equal(vertical == 0 ? faces : faces * 4, batch.Attempts.Length);
        Assert.Equal(index switch { 0 => 80, 1 or 2 => 90 * 76, _ => 16 * 32 + 32 * 40 }, batch.AllocationBytes);
        if (vertical != 0)
        {
            Assert.Equal(faces, batch.Attempts.Count(attempt => attempt.Primitive?.Words[1] == 0xE2000210));
            Assert.Equal(faces, batch.Attempts.Count(attempt => attempt.Primitive?.Words[1] == 0xE2000000));
        }
        else if (index is 4 or 5)
        {
            Assert.Equal(16, batch.Attempts.Count(attempt => (attempt.Primitive!.Words[1] >> 24 & 0xFC) == 0x24));
            Assert.Equal(32, batch.Attempts.Count(attempt => (attempt.Primitive!.Words[1] >> 24 & 0xFC) == 0x2C));
        }
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(11, 48)]
    public void Original_Full_life_textured_meshes_use_the_shared_exact_material_and_packet_core(int index, int faces)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationExecutableState bank = NativeBattleRestorationExecutableState.FromColdProcess(27).Initialize(game.ReadNativeRestorationResources(27).Executable!);
        ImmutableArray<byte> mesh = ReadMesh(bank, index);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch884370(mesh, matrix, projection, new(0x30, null, null, null, null, null, 17, 2));
        NativeBattleGfGeometryBatch normalized = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh, matrix, projection, new(0x30, null, null, null, null, null, 17, 2));
        Assert.Equal(JsonSerializer.Serialize(normalized), JsonSerializer.Serialize(batch));
        Assert.Equal(faces, batch.Attempts.Length);
        Assert.Equal(faces * 40, batch.AllocationBytes);
    }

    [Theory]
    [InlineData(8, 0x2A6C3D31u)]
    [InlineData(9, 0x2A563964u)]
    public void Actual_Full_life_flat_face_keeps_original_color_while_the_unported_G4_is_explicit(int index, uint color)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRestorationExecutableState bank = NativeBattleRestorationExecutableState.FromColdProcess(27).Initialize(game.ReadNativeRestorationResources(27).Executable!);
        ImmutableArray<byte> mesh = ReadMesh(bank, index);
        int groups = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan());
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(groups)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(groups + 4)));
        ReadOnlySpan<byte> face = mesh.AsSpan().Slice(groups + 8, 12);
        Assert.Equal(color, BinaryPrimitives.ReadUInt32LittleEndian(face));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleModuleProjectedVertex[] vertices = ProjectFace(mesh, face, matrix, projection);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitF4_8846C0(face, corner => vertices[corner], new(0x30, null, null, null, null, null, 17, 2));
        Assert.Equal(color, Assert.Single(batch.Attempts).Primitive!.Words[1]);
        Assert.Equal(24, batch.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationMeshPackets.EmitBatch884370(mesh, matrix, projection, new(0x30, null, null, null, null, null, 17, 2)));
    }

    internal static ImmutableArray<byte> ReadMesh(NativeBattleRestorationExecutableState bank, int index)
    {
        uint address = NativeBattleRestorationProfile.ForEffect(bank.Effect).ExecutableAddress;
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(address + 0x84 + (uint)index * 4, 4));
        return bank.Read(pointer, checked(NativeBattleRestorationExecutableState.Length(bank.Effect) - (int)(pointer - address))).ToImmutableArray();
    }

    internal static NativeBattleModuleProjectedVertex[] ProjectFace(ImmutableArray<byte> mesh, ReadOnlySpan<byte> face, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection)
    {
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[4];
        for (int corner = 0; corner < 4; corner++)
        {
            int offset = 8 + BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + corner * 2)..]) * 4;
            ReadOnlySpan<byte> xyz = mesh.AsSpan().Slice(offset, 6);
            vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..])), projection);
        }
        return vertices;
    }
}
