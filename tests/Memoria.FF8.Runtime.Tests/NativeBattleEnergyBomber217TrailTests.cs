using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnergyBomber217TrailTests
{
    private const uint Address = 0x1001000;
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    [Theory]
    [InlineData(0x6A, 18)]
    [InlineData(0x6B, 19)]
    public void Actual120_temporary_load_retains_each_padding_byte_independently(int offset, int lane)
    {
        NativeBattleTemporaryAllocation allocation = Allocate();
        NativeBattleFileStaging staging = allocation.Staging.Write(checked(allocation.Address + (uint)offset), [0x7B]);
        NativeBattleEnergyBomber217TrailStep step = Draw(staging, allocation.Address);
        Assert.Equal((byte?)0x7B, step.Memory.GeometryRegisters!.Rotation20[lane]);
        Assert.Null(step.Memory.GeometryRegisters.Rotation20[lane == 18 ? 19 : 18]);
        Assert.Equal(new byte[] { 0x7B }, NativeBattleTemporaryMemory.ReadView(step.Staging, allocation.Address, 120).Read(offset, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleTemporaryMemory.ReadView(step.Staging, allocation.Address, 120).Read(offset == 0x6A ? 0x6B : 0x6A, 1));
    }

    [Fact]
    public void CPU_camera_rotation_and_translation_publish_LONGs_before_identity_R_T_load()
    {
        NativeBattleTemporaryAllocation allocation = Allocate();
        NativeBattleActionMatrix camera = new([8192, 0, 0, 0, 4096, 0, 0, 0, -4096], 50000, -30, 3000, 4096);
        NativeBattleEnergyBomber217TrailStep step = Draw(allocation.Staging, allocation.Address, new(10000, 12, 20), camera);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(step.Staging, allocation.Address, 120);
        Assert.Equal(new int?[] { 70000, -18, 2980 }, step.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Equal(70000, BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(0x6C, 4)));
        Assert.Equal(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null).Bytes.Take(18).ToArray(), temporary.Read(0x58, 18));
    }

    [Fact]
    public void Full120_publication_merges_packet_context_and_preserves_unwritten_fields()
    {
        NativeBattleTemporaryAllocation allocation = Allocate();
        NativeBattleFileStaging staging = allocation.Staging.Write(allocation.Address, Enumerable.Repeat((byte)0xA5, 120).ToArray());
        NativeBattleEnergyBomber217TrailStep step = Draw(staging, allocation.Address);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(step.Staging, allocation.Address, 120);
        Assert.Equal(new byte[4], temporary.Read(8, 4));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, temporary.Read(0x10, 4));
        Assert.Equal(0u, step.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x2E112233u, Assert.Single(step.Batch.Attempts).Primitive!.Words[1]);
        Assert.Equal(Address, BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)));
        Assert.Equal((byte?)0xA5, step.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal((byte?)0xA5, step.Memory.GeometryRegisters.Rotation20[19]);
    }

    [Fact]
    public void Existing_matrix_API_and_loaded_API_share_one_packet_and_control_result()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(3072)).Write(0x1C, BitConverter.GetBytes(0x30u));
        NativeBattleStaticMeshContext88Step existing = NativeBattleStaticMeshContext88.Draw(memory, Mesh(3), matrix, Projection, context, Constants, 0x1234);
        NativeBattleEffectMemory loaded = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, matrix, true, true, 0x1234) };
        NativeBattleStaticMeshContext88Step current = NativeBattleStaticMeshContext88.DrawLoaded(loaded, Mesh(3), Projection, context, Constants);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(existing)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(current)));
    }

    private static NativeBattleTemporaryAllocation Allocate() => NativeBattleTemporaryMemory.Allocate5082B0(NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), 120);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEnergyBomber217TrailStep Draw(NativeBattleFileStaging staging, uint address, NativeBattleVector? point = null, NativeBattleActionMatrix? camera = null) => NativeBattleEnergyBomber217Trail.Draw(Memory(), staging, address, point ?? new(0, 0, 0), Mesh(3), NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), Projection, Constants, 3072);

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), 4);
        short[] vertices = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2, 2), vertices[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face, 4), group == 3 ? 0x2E112233u : 0x3A112233u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8, 2), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10, 2), 6);
        uint[] payload = group == 3 ? [0x12345678, 0x9ABCDEF0, 0x76544321] : [0xAA445566, 0xBB778899, 0xCC8899AA];
        for (int index = 0; index < payload.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12 + index * 4, 4), payload[index]);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
