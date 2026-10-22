using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260ParticlesDrawingTests
{
    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 48)]
    public void Puff_leaf_publishes_full_GTE_without_AVSZ_and_retains_sparse_packet_padding(byte extended, int allocated)
    {
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 999, Mac0 = 777 } };
        NativeBattleShockwavePulsar260PuffStep result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(memory, Temporary(extended), Sprite(), Projection);
        Assert.Equal(allocated, result.Batch.AllocationBytes);
        Assert.Equal(999u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0u, result.Memory.GeometryRegisters.Mac0);
        Assert.All(result.Batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(19, attempt.Primitive!.Bucket));
        NativeBattleGfGeometryAttempt packet = result.Batch.Attempts.First();
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Memory.GeometryRegisters.Sz.ToArray());
        Assert.Equal(0u, U32(result.Temporary180, 0x40));
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x76, 2));
    }

    [Fact]
    public void Rejected_puff_retains_prefix_and_does_not_require_extended_flag_or_fourth_W()
    {
        NativeBattleEffectArena temporary = Temporary(0, 0).MarkUnknown(0x25, 1).MarkUnknown(0x62, 2);
        NativeBattleShockwavePulsar260PuffStep result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), temporary, Sprite(), Projection);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x25, 1));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Puffs_deliver_each_row_before_next_draw_and_publish_retained_physical_temporary_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 2; index++)
        {
            int offset = PuffsOffset + index * 16;
            arena = arena.Write(offset, BitConverter.GetBytes(SpriteAddress)).Write(offset + 10, BitConverter.GetBytes((short)4096)).Write(offset + 15, [1]);
        }
        NativeBattleShockwavePulsar260Frame frame = Frame(arena);
        int delivered = 0;
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(frame, Resources(), Camera, Projection, (current, batch) =>
        {
            delivered++;
            Assert.Equal(48, batch.AllocationBytes);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 180, Read32(current.Staging!, NativeBattleTemporaryMemory.Cursor));
            if (delivered == 2)
            {
                Assert.Equal((short)0x55AA, BinaryPrimitives.ReadInt16LittleEndian(current.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            }
            return current with { Staging = current.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x62, BitConverter.GetBytes((short)0x55AA)) };
        });
        Assert.Equal(2, delivered);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x84, 2)));
        Assert.Equal(1, result.Staging.Read(NativeBattleTemporaryMemory.Address + 0x25, 1)[0]);
        Assert.Equal(frame.Memory.Arena!.Read(PuffsOffset, 32), result.Memory.Arena!.Read(PuffsOffset, 32));
    }

    [Fact]
    public void Empty_banks_read_only_live_fields_and_do_not_require_target_or_source_data()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length);
        for (int index = 0; index < PuffsCount; index++)
        {
            arena = arena.Write(PuffsOffset + index * 16 + 15, [0]);
        }
        for (int index = 0; index < SecondaryParticlesCount; index++)
        {
            arena = arena.Write(SecondaryParticlesOffset + index * 16, [0, 0]);
        }
        NativeBattleShockwavePulsar260Frame frame = Frame(arena);
        NativeBattleShockwavePulsar260Frame first = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(frame, Resources(), Camera, Projection, (_, _) => throw new InvalidOperationException());
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(first, Resources(), Camera, Projection, Constants, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(unchecked((uint)-1024), Read32(result.Staging!, NativeBattleTemporaryMemory.Address + 0x38));
        Assert.Equal(0x30u, Read32(result.Staging!, NativeBattleTemporaryMemory.Address + 0x3C));
    }

    [Fact]
    public void Secondary_uses_target_position_and_truncated_signed_midpoint_then_static30_source()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(SecondaryParticlesOffset, BitConverter.GetBytes((short)1)).Write(SecondaryParticlesOffset + 2, BitConverter.GetBytes((short)7)).Write(SecondaryParticlesOffset + 4, BitConverter.GetBytes((short)2048)).Write(SecondaryParticlesOffset + 6, BitConverter.GetBytes((short)4096)).Write(SecondaryParticlesOffset + 12, BitConverter.GetBytes(0x2500000u));
        NativeBattleEffectArena target = NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0, BitConverter.GetBytes(0x1D97000u)).Write(0x20, BitConverter.GetBytes((short)-3)).Write(0x22, BitConverter.GetBytes((short)-2));
        int calls = 0;
        NativeBattleShockwavePulsar260Frame result = NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(Frame(arena), Resources(), Camera, Projection, Constants, pointer =>
        {
            Assert.Equal(0x2500000u, pointer);
            return target;
        }, pointer =>
        {
            Assert.Equal(0x1D97000u, pointer);
            return new(100, 10, 300);
        }, (current, batch) =>
        {
            calls++;
            Assert.Empty(batch.Attempts);
            Assert.Equal(new int?[] { 100, 15, 2300 }, current.Memory.GeometryRegisters!.Translation.ToArray());
            Assert.Null(current.Memory.GeometryRegisters.Rotation20[18]);
            Assert.Equal(0x111D314u, Read32(current.Staging!, NativeBattleTemporaryMemory.Address + 0x24));
            return current;
        });
        Assert.Equal(1, calls);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Read32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal((short)-4096, BinaryPrimitives.ReadInt16LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address, 2)));
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 8, 2)));
    }

    [Fact]
    public void Static30_mesh_uses_real_plus8_vertices_and_preserves_selected_ACT_guards()
    {
        NativeBattleStaticMeshResource mesh = Mesh(true);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(0x18, BitConverter.GetBytes(-1024)).Write(0x1C, BitConverter.GetBytes(0x30u));
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Camera.ReadMatrix(), true, true, null) };
        NativeBattleShockwavePulsar260MeshStep result = NativeBattleShockwavePulsar260Mesh.Draw(memory, new(mesh.Address, mesh.Bytes), 0, context, Projection, Constants);
        Assert.Equal(mesh.Address + 8, U32(result.Context88, 4));
        Assert.Equal(48, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Equal(0u, U32(result.Context88, 0x2C));
        Assert.Throws<NotSupportedException>(() => NativeBattleShockwavePulsar260Mesh.Draw(memory, new(mesh.Address, mesh.Bytes), 0, context.Write(4, BitConverter.GetBytes(0x20E0000u)).Write(0x1C, BitConverter.GetBytes(0x2030u)), Projection, Constants));
    }

    [Fact]
    public void Puff_unknown_W_and_padding_have_identical_JSON_next_without_rehydration()
    {
        NativeBattleShockwavePulsar260PuffStep first = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Memory(), Temporary(1), Sprite(), Projection);
        NativeBattleShockwavePulsar260PuffStep restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260PuffStep>(JsonSerializer.Serialize(first))!;
        NativeBattleShockwavePulsar260PuffStep Next(NativeBattleShockwavePulsar260PuffStep value) => NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(value.Memory, value.Temporary180, Sprite(), Projection);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    private const uint SpriteAddress = 0x111CF4C;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattlePackedMatrix32 Camera => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleShockwavePulsar260Frame Frame(NativeBattleEffectArena arena)
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[180]));
        return new(Memory() with { Arena = arena }, null!, new(1), NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress)) { Staging = staging };
    }
    private static NativeBattleShockwavePulsar260Resources Resources() => new(null!, [], [Sprite()], Mesh(), [], [], [], []);
    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x80000001);
        bytes[16] = 8;
        bytes[17] = 0x80;
        bytes[18] = 8;
        bytes[19] = 0x2C;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), 4096);
        return new(0, SpriteAddress, 0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleEffectArena Temporary(byte extended, int z = 2000)
    {
        NativeBattleEffectArena value = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x25, [extended]).Write(0x2C, BitConverter.GetBytes(SpriteAddress + 16)).Write(0x30, BitConverter.GetBytes(1));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, 0);
        value = value.Write(0x84, packed.Bytes.AsSpan());
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            value = value.Write(offset, [0, 0]);
        }
        return value;
    }
    private static NativeBattleStaticMeshResource Mesh(bool face = false)
    {
        int directory = face ? 40 : 8;
        byte[] bytes = new byte[directory + 32 + (face ? 24 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, directory);
        if (face)
        {
            short[] values = [-400, -400, 0, 1, 400, -400, 0, 2, -400, 400, 0, 3, 400, 400, 0, 4];
            for (int index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), values[index]);
            }
            int at = directory + 12;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 4), 0x2C112233);
            for (int index = 0; index < 4; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 8 + index * 2), (ushort)(index * 2));
            }
        }
        return new(0, 0x111D30C, bytes.ToImmutableArray());
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Read32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
