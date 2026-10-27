using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70ParticleTests
{
    [Fact]
    public void Initialization_clears_only_lifetime_markers_and_sets_distinct_current_cursors()
    {
        NativeBattleEffect70Frame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(SparkOffset, Enumerable.Repeat((byte)0xA5, SparkLength + RingLength).ToArray()) } };
        frame = NativeBattleEffect70Particles.InitializeRings(NativeBattleEffect70Particles.InitializeSparks(frame));
        Assert.Equal(ArenaAddress + SparkOffset, frame.State.ReadDword(SparkCursorGlobal));
        Assert.Equal(ArenaAddress + RingOffset, frame.State.ReadDword(RingCursorGlobal));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(SparkOffset, 1)[0]);
        Assert.Equal((byte)0, frame.Memory.Arena.Read(SparkOffset + 15, 1)[0]);
        Assert.Equal(new byte[] { 0, 0, 0xA5, 0xA5 }, frame.Memory.Arena.Read(RingOffset, 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Allocator_prefers_current_cursor_wraps_and_full_failure_preserves_state(bool ring)
    {
        NativeBattleEffect70Frame frame = Frame();
        int bank = ring ? RingOffset : SparkOffset;
        int count = ring ? 64 : 128;
        uint cursor = ring ? RingCursorGlobal : SparkCursorGlobal;
        uint last = ArenaAddress + (uint)(bank + (count - 1) * 16);
        frame = frame with { State = frame.State.WriteDword(cursor, last) };
        (NativeBattleEffect70Frame next, uint address) = ring ? NativeBattleEffect70Particles.AllocateRing(frame) : NativeBattleEffect70Particles.AllocateSpark(frame);
        Assert.Equal(last, address);
        Assert.Equal(ArenaAddress + (uint)bank, next.State.ReadDword(cursor));
        NativeBattleEffectArena arena = next.Memory.Arena!;
        for (int index = 0; index < count; index++)
        {
            arena = arena.Write(bank + index * 16 + (ring ? 0 : 15), [1]);
        }
        next = next with { Memory = next.Memory with { Arena = arena } };
        (NativeBattleEffect70Frame failed, uint missing) = ring ? NativeBattleEffect70Particles.AllocateRing(next) : NativeBattleEffect70Particles.AllocateSpark(next);
        Assert.Equal(0u, missing);
        Assert.Same(next, failed);
    }

    [Fact]
    public void Final_tick_still_integrates_signed_spark_velocity_and_ring_scale_with_WORD_wrap()
    {
        NativeBattleEffect70Frame frame = Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(SparkOffset + 4, BitConverter.GetBytes((short)32766)).Write(SparkOffset + 12, [2, 0xFE, 0x80, 1]);
        arena = arena.Write(RingOffset, BitConverter.GetBytes((short)1)).Write(RingOffset + 4, BitConverter.GetBytes((short)32767)).Write(RingOffset + 6, BitConverter.GetBytes((short)-32768)).Write(RingOffset + 8, BitConverter.GetBytes((short)1)).Write(RingOffset + 10, BitConverter.GetBytes((short)-1));
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        frame = NativeBattleEffect70Particles.AdvanceRings(NativeBattleEffect70Particles.AdvanceSparks(frame));
        Assert.Equal((short)-32762, Word(frame, SparkOffset + 4));
        Assert.Equal((short)-8, Word(frame, SparkOffset + 6));
        Assert.Equal((short)-512, Word(frame, SparkOffset + 8));
        Assert.Equal((byte)0, frame.Memory.Arena!.Read(SparkOffset + 15, 1)[0]);
        Assert.Equal((short)0, Word(frame, RingOffset));
        Assert.Equal(short.MinValue, Word(frame, RingOffset + 4));
        Assert.Equal(short.MaxValue, Word(frame, RingOffset + 6));
        NativeBattleEffect70Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect70Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect70Particles.AdvanceRings(frame))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect70Particles.AdvanceRings(restored))));
    }

    [Fact]
    public void Disabled_bone_query_retains_current_heap_translation_and_delivery_writes_reach_next_ring()
    {
        NativeBattleEffect70Frame frame = Frame();
        uint target = ArenaAddress + 0x100;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x100, BitConverter.GetBytes(0x1D972C0u)).Write(0x114, BitConverter.GetBytes((short)-3)).Write(0x116, BitConverter.GetBytes((short)-2));
        for (int index = 0; index < 2; index++)
        {
            int offset = RingOffset + index * 16;
            arena = arena.Write(offset, BitConverter.GetBytes((short)1)).Write(offset + 4, BitConverter.GetBytes((short)4096)).Write(offset + 6, BitConverter.GetBytes((short)4096)).Write(offset + 12, BitConverter.GetBytes(target));
        }
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[120]));
        staging = staging.Write(NativeBattleTemporaryMemory.Address + 0x14, BitConverter.GetBytes(100)).Write(NativeBattleTemporaryMemory.Address + 0x18, BitConverter.GetBytes(10)).Write(NativeBattleTemporaryMemory.Address + 0x1C, BitConverter.GetBytes(300));
        frame = frame with { Memory = frame.Memory with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() }, Staging = staging };
        byte[] mesh = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 8);
        NativeBattleEffect70Resources resources = new(null!, [], [], new(0, 0x1324ED8, mesh.ToImmutableArray()), [], [], null!, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty);
        int queries = 0;
        int deliveries = 0;
        NativeBattleEffect70Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new NotSupportedException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException(), _ => 0, (_, _) => 0, (current, operation) =>
        {
            if (operation is NativeBattleEffect70QueryBonePoint query)
            {
                queries++;
                Assert.Equal(0, query.Actor);
                Assert.Equal(NativeBattleTemporaryMemory.Address + 0x14, query.Destination);
                Assert.Equal(queries == 1 ? 10 : 99, BinaryPrimitives.ReadInt32LittleEndian(current.Staging!.Read(query.Destination + 4, 4)));
                return current;
            }
            NativeBattleEffect70DeliverPrivate deliver = Assert.IsType<NativeBattleEffect70DeliverPrivate>(operation);
            Assert.Equal(CursorGlobal, deliver.CursorGlobal);
            Assert.Empty(deliver.Batch.Attempts);
            deliveries++;
            Assert.Equal(deliveries == 1 ? 9 : 98, current.Memory.GeometryRegisters!.Translation[1]);
            return current with { Staging = current.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x18, BitConverter.GetBytes(99)) };
        });
        NativeBattleEffect70Frame result = NativeBattleEffect70Particles.DrawRings(frame, resources, inputs);
        Assert.Equal(2, queries);
        Assert.Equal(2, deliveries);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static short Word(NativeBattleEffect70Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));
    private static NativeBattleEffect70Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(70, 69, new(70, Entry, Initializer), null, null), NativeCrtRandomState.FromColdProcess(), NativeBattleEffect70State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress));
}

