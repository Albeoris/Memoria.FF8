using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Controller;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional181ChildrenTests
{
    private static readonly NativeBattleAdditional181Resources Resources = new(null!, null!, null!, null!, null!, null!, 0);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(13, 3796)]
    [InlineData(14, 4096)]
    [InlineData(15, -1)]
    public void Body_uses_actual_caster_and_exact_factor_then_draws_before_pause(int age, int factor)
    {
        var frame = Frame(0x5CB9B0, (ushort)age);
        List<NativeBattleAdditional181Operation> operations = [];
        var actual = AdvanceBody(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleAdditional181DrawBody128 body)
            {
                Assert.Equal(0, body.Actor);
                Assert.Equal(NativeBattleTemporaryMemory.Address, body.TemporaryAddress);
                Assert.Equal((uint)factor, current.Memory.ScriptedColorRegisters!.Ir0);
                Assert.Equal(current.Memory.ScriptedColorRegisters.Rgb[2], Dword(Context(current, body.TemporaryAddress, 128), 0x5C));
                Assert.Equal(240u << 4, current.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
                Assert.Equal(96u << 4, current.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
                if (age == 14)
                {
                    Assert.Equal(0x006060F0u, Dword(Context(current, body.TemporaryAddress, 128), 0x5C));
                }
            }
            return current;
        }, 0x201));
        if (age < 15)
        {
            var flags = Assert.IsType<NativeBattleAdditional181ActorFlags>(operations[0]);
            Assert.Equal(0, flags.Actor);
            Assert.Equal((ushort)0x804, flags.SetMask);
            Assert.Equal((ushort)0, flags.ClearMask);
            Assert.IsType<NativeBattleAdditional181DrawBody128>(operations[1]);
            Assert.Equal(2, operations.Count);
        }
        else
        {
            Assert.Empty(operations);
        }
        Assert.Equal((ushort)age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
        var unpaused = AdvanceBody(frame, ChildBank, Resources, Inputs());
        Assert.Equal(age == 15 ? 2 : 0, unpaused.Result);
        Assert.Equal((ushort)(age + 1), Read(unpaused.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(0x5CC000u, 7, 0x33u, -1)]
    [InlineData(0x5CC000u, 15, 0xF3u, 3584)]
    [InlineData(0x5CC160u, 9, 0x33u, -1)]
    [InlineData(0x5CC160u, 17, 0xF3u, 3584)]
    public void Mesh_cue_thresholds_draw_before_pause_and_complete_on_native_new_age(uint callback, int age, uint flags, int cue)
    {
        var frame = Frame(callback, (ushort)age);
        int calls = 0;
        var input = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional181DrawStatic88>(operation);
            Assert.Equal(callback == 0x5CC000 ? 0xD3E5C4u : 0xD405FCu, draw.SourceAddress);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(flags, Dword(context, 0x1C));
            if (cue >= 0)
            {
                Assert.Equal(cue, unchecked((int)Dword(context, 12)));
            }
            calls++;
            return current;
        });
        var paused = AdvanceMesh(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)age, Read(paused.Frame, ChildBank).ReadWord(12));
        var actual = AdvanceMesh(frame, ChildBank, Resources, input);
        Assert.Equal(age == 15 || age == 17 ? 2 : 0, actual.Result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Mesh_spin_uses_SAR_but_scale_speed_uses_truncating_division_by_seven()
    {
        var frame = Frame(0x5CC000, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1A, unchecked((ushort)-17)).WriteWord(0x1E, unchecked((ushort)-17)));
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs());
        var task = Read(actual.Frame, ChildBank);
        Assert.Equal((short)-17, Signed(task, 0x18));
        Assert.Equal((short)-15, Signed(task, 0x1A));
        Assert.Equal((ushort)4079, task.ReadWord(0x1C));
        Assert.Equal((short)-15, Signed(task, 0x1E));
    }

    [Theory]
    [InlineData(0x5CC2A0u, 7, 42, 2u)]
    [InlineData(0x5CC560u, 2, 8, 4u)]
    public void Particle_births_copy_packed_unknown_W_keep_suffix_and_exact_CRT_draws(uint callback, int births, int draws, uint flags)
    {
        var frame = Frame(callback, 0);
        frame = WriteBytes(frame, ChildBank + 0x16, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
        for (int index = 0; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteView(ParticleBase + (uint)(index * 24) + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(ParticleBase + (uint)(index * 24)) == flags));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(ParticleBase + 14));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(ParticleBase + 22));
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAdditional181Frame>(NativeBattleTestSnapshots.Json(actual.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(AdvanceSlots(actual.Frame, ChildBank, Resources, Inputs()).Frame), NativeBattleTestSnapshots.Json(AdvanceSlots(restored, ChildBank, Resources, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x5CC2A0u, 6)]
    [InlineData(0x5CC560u, 4)]
    public void Particle_partial_bank_uses_one_slot_without_extra_draws(uint callback, int draws)
    {
        var frame = Frame(callback, 0);
        for (int index = 1; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteDword(ParticleBase + (uint)(index * 24), 0x80) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.Equal(0x80u, actual.Frame.State.ReadDword(ParticleBase + 24));
    }

    [Theory]
    [InlineData(0x5CC2A0u, 2u, 83, -12)]
    [InlineData(0x5CC560u, 4u, 100, -17)]
    public void Shared_particle_bank_masks_select_only_their_type_and_motion_policy(uint callback, uint mask, int point, int velocity)
    {
        var frame = Frame(callback, 14);
        frame = frame with { State = frame.State.WriteDword(ParticleBase, mask).WriteDword(ParticleBase + 24, mask == 2 ? 4u : 2u).WriteWord(ParticleBase + 6, 1024).WriteWord(ParticleBase + 8, 100).WriteWord(ParticleBase + 10, 100).WriteWord(ParticleBase + 12, 100).WriteWord(ParticleBase + 16, unchecked((ushort)-17)).WriteWord(ParticleBase + 18, unchecked((ushort)-17)).WriteWord(ParticleBase + 20, unchecked((ushort)-17)) };
        int calls = 0;
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleAdditional181DrawSprite180>(operation);
            Assert.Equal(mask == 2 ? 0xD3E3A0u : 0xD3E1F4u, draw.SourceAddress);
            calls++;
            return Feedback(current, draw.TemporaryAddress, 0);
        }));
        Assert.Equal(1, calls);
        Assert.Equal((ushort)1024, actual.Frame.State.ReadWord(ParticleBase + 6));
        Assert.Equal((ushort)point, actual.Frame.State.ReadWord(ParticleBase + 10));
        Assert.Equal((short)velocity, unchecked((short)actual.Frame.State.ReadWord(ParticleBase + 18)));
        Assert.Equal((ushort)0, actual.Frame.State.ReadWord(ParticleBase + 28));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0x5CC2A0u, 2u)]
    [InlineData(0x5CC560u, 4u)]
    public void Pause_preserves_live_particle_and_feedback_kills_before_physics_unpaused(uint callback, uint flags)
    {
        var frame = Frame(callback, 14);
        frame = frame with { State = frame.State.WriteDword(ParticleBase, flags).WriteWord(ParticleBase + 6, 1024).WriteWord(ParticleBase + 10, 100).WriteWord(ParticleBase + 18, 77) };
        var input = Inputs((current, operation) => Feedback(current, Assert.IsType<NativeBattleAdditional181DrawSprite180>(operation).TemporaryAddress, -1));
        var paused = AdvanceSlots(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal(flags, paused.Frame.State.ReadDword(ParticleBase));
        Assert.Equal((ushort)0, paused.Frame.State.ReadWord(ParticleBase + 4));
        var killed = AdvanceSlots(frame, ChildBank, Resources, input);
        Assert.Equal(2, killed.Result);
        Assert.Equal(0u, killed.Frame.State.ReadDword(ParticleBase));
        Assert.Equal((ushort)100, killed.Frame.State.ReadWord(ParticleBase + 10));
        Assert.Equal((ushort)1, killed.Frame.State.ReadWord(ParticleBase + 4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 2)]
    public void Sprite_lifetime_uses_actual_postdraw_feedback_after_motion(short feedback, int result)
    {
        var frame = Frame(0x5CC720, 99);
        var input = Inputs((current, operation) => Feedback(current, Assert.IsType<NativeBattleAdditional181DrawSprite180>(operation).TemporaryAddress, feedback));
        var paused = AdvanceSprite(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)99, Read(paused.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, paused.Result);
        var actual = AdvanceSprite(frame, ChildBank, Resources, input);
        Assert.Equal(result, actual.Result);
        Assert.Equal((ushort)100, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)202, Read(actual.Frame, ChildBank).ReadWord(0x12));
        Assert.Equal((ushort)307, Read(actual.Frame, ChildBank).ReadWord(0x14));
    }

    private static NativeBattleAdditional181Frame Frame(uint callback, ushort age)
    {
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        var child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        NativeBattleOriginalEffectState effect = new(181, 180, new(181, Entry, Initializer), null, RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleAdditional181Frame frame = new(memory, effect, new(123), NativeBattleAdditional181State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, 0).WriteDword(FirstTargetGlobal, 3) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 77).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 4096).WriteWord(0x1E, 128));
    }

    private static NativeBattleAdditional181Inputs Inputs(Func<NativeBattleAdditional181Frame, NativeBattleAdditional181Operation, NativeBattleAdditional181Frame>? execute = null, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleAdditional181DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattleAdditional181Frame Feedback(NativeBattleAdditional181Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleAdditional181Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
