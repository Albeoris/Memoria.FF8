using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Controller;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional182ChildrenTests
{
    private static readonly NativeBattleAdditional182Resources Resources = new(null!, null!, MeshVariantAddresses.Select(address => new NativeBattleStaticMeshResource(0, address, [])).ToImmutableArray(), null!, null!, null!, null!, 0x494AB, 0x494C1, [0xC04040, 0xA04060, 0x804080, 0x6040A0, 0x4040C0, 0x6040A0, 0x804080, 0xA04060]);

    [Theory]
    [InlineData(0, 6144)]
    [InlineData(7, 4352)]
    [InlineData(8, 2048)]
    [InlineData(25, 2048)]
    [InlineData(26, 2048)]
    [InlineData(33, 3840)]
    public void Primary_mesh_cue_windows_draw_before_pause(int age, int cue)
    {
        NativeBattleAdditional182Frame frame = Frame(0x5CA6B0, (ushort)age);
        int calls = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional182DrawStatic88>(operation);
            Assert.Equal(0xD3B3D8u, draw.SourceAddress);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0xF3u, Dword(context, 0x1C));
            Assert.Equal(cue, unchecked((int)Dword(context, 12)));
            Assert.Equal(0u, Dword(context, 8));
            calls++;
            return current;
        }, 0x201));
        Assert.Equal(1, calls);
        Assert.Equal((ushort)age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Fact]
    public void Primary_mesh_growth_is_signed_division_and_completion_uses_new_WORD_age()
    {
        var frame = Frame(0x5CA6B0, 33);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1E, unchecked((ushort)-65)));
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs());
        var task = Read(actual.Frame, ChildBank);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)4031, task.ReadWord(0x1C));
        Assert.Equal((short)-67, Signed(task, 0x1E));
        Assert.Equal((ushort)7, task.ReadWord(0x18));
    }

    [Theory]
    [InlineData(0x5CA810u, 0x16, 0u, 1)]
    [InlineData(0x5CA810u, 0x16, 0x201u, 2)]
    [InlineData(0x5CAED0u, 0x18, 0u, 1)]
    [InlineData(0x5CAED0u, 0x18, 0x201u, 2)]
    public void Positive_delays_do_not_allocate_draw_or_consume_random(uint callback, int offset, uint flags, int expected)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(offset, 2));
        NativeBattleAdditional182Inputs inputs = Inputs((_, _) => throw new InvalidOperationException(), flags);
        var actual = callback == 0x5CA810 ? AdvanceMesh(frame, ChildBank, Resources, inputs) : AdvanceSlots(frame, ChildBank, Resources, inputs);
        Assert.Equal((ushort)expected, Read(actual.Frame, ChildBank).ReadWord(offset));
        Assert.Equal((ushort)0, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Staging), NativeBattleTestSnapshots.Json(actual.Frame.Staging));
    }

    [Theory]
    [InlineData(9, 1088, 4096)]
    [InlineData(10, 1024, 4096)]
    [InlineData(13, 1024, 4096)]
    [InlineData(14, 1024, 3968)]
    [InlineData(23, 1024, 3968)]
    public void Delayed_mesh_selects_actual_variant_and_old_age_scale_branch(int age, int y, int xz)
    {
        var frame = Frame(0x5CA810, (ushort)age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 11));
        int draws = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional182DrawStatic88>(operation);
            Assert.Equal(MeshVariantAddresses[11], draw.SourceAddress);
            Assert.Equal(0x33u, Dword(Context(current, draw.TemporaryAddress, 88), 0x1C));
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal((ushort)y, Read(actual.Frame, ChildBank).ReadWord(0x20));
        Assert.Equal((ushort)xz, Read(actual.Frame, ChildBank).ReadWord(0x1C));
        Assert.Equal(age == 23 ? 2 : 0, actual.Result);
    }

    [Fact]
    public void History_is_local_before_camera_and_paused_draw_reuses_one_context_with_cumulative_cues()
    {
        var frame = Frame(0x5CB130, 25);
        for (int index = 0; index < 5; index++)
        {
            frame = SetHistory(frame, index, 700 + index);
        }
        List<int> cues = [];
        List<int> translations = [];
        List<uint> temporaries = [];
        var inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional182DrawStatic88>(operation);
            temporaries.Add(draw.TemporaryAddress);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0xC3u, Dword(context, 0x1C));
            cues.Add(unchecked((int)Dword(context, 12)));
            translations.Add(current.Memory.GeometryRegisters!.Translation[0]!.Value);
            return PublishContext(current, draw.TemporaryAddress, context.Write(0x40, BitConverter.GetBytes(0xAABBCCDDu)), 88);
        }, 0x201) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000 }, null) };
        var actual = AdvanceMesh(frame, ChildBank, Resources, inputs);
        Assert.Equal(new[] { 1536, 3072, 3104, 3168, 3264, 3392 }, cues);
        Assert.Equal(new[] { 1100, 1704, 1703, 1702, 1701, 1100 }, translations);
        Assert.All(temporaries, temporary => Assert.Equal(NativeBattleTemporaryMemory.Address, temporary));
        Assert.Equal(100, unchecked((int)actual.Frame.State.ReadDword(HistoryAddress + 4 + 20)));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.Read(HistoryAddress + 4 + 18, 2));
        Assert.Equal((ushort)25, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
        Assert.Equal(0xAABBCCDDu, Dword(Context(actual.Frame, NativeBattleTemporaryMemory.Address, 88), 0x40));
    }

    [Fact]
    public void History_breaks_at_first_empty_record_and_tail_count_retires_after_pause()
    {
        var frame = SetHistory(Frame(0x5CB130, 34), 3, 700);
        int calls = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            Assert.IsType<NativeBattleAdditional182DrawStatic88>(operation);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)72, Read(actual.Frame, ChildBank).ReadWord(0x12));
        Assert.Equal((ushort)7, Read(actual.Frame, ChildBank).ReadWord(0x1A));
        frame = SetHistory(Frame(0x5CB130, 3), 0, 700);
        calls = 0;
        AdvanceMesh(frame, ChildBank, Resources, Inputs((current, _) =>
        {
            calls++;
            return current;
        }, 0x201));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0x5CA490u, 15)]
    [InlineData(0x5CAED0u, 12)]
    public void Births_preserve_native_W_and_suffix_and_consume_exact_CRT_stream(uint callback, int draws)
    {
        var frame = Frame(callback, 0);
        uint bank = callback == 0x5CA490 ? ParticleBBase : ParticleABase;
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 2));
        for (int index = 0; index < 3; index++)
        {
            frame = frame with { State = frame.State.WriteView(bank + (uint)(index * 24) + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(bank + (uint)(index * 24) + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(callback == 0x5CA490 ? 1u : 2u, actual.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)(callback == 0x5CA490 ? 0 : 6), actual.Frame.State.ReadWord(bank + 4));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(bank + 22));
        if (callback == 0x5CA490)
        {
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(bank + 14));
            Assert.Equal((ushort)0, actual.Frame.State.ReadWord(bank + 16));
            Assert.Equal((ushort)0, actual.Frame.State.ReadWord(bank + 20));
        }
        else
        {
            Assert.Equal((ushort)0, actual.Frame.State.ReadWord(bank + 14));
        }
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAdditional182Frame>(NativeBattleTestSnapshots.Json(actual.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(AdvanceSlots(actual.Frame, ChildBank, Resources, Inputs()).Frame), NativeBattleTestSnapshots.Json(AdvanceSlots(restored, ChildBank, Resources, Inputs()).Frame));
    }

    [Fact]
    public void Falling_particle_uses_SAR_damping_and_actual_phase_color_then_feedback_kills()
    {
        var frame = Frame(0x5CA490, 15);
        frame = frame with { State = frame.State.WriteDword(ParticleBBase, 1).WriteWord(ParticleBBase + 4, 7).WriteWord(ParticleBBase + 6, 1024).WriteWord(ParticleBBase + 10, 100).WriteWord(ParticleBBase + 18, unchecked((ushort)-17)) };
        var input = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional182DrawSprite180>(operation);
            Assert.Equal(Resources.Colors[7], Dword(Context(current, draw.TemporaryAddress, 180), 0x1C));
            return Feedback(current, draw.TemporaryAddress, 0);
        });
        var actual = AdvanceSlots(frame, ChildBank, Resources, input);
        Assert.Equal((ushort)83, actual.Frame.State.ReadWord(ParticleBBase + 10));
        Assert.Equal((short)-15, unchecked((short)actual.Frame.State.ReadWord(ParticleBBase + 18)));
        Assert.Equal((ushort)8, actual.Frame.State.ReadWord(ParticleBBase + 4));
        Assert.Equal(0, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        var killed = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) => Feedback(current, Assert.IsType<NativeBattleAdditional182DrawSprite180>(operation).TemporaryAddress, -1)));
        Assert.Equal(0u, killed.Frame.State.ReadDword(ParticleBBase));
        Assert.Equal(2, killed.Result);
    }

    [Fact]
    public void Trail_selects_signed_DWORD_mask_and_reuses_anchor_context_before_pause()
    {
        var frame = Frame(0x5CAED0, 7);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 2));
        frame = frame with { State = frame.State.WriteDword(ParticleABase, 1).WriteDword(ParticleABase + 24, 2).WriteWord(ParticleABase + 24 + 4, 6).WriteWord(ParticleABase + 24 + 6, 1024).WriteWord(ParticleABase + 24 + 10, 500) };
        List<ushort> phases = [];
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional182DrawSprite180>(operation);
            var context = Context(current, draw.TemporaryAddress, 180);
            phases.Add(BinaryPrimitives.ReadUInt16LittleEndian(context.Read(4, 2)));
            Assert.Equal((ushort)(phases.Count == 1 ? 8 : 0x108), BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2)));
            return PublishContext(current, draw.TemporaryAddress, context.Write(0x24, BitConverter.GetBytes((ushort)0x108)).Write(0x28, BitConverter.GetBytes((short)-1)), 180);
        }, 0x201));
        Assert.Equal(new ushort[] { 7, 6 }, phases);
        Assert.Equal(2u, actual.Frame.State.ReadDword(ParticleABase + 24));
        Assert.Equal((ushort)6, actual.Frame.State.ReadWord(ParticleABase + 28));
        Assert.Equal((ushort)500, actual.Frame.State.ReadWord(ParticleABase + 34));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0x5CA490u)]
    [InlineData(0x5CAED0u)]
    public void Partial_bank_birth_stops_after_first_free_slot_without_extra_random(uint callback)
    {
        var frame = Frame(callback, 0);
        uint bank = callback == 0x5CA490 ? ParticleBBase : ParticleABase;
        int count = callback == 0x5CA490 ? ParticleCountB : ParticleCountA;
        for (int index = 1; index < count; index++)
        {
            frame = frame with { State = frame.State.WriteDword(bank + (uint)(index * 24), 0x80) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < (callback == 0x5CA490 ? 5 : 4); index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.NotEqual(0u, actual.Frame.State.ReadDword(bank));
        Assert.Equal(0x80u, actual.Frame.State.ReadDword(bank + 24));
    }

    [Theory]
    [InlineData(0, 0x201u, 4096, 255, 0)]
    [InlineData(0, 0u, 4096, 255, 1)]
    [InlineData(3, 0u, 312, 19, 1)]
    public void Actor_fade_alpha_recolor_precede_pause_and_flags_follow_age(int age, uint flags, int cue, int alpha, int flagCalls)
    {
        var frame = Frame(0x5CB390, (ushort)age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x18, 4).WriteWord(0x1A, 0x20));
        List<NativeBattleAdditional182Operation> operations = [];
        var actual = AdvanceActorFade(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }, flags));
        Assert.Equal((byte)alpha, Assert.IsType<NativeBattleAdditional182ActorAlpha>(operations[0]).Alpha);
        Assert.Equal(cue, Assert.IsType<NativeBattleAdditional182ActorRecolor>(operations[1]).Cue);
        Assert.Equal(flagCalls, operations.OfType<NativeBattleAdditional182ActorFlags>().Count());
        if (flagCalls != 0)
        {
            var update = Assert.Single(operations.OfType<NativeBattleAdditional182ActorFlags>());
            Assert.Equal((ushort)(age == 0 ? 0 : 0x800), update.ClearMask);
            Assert.Equal((ushort)(age == 0 ? 0x800 : 0x24), update.SetMask);
        }
        Assert.Equal((ushort)(flags == 0 ? age + 1 : age), Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(age == 3 ? 2 : 0, actual.Result);
    }

    private static NativeBattleAdditional182Frame Frame(uint callback, ushort age)
    {
        var pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (pool, _) = pool.Append(callback);
        NativeBattleOriginalEffectState effect = new(182, 181, new(182, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleAdditional182Frame frame = new(memory, effect, new(123), NativeBattleAdditional182State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 0).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 4096).WriteWord(0x1E, 128).WriteWord(0x20, 1024).WriteWord(0x22, 64));
    }

    private static NativeBattleAdditional182Frame SetHistory(NativeBattleAdditional182Frame frame, int index, int x)
    {
        var packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x }, null);
        uint at = HistoryAddress + (uint)(index * 36);
        return frame with { State = frame.State.WriteDword(at, 1).WriteView(at + 4, NativeBattleEffectArena.Clear().Write(0, packed.Bytes.AsSpan()).MarkUnknown(18, 2), 32) };
    }

    private static NativeBattleAdditional182Inputs Inputs(Func<NativeBattleAdditional182Frame, NativeBattleAdditional182Operation, NativeBattleAdditional182Frame>? execute = null, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleAdditional182DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattleAdditional182Frame Feedback(NativeBattleAdditional182Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleAdditional182Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
