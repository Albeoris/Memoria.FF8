using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306StaticMeshTests
{
    [Theory]
    [InlineData(0, 0x33)]
    [InlineData(0, 0xF3)]
    [InlineData(7, 0x33)]
    [InlineData(7, 0xF3)]
    public void Reached_F3_and_GT4_publish_native_GTE_and_actual_caller_ordering(int group, uint flags)
    {
        NativeBattleStaticMeshResource mesh = new(0, 0xCE643C, Mesh(group));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectArena context = Context(mesh.Address, flags);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleStaticMeshContext88Step current = NativeBattleStaticMeshContext88.Draw(memory, mesh, matrix, Projection, context, Colors, null, 4122, 14);
        NativeBattleGfGeometryAttempt packet = Assert.Single(current.Batch.Attempts);
        Assert.Equal(group == 0 ? 20 : 52, packet.PacketLength);
        Assert.Equal(4122, packet.Primitive!.Bucket);
        Assert.Equal(current.Memory.GeometryRegisters!.Otz, BinaryPrimitives.ReadUInt32LittleEndian(current.Context.Read(0x2C, 4)));
        Assert.Equal(0x120u, current.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(current.Context.Read(0x20, 4)));
        Assert.NotNull(current.Memory.ScriptedColorRegisters!.NormalInput);
        if (flags == 0x33)
        {
            Assert.Null(current.Memory.ScriptedColorRegisters.Material);
        }
        else
        {
            Assert.NotNull(current.Memory.ScriptedColorRegisters.Material);
        }
        NativeBattleStaticMeshContext88Step restored = JsonSerializer.Deserialize<NativeBattleStaticMeshContext88Step>(JsonSerializer.Serialize(current))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStaticMeshContext88.Draw(current.Memory, mesh, matrix, Projection, current.Context, Colors, null, 4122, 14)), JsonSerializer.Serialize(NativeBattleStaticMeshContext88.Draw(restored.Memory, mesh, matrix, Projection, restored.Context, Colors, null, 4122, 14)));
    }

    [Fact]
    public void F3_near_reject_preserves_unknown_cue_and_material_and_native_prefix()
    {
        NativeBattleStaticMeshResource mesh = new(0, 0xCE643C, Mesh(0));
        NativeBattleEffectArena context = Context(mesh.Address, 0xF3).MarkUnknown(12, 4);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.Draw(Memory(), mesh, NativeBattleCuragaMatrices.Identity8DD770(), Projection, context, Colors);
        NativeBattleGfGeometryAttempt rejected = Assert.Single(result.Batch.Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[8]);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint address, uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(address)).Write(8, BitConverter.GetBytes(0x563412u)).Write(12, BitConverter.GetBytes(2048)).Write(0x1C, BitConverter.GetBytes(flags));

    private static ImmutableArray<byte> Mesh(int group)
    {
        int stride = group == 0 ? 12 : 36;
        byte[] bytes = new byte[72 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), positions[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 0 ? 0x20112233u : 0x3C112233u);
        for (int corner = 0; corner < (group == 0 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x30223344);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x30445566);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x30778899);
        }
        return bytes.ToImmutableArray();
    }
}
