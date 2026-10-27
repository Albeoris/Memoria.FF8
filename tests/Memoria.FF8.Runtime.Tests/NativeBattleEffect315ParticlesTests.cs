using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect315Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315ParticlesTests
{
    [Theory]
    [InlineData(0, 12, 2, 1u)]
    [InlineData(1, 10, 2, 0x10u)]
    [InlineData(2, 15, 3, 0x40u)]
    [InlineData(3, 28, 4, 0x20u)]
    public void Original_birth_formulas_consume_ordered_CRT_and_only_their_selected_shared_rows(int kind, int draws, int births, uint type)
    {
        NativeBattleEffect315Frame frame = Frame(kind, 0);
        List<NativeBattleEffectArena> rows = [];
        NativeBattleEffect315Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleEffect315DrawSprite180 draw = Assert.IsType<NativeBattleEffect315DrawSprite180>(operation);
            rows.Add(ReadBytes(current, ParticleAddress, ParticleStride));
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 180);
            Assert.Equal(SpriteAddresses[kind == 0 ? 0 : kind == 3 ? 2 : 1], U(context, 0));
            Assert.Equal((ushort)(kind == 0 ? 10 : 0), W(context, 0x24));
            if (kind == 0)
            {
                Assert.Equal(4096u, U(context, 12));
                Assert.Equal(2560u, U(context, 16));
            }
            return PublishContext(current, draw.TemporaryAddress, context.Write(0x28, BitConverter.GetBytes((short)0)), 180);
        });
        NativeBattleEffect315Frame actual = Step(frame, kind, inputs).Frame;
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
        Assert.Equal(births, rows.Count);
        NativeBattleEffectArena born = rows[0];
        Assert.Equal(type, U(born, 0));
        short[] first = Enumerable.Range(0, 12).Select(index => S(born, index * 2)).ToArray();
        if (kind == 0)
        {
            Assert.Equal(new short[] { 0, 1312, 114, 215, 335, unchecked((short)0xBEEF), 0, 0, 568, 17 }, first.Skip(2).ToArray());
            NativeBattleEffectArena integrated = ReadBytes(actual, ParticleAddress, ParticleStride);
            Assert.Equal((short)1394, S(integrated, 6));
            Assert.Equal((short)232, S(integrated, 10));
            Assert.Equal((short)903, S(integrated, 12));
            Assert.Equal((short)497, S(integrated, 20));
            Assert.Equal((ushort)0xBEEF, W(integrated, 14));
        }
        else if (kind == 1)
        {
            Assert.Equal(new short[] { 1184, 464, -295, 425 }, first.Skip(3).Take(4).ToArray());
            Assert.Equal((short)-20, S(born, 18));
        }
        else if (kind == 2)
        {
            Assert.Equal(new short[] { 1568, -236, -145, -75 }, first.Skip(3).Take(4).ToArray());
            Assert.Equal((short)-8, S(born, 18));
        }
        else
        {
            Assert.Equal(new short[] { 1184, 2780, -195, 225 }, first.Skip(3).Take(4).ToArray());
            Assert.Equal(new short[] { 58, -10, 22 }, first.Skip(8).Take(3).ToArray());
        }
        Assert.Equal((ushort)1, Read(actual, ChildBank).ReadWord(0xC));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(0, 21)]
    [InlineData(1, 41)]
    [InlineData(2, 96)]
    [InlineData(3, 61)]
    public void Exact_last_birth_boundary_and_full_shared_pool_skip_RNG_and_retire_without_survivors(int kind, int age)
    {
        NativeBattleEffect315Frame frame = Frame(kind, (ushort)age);
        NativeCrtRandomState random = frame.Random;
        (NativeBattleEffect315Frame actual, int result) = Step(frame, kind, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(2, result);
        Assert.Equal(random, actual.Random);
        Assert.Equal((ushort)(age + 1), Read(actual, ChildBank).ReadWord(0xC));
        frame = Frame(kind, 0);
        for (int index = 0; index < ParticleCount; index++)
        {
            frame = frame with { State = frame.State.WriteDword(ParticleAddress + (uint)(index * ParticleStride), 0x80) };
        }
        (actual, result) = Step(frame, kind, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(2, result);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(0x80u, actual.State.ReadDword(ParticleAddress + (ParticleCount - 1) * ParticleStride));
    }

    [Fact]
    public void Positive_delay_does_not_allocate_draw_or_consume_CRT_and_pause_does_not_decrement()
    {
        NativeBattleEffect315Frame frame = Frame(0, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0xE, 2));
        NativeBattleEffect315Frame paused = Step(frame, 0, Inputs((_, _) => throw new InvalidOperationException(), true)).Frame;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(paused)));
        NativeBattleEffect315Frame actual = Step(frame, 0, Inputs((_, _) => throw new InvalidOperationException())).Frame;
        Assert.Equal((ushort)1, Read(actual, ChildBank).ReadWord(0xE));
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.Staging)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual.Staging)));
    }

    [Theory]
    [InlineData(0, 21)]
    [InlineData(1, 41)]
    [InlineData(2, 96)]
    [InlineData(3, 61)]
    public void Paused_rows_still_draw_but_keep_phase_position_flags_age_and_RNG_even_at_end_frame(int kind, int age)
    {
        NativeBattleEffect315Frame frame = Frame(kind, (ushort)age);
        uint type = kind switch { 0 => 1u, 1 => 0x10u, 2 => 0x40u, _ => 0x20u };
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(type)).Write(4, BitConverter.GetBytes((short)5)).Write(6, BitConverter.GetBytes((short)1024)).Write(8, BitConverter.GetBytes((short)100)).Write(10, BitConverter.GetBytes((short)-50)).Write(12, BitConverter.GetBytes((short)1000)).Write(14, BitConverter.GetBytes((ushort)0x7777));
        frame = WriteBytes(frame, ParticleAddress, row, ParticleStride);
        int calls = 0;
        (NativeBattleEffect315Frame actual, int result) = Step(frame, kind, Inputs((current, operation) =>
        {
            calls++;
            NativeBattleEffect315DrawSprite180 draw = Assert.IsType<NativeBattleEffect315DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }, true));
        Assert.Equal(1, calls);
        Assert.Equal(0, result);
        Assert.Equal(row.Read(0, ParticleStride), ReadBytes(actual, ParticleAddress, ParticleStride).Read(0, ParticleStride));
        Assert.Equal((ushort)age, Read(actual, ChildBank).ReadWord(0xC));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Delayed_mask_scans_shared_matching_rows_and_frame_end_clears_only_flag_DWORD()
    {
        NativeBattleEffect315Frame frame = Frame(0, 21);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1A, 3));
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)).Write(6, BitConverter.GetBytes((short)1024)).Write(12, BitConverter.GetBytes((short)1000)).Write(22, BitConverter.GetBytes((short)77));
        frame = WriteBytes(frame, ParticleAddress + ParticleStride, row, ParticleStride);
        (NativeBattleEffect315Frame actual, int result) = Step(frame, 0, Inputs((current, operation) =>
        {
            NativeBattleEffect315DrawSprite180 draw = Assert.IsType<NativeBattleEffect315DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }));
        Assert.Equal(2, result);
        Assert.Equal(0u, actual.State.ReadDword(ParticleAddress + ParticleStride));
        Assert.Equal(row.Read(4, 20), ReadBytes(actual, ParticleAddress + ParticleStride + 4, 20).Read(0, 20));
    }

    [Fact]
    public void Particle_rows_current_alternate_controls_and_released_temporaries_resume_through_JSON()
    {
        NativeBattleEffect315Frame frame = Frame(3, 0);
        NativeBattleEffect315Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleEffect315DrawSprite180 draw = Assert.IsType<NativeBattleEffect315DrawSprite180>(operation);
            return PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 180).Write(0x28, BitConverter.GetBytes((short)0)), 180);
        });
        frame = Step(frame, 3, inputs).Frame;
        NativeBattleEffect315Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect315Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Step(frame, 3, inputs).Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Step(restored, 3, inputs).Frame)));
    }

    private static (NativeBattleEffect315Frame Frame, int Result) Step(NativeBattleEffect315Frame frame, int kind, NativeBattleEffect315Inputs inputs) => kind switch
    {
        0 => AdvanceDelayedParticles(frame, ChildBank, Resources(), inputs),
        1 => AdvanceVerticalParticles(frame, ChildBank, Resources(), inputs, false),
        2 => AdvanceVerticalParticles(frame, ChildBank, Resources(), inputs, true),
        _ => AdvanceOrbitParticles(frame, ChildBank, Resources(), inputs)
    };

    private static NativeBattleEffect315Frame Frame(int kind, ushort age)
    {
        uint callback = kind switch { 0 => 0x599470u, 1 => 0x599860u, 2 => 0x599A90u, _ => 0x59A7F0u };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (pool, int slot) = pool.Append(callback);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(0xC, age).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x1A, 1));
        NativeBattleOriginalEffectState effect = new(315, 314, new(315, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedLighting = NativeBattleScriptedLightingMemory.Unknown() };
        return new(memory, effect, new(12345), NativeBattleEffect315State.FromColdProcess().WriteDword(CasterGlobal, 3) with { ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect315Inputs Inputs(Func<NativeBattleEffect315Frame, NativeBattleEffect315Operation, NativeBattleEffect315Frame> execute, bool paused = false) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => paused ? 0x201u : 0u, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => new(3, 2) { GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() }, _ => throw new InvalidOperationException(), (_, _, _, _, _) => throw new InvalidOperationException(), _ => 0, execute, _ => throw new InvalidOperationException(), (_, _) => false);

    private static NativeBattleEffect315Resources Resources()
    {
        byte[] point = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(point, 10);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), 20);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), 30);
        BinaryPrimitives.WriteUInt16LittleEndian(point.AsSpan(6), 0xBEEF);
        byte[] vector = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(vector.AsSpan(8), 4096);
        return new([], [], new("test", []), 0, 0, new(new string('0', 64), [], []), point.ToImmutableArray(), vector.ToImmutableArray(), [], []);
    }

    private static uint U(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
    private static ushort W(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row.Read(offset, 2));
    private static short S(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
}
