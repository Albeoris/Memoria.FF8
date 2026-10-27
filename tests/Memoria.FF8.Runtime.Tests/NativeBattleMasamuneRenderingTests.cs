using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMasamuneRenderingTests
{
    [Theory]
    [InlineData(false, 64)]
    [InlineData(true, 40)]
    public void Eight_births_use_exact_CRT_order_and_preserve_unwritten_W_and_ring_vertical_velocity(bool ring, int calls)
    {
        NativeCrtRandomState random = new(123);
        NativeCrtRandomState expected = random;
        for (int index = 0; index < calls; index++)
        {
            expected = expected.Draw(out _);
        }
        int bank = NativeBattleMasamuneParticles.Bank(ring);
        int stride = NativeBattleMasamuneParticles.Stride(ring);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(bank, 200 * stride);
        for (int index = 0; index < 200; index++)
        {
            arena = Dword(arena, bank + index * stride, 0);
        }
        NativeBattleMasamuneParticlePreparation result = NativeBattleMasamuneParticles.Prepare(Task(ring), arena, random, ring, false);
        Assert.Equal(expected, result.Random);
        for (int index = 0; index < 8; index++)
        {
            int record = bank + index * stride;
            Assert.Equal(0xFFFFFFFFu, ReadDword(result.Arena, record));
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(record + 14, 2));
            if (ring)
            {
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(record + 18, 2));
                Assert.InRange(Word(result.Arena, record + 10), (short)-49, (short)0);
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => result.Arena.Read(record + 22, 6));
                int angular = Word(result.Arena, record + 30);
                Assert.Equal((Math.Abs(angular) & 1) == 0, angular > 0);
            }
        }
        Assert.Equal(0u, ReadDword(result.Arena, bank + 8 * stride));
    }

    [Fact]
    public void Delay_and_pause_preserve_unknown_record_state_without_random_draws()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E000, 4800);
        NativeBattleModulePoolTask task = Task(true).WriteWord(14, 2);
        NativeBattleMasamuneParticlePreparation paused = NativeBattleMasamuneParticles.Prepare(task, arena, new(42), true, true);
        Assert.Same(task, paused.Task);
        Assert.Same(arena, paused.Arena);
        Assert.Equal(new NativeCrtRandomState(42), paused.Random);
        Assert.True(paused.Delayed);
        NativeBattleMasamuneParticlePreparation tick = NativeBattleMasamuneParticles.Prepare(task, arena, new(42), true, false);
        Assert.Equal((ushort)1, tick.Task.ReadWord(14));
        Assert.Same(arena, tick.Arena);
        Assert.Equal(paused.Random, tick.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Damping_uses_signed_SAR_and_dead_records_clear_only_mask(bool ring)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena = Dword(arena, 0, 3);
        arena = PutWord(arena, 8, 32767);
        arena = PutWord(arena, 10, -22);
        arena = PutWord(arena, 16, -1);
        arena = PutWord(arena, 18, -7);
        arena = PutWord(arena, 20, 9);
        arena = PutWord(arena, 28, 32767);
        arena = PutWord(arena, 30, 30);
        NativeBattleEffectArena next = NativeBattleMasamuneParticles.AdvanceRecord(arena, 0, ring, false);
        Assert.Equal((short)32766, Word(next, 8));
        Assert.Equal((short)0, Word(next, 16));
        Assert.Equal(ring ? (short)-22 : (short)-29, Word(next, 10));
        Assert.Equal(ring ? (short)-7 : (short)-6, Word(next, 18));
        Assert.Equal(ring ? (short)32767 : unchecked((short)32797), Word(next, 28));
        NativeBattleEffectArena dead = NativeBattleMasamuneParticles.AdvanceRecord(arena, 0, ring, true);
        Assert.Equal(0u, ReadDword(dead, 0));
        Assert.Equal(arena.Read(4, 28), dead.Read(4, 28));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Mesh_retains_original_module_packets_and_publishes_actual_context_GTE_and_cold_next(int group)
    {
        NativeBattleStaticMeshResource mesh = new(0, 0xCC72FC, Mesh(group));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena context = Dword(Dword(Dword(Dword(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, mesh.Address), 8, 0), 0x1C, 0xF3), 12, 2048);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.Draw(memory, mesh, matrix, projection, context, Constants, 0x4321);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, new(0xF3, 0, 2048, null, null, 0, 17, 2), Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, ReadDword(result.Context, 0x20));
        Assert.Equal(mesh.Address + 8, ReadDword(result.Context, 4));
        Assert.Equal(0u, ReadDword(result.Context, 0x18));
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, ReadDword(result.Context, 0x2C));
        Assert.Equal(new byte?[] { 0x21, 0x43 }, result.Memory.GeometryRegisters.Rotation20.Skip(18));
        Assert.NotNull(result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x34, 36));
        NativeBattleStaticMeshContext88Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStaticMeshContext88Step>(NativeBattleTestSnapshots.Json(result))!;
        NativeBattleStaticMeshContext88Step Next(NativeBattleStaticMeshContext88Step prior) => NativeBattleStaticMeshContext88.Draw(prior.Memory, mesh, matrix, projection, prior.Context, Constants, 0x4321);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(result)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Unfaded_mesh_initializes_absent_register_containers_only_through_reached_writes(int group)
    {
        NativeBattleStaticMeshResource mesh = new(0, 0xCCDE04, Mesh(group));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Null(restored.GeometryRegisters);
        Assert.Null(restored.ScriptedColorRegisters);
        NativeBattleEffectArena context = Dword(Dword(Dword(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, mesh.Address), 8, 0), 0x1C, 0x33);
        NativeBattleStaticMeshContext88Step Draw(NativeBattleEffectMemory current, NativeBattleEffectArena currentContext) => NativeBattleStaticMeshContext88.Draw(current, mesh, matrix, projection, currentContext, Constants);
        NativeBattleStaticMeshContext88Step result = Draw(memory, context);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Draw(restored, context)));
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, new(0x33, 0, null, null, null, 0, 17, 2), Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.All(result.Memory.GeometryRegisters!.AnchorScratch8, value => Assert.Null(value));
        Assert.All(result.Memory.GeometryRegisters.Rotation20.Skip(18), value => Assert.Null(value));
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Memory.GeometryRegisters.Translation);
        Assert.All(result.Memory.ScriptedColorRegisters!.Rgb, value => Assert.Null(value));
        Assert.Null(result.Memory.ScriptedColorRegisters.Material);
        Assert.All(result.Memory.ScriptedColorRegisters.NormalInputKnown!.Value, Assert.True);
        Assert.Equal((short)(group == 4 ? -400 : 400), BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan()));
        Assert.Equal((short)(group == 4 ? 11 : 44), BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput.Value.AsSpan().Slice(6, 2)));
        if (group == 4)
        {
            Assert.Null(result.Memory.GeometryRegisters.Sz[0]);
        }
        Assert.All(result.Memory.GeometryRegisters.Sz.Skip(1), value => Assert.NotNull(value));
        NativeBattleStaticMeshContext88Step cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleStaticMeshContext88Step>(NativeBattleTestSnapshots.Json(result))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(result.Memory, result.Context)), NativeBattleTestSnapshots.Json(Draw(cold.Memory, cold.Context)));
    }

    [Fact]
    public void Depth_rejected_mesh_preserves_unreached_area_and_color_owners()
    {
        NativeBattleStaticMeshResource mesh = new(0, 0xCCF2DC, Mesh(5));
        NativeBattleEffectArena context = Dword(Dword(Dword(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, mesh.Address), 8, 0), 0x1C, 0x33);
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.Draw(Memory(), mesh, NativeBattleCuragaMatrices.Identity8DD770() with { Z = -1 }, new(null, 288, 160, 108), context, Constants);
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x2C, 4));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(new uint?[] { 1, 2, 3 }, result.Memory.ScriptedColorRegisters.Rgb);
    }

    [Fact]
    public void Light_transform_uses_raw_backcolor_and_full_MAC_preserves_unknown_W_and_does_not_replace_light_controls()
    {
        NativeBattleEffectArena particles = NativeBattleEffectArena.Clear().MarkUnknown(14, 2);
        particles = PutWord(particles, 6, 8);
        particles = PutWord(particles, 8, 100);
        particles = PutWord(particles, 10, -200);
        particles = PutWord(particles, 12, 300);
        byte[] light = new byte[20];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(light.AsSpan(index * 8), 4096);
        }
        NativeBattleScriptedLightingMemory lighting = new(new(light.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray()), null, new(40000, -50000, 60000));
        NativeBattleEffectMemory memory = Memory() with { ScriptedLighting = lighting };
        NativeBattleMasamuneParticleHead head = NativeBattleMasamuneSprites.PrepareParticle(memory, particles, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 72));
        Assert.Equal(new int?[] { 40100, -50200, 60300 }, head.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 32767, -32768, 32767 }, head.Memory.ScriptedColorRegisters.Ir);
        Assert.Same(lighting, head.Memory.ScriptedLighting);
        Assert.Null(head.Memory.GeometryRegisters!.V1[6]);
        Assert.Null(head.Memory.GeometryRegisters.V1[7]);
        Assert.Equal(new byte?[] { 0, 0 }, head.Memory.GeometryRegisters.Rotation20.Skip(18));
        var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(40100, -50200, 60300);
        Assert.Equal(unchecked(40100 + (-normal.X >> 12)), head.Memory.GeometryRegisters.Translation[0]);
        Assert.Equal(unchecked(-50200 + (-normal.Y >> 12)), head.Memory.GeometryRegisters.Translation[1]);
        Assert.Equal(unchecked(60300 + (-normal.Z >> 12)), head.Memory.GeometryRegisters.Translation[2]);
        Assert.Throws<NotSupportedException>(() => head.Scratch72.Read(0, 40));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sprite_publishes_actual_context_angle_and_GTE_while_retaining_vector_W_and_matrix_padding(bool ring)
    {
        byte[] bytes = new byte[40];
        bytes[3] = ring ? (byte)7 : (byte)6;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1);
        bytes[20] = 4;
        bytes[21] = 128;
        bytes[22] = 4;
        bytes[23] = 0x2E;
        NativeBattleModuleResource resource = new(0, ring ? 0xCC6DC8u : 0xCC7120u, 0, 0, bytes.ToImmutableArray());
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, new byte[180]);
        staging = staging.MarkUnknown(address + 74, 2).MarkUnknown(address + 82, 2).MarkUnknown(address + 90, 2).MarkUnknown(address + 98, 2).MarkUnknown(address + 118, 2);
        staging = staging.Write(address + 8, [0x78, 0x56, 0x34, 0x12]);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, 0x1234) };
        NativeBattleMasamuneSpriteStep result = NativeBattleMasamuneSprites.Draw(memory, staging, address, resource, 0, 512, ring, 4096, new(null, 288, 160, 108), 0);
        Assert.NotEmpty(result.Batch.Primitives);
        Assert.Equal(ring ? 0x12345678u : 512u, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(address + 8, 4)));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Null(result.Memory.GeometryRegisters.V1[6]);
        Assert.Null(result.Memory.GeometryRegisters.V1[7]);
        Assert.NotNull(result.Memory.GeometryRegisters.Otz);
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Flags);
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(address + 150, 2)));
    }

    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1, 2, 3] }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleModulePoolTask Task(bool ring) => new NativeBattleModulePoolTask(1, 0, null, ring ? 0x590F10u : 0x590B60u, new byte[24].ToImmutableArray()).WriteWord(22, 0xFFFF);
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint ReadDword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }
    private static ImmutableArray<byte> Mesh(int group)
    {
        int stride = group is 2 or 4 ? 20 : 24;
        byte[] bytes = new byte[72 + stride];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), positions[index]);
        }
        int cursor = 44 + group * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (group switch { 2 => 0x24000000u, 3 => 0x2C000000u, 4 => 0x30000000u, _ => 0x38000000u }) | 0x332211);
        int corners = (group & 1) != 0 ? 4 : 3;
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x554433);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x887766);
        if ((group & 1) != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0xBBAA99);
        }
        return bytes.ToImmutableArray();
    }
}
