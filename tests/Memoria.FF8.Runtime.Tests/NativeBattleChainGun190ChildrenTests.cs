using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Controller;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChainGun190ChildrenTests
{
    private static readonly NativeBattleChainGun190Resources Resources = new(null!, null!, null!, null!, null!, null!, null!, 0);

    [Theory]
    [InlineData(0x5BB5A0u, -1, 0xD18CDCu, 0x33u, -1, 2)]
    [InlineData(0x5BB5A0u, 1, 0xD18CDCu, 0xF3u, 2048, 2)]
    [InlineData(0x5BBAA0u, 8, 0xD18FDCu, 0xF3u, 4096, 1)]
    [InlineData(0x5BC080u, 0, 0xD192A4u, 0x33u, -1, 1)]
    [InlineData(0x5BC080u, 4, 0xD192A4u, 0xF3u, 3072, 1)]
    public void Mesh_caller_fields_and_draw_count_precede_pause(uint callback, int age, uint source, uint flags, int cue, int count)
    {
        var frame = Frame(callback, unchecked((ushort)age));
        int draws = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleChainGun190DrawStatic88>(operation);
            Assert.DoesNotContain("PilotIdentity", JsonSerializer.Serialize(draw));
            Assert.Equal(source, draw.SourceAddress);
            NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
            Assert.Equal((ushort)190, identity.Effect);
            Assert.Equal(ChildDescriptor, identity.Bank);
            Assert.Equal((long)source, identity.ResourceGeneration);
            Assert.Equal(draws, identity.Primitive);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(flags, Dword(context, 0x1C));
            Assert.Equal(0u, Dword(context, 8));
            if (cue >= 0)
            {
                Assert.Equal(cue, unchecked((int)Dword(context, 12)));
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => context.Read(12, 4));
            }
            draws++;
            return current;
        }, 0x201));
        Assert.Equal(count, draws);
        Assert.Equal(unchecked((ushort)age), Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Fact]
    public void Double_mesh_keeps_setup_translation_then_first_draw_registers_and_context()
    {
        var frame = Frame(0x5BB5A0, 0);
        var view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        var setup = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, view, new(100, 200, 300), 77, 0, -512);
        int draws = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleChainGun190DrawStatic88>(operation);
            Assert.Equal(draws, draw.PilotIdentity!.Value.Primitive);
            Assert.Equal((ushort)190, draw.PilotIdentity.Value.Effect);
            draws++;
            if (draws == 1)
            {
                Assert.Equal(setup.GeometryRegisters!.Translation, current.Memory.GeometryRegisters!.Translation);
                Assert.NotEqual(100, current.Memory.GeometryRegisters.Translation[0]);
                Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(current.Memory.ModuleSpriteBillboard21DFED0!.Bytes.AsSpan()));
                current = PublishContext(current, draw.TemporaryAddress, Context(current, draw.TemporaryAddress, 88).Write(0x40, BitConverter.GetBytes(0xAABBCCDDu)), 88);
                return current with { Memory = current.Memory with { GeometryRegisters = current.Memory.GeometryRegisters! with { Translation = [12345, 23456, 34567] }, ScriptedColorRegisters = current.Memory.ScriptedColorRegisters! with { Flags = 0x12345678 } } };
            }
            Assert.Equal(0xAABBCCDDu, Dword(Context(current, draw.TemporaryAddress, 88), 0x40));
            Assert.Equal(12345, current.Memory.GeometryRegisters!.Translation[0]);
            Assert.Equal(0x12345678u, current.Memory.ScriptedColorRegisters!.Flags);
            return current;
        }, 0x201));
        Assert.Equal(2, draws);
        Assert.Equal(12345, actual.Frame.Memory.GeometryRegisters!.Translation[0]);
    }

    [Theory]
    [InlineData(0x5BB5A0u, 1, 2)]
    [InlineData(0x5BBAA0u, 7, 0)]
    [InlineData(0x5BBAA0u, 8, 2)]
    [InlineData(0x5BC080u, 4, 2)]
    public void Mesh_lifetime_and_speed_damping_use_native_new_age_and_signed_division(uint callback, int age, int result)
    {
        var frame = Frame(callback, (ushort)age);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x1E, unchecked((ushort)-17)));
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs());
        Assert.Equal(result, actual.Result);
        if (callback != 0x5BB5A0)
        {
            Assert.Equal((ushort)4079, Read(actual.Frame, ChildBank).ReadWord(0x1C));
            Assert.Equal((short)-15, Signed(Read(actual.Frame, ChildBank), 0x1E));
        }
    }

    [Fact]
    public void Directional_mesh_postmultiplies_extra_Euler_rotation_and_keeps_composed_translation()
    {
        var frame = Frame(0x5BC080, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x18, 300).WriteWord(0x1A, 700));
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Euler56CD50(new(100, 200, 300)) with { X = 1000, Y = 2000, Z = 3000 };
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(1024, 300, 0)) with { X = 100, Y = 200, Z = 300 };
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(view, local);
        camera = camera with { Rotation = NativeBattleGfMatrices.Multiply56C090(camera, NativeBattleGfMatrices.Euler56CD50(new(0, 700, 0))).Rotation };
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs(flags: 0x201) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(view, null) });
        NativeBattlePackedMatrix32 packed = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(actual.Frame.Memory.GeometryRegisters!);
        Assert.Equal(camera.Rotation, packed.ReadMatrix().Rotation);
        Assert.Equal(camera.X, packed.ReadMatrix().X);
        Assert.Equal(camera.Y, packed.ReadMatrix().Y);
        Assert.Equal(camera.Z, packed.ReadMatrix().Z);
        Assert.False(packed.Known[18]);
        Assert.False(packed.Known[19]);
    }

    [Theory]
    [InlineData(0x5BB9B0u, 0u, 1)]
    [InlineData(0x5BB9B0u, 0x201u, 2)]
    [InlineData(0x5BBEC0u, 0u, 1)]
    [InlineData(0x5BBEC0u, 0x201u, 2)]
    public void Impact_pause_and_delay_prevent_children_and_RNG(uint callback, uint flags, int delay)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 2));
        var actual = AdvanceImpact(frame, ChildBank, Resources, Inputs((_, _) => throw new InvalidOperationException(), flags));
        Assert.Equal((ushort)delay, Read(actual.Frame, ChildBank).ReadWord(14));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Single(actual.Frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Theory]
    [InlineData(0x5BB9B0u, 2, 0x5BBAA0u, 0x5BBC70u)]
    [InlineData(0x5BBEC0u, 6, 0x5BC080u, 0x5BC1F0u)]
    public void Impacts_append_three_literal_children_in_order_with_exact_conditional_stream(uint callback, int draws, uint firstCallback, uint lastCallback)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0).WriteWord(0x18, 9));
        int worldReads = 0;
        var input = Inputs() with
        {
            ReadModelWorld = (_, actor) =>
            {
                Assert.Equal(3, actor);
                worldReads++;
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            },
            ReadModel = (_, actor) =>
            {
                Assert.Equal(2, actor);
                return new(actor, 3) { Rotation14 = 700 };
            }
        };
        var actual = AdvanceImpact(frame, ChildBank, Resources, input);
        Assert.Equal(2, actual.Result);
        NativeBattleModulePoolTask[] children = actual.Frame.Effect.Pools[1].Slots.Skip(1).Take(3).ToArray();
        Assert.Equal(new uint[] { firstCallback, 0x5BBBE0, lastCallback }, children.Select(task => task.Callback));
        Assert.All(children, task => Assert.Equal((ushort)0, task.ReadWord(12)));
        Assert.Equal((ushort)9, children[2].ReadWord(14));
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        if (callback == 0x5BB9B0)
        {
            Assert.Equal(0, worldReads);
            Assert.Equal((short)-200, Signed(children[1], 0x12));
            Assert.All(children, task => Assert.Equal((ushort)77, task.ReadWord(0x16)));
            Assert.Equal((ushort)1024, children[0].ReadWord(0x1C));
            Assert.Equal((ushort)256, children[0].ReadWord(0x1E));
        }
        else
        {
            Assert.Equal(1, worldReads);
            Assert.Equal((ushort)2748, children[0].ReadWord(0x18));
            Assert.Equal((ushort)(children[0].ReadWord(0x1C) / 4), children[0].ReadWord(0x1E));
            Assert.Equal(TaskView(children[0], 0x10, 8).Read(0, 8), TaskView(children[1], 0x10, 8).Read(0, 8));
            Assert.Equal(TaskView(children[0], 0x10, 8).Read(0, 8), TaskView(children[2], 0x10, 8).Read(0, 8));
        }
    }

    [Theory]
    [InlineData(0x5BB6F0u, 3, 21, 1u)]
    [InlineData(0x5BBC70u, 10, 40, 2u)]
    [InlineData(0x5BC1F0u, 12, 84, 2u)]
    public void Particle_births_keep_actual_packed_W_unwritten_suffix_and_exact_RNG(uint callback, int births, int draws, uint group)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, (ushort)group));
        uint bank = callback == 0x5BC1F0 ? ParticleABase : ParticleBBase;
        for (int index = 0; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteView(bank + (uint)(index * 24) + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(bank + (uint)(index * 24)) == group));
        Assert.Equal((ushort)(callback == 0x5BB6F0 ? 55 : 77), actual.Frame.State.ReadWord(bank + 14));
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        if (callback == 0x5BC1F0)
        {
            Assert.InRange(actual.Frame.State.ReadWord(bank + 22), (ushort)0, (ushort)2047);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(bank + 22));
        }
        if (callback == 0x5BBC70)
        {
            Assert.Equal((ushort)0, actual.Frame.State.ReadWord(bank + 18));
            Assert.Equal((ushort)200, actual.Frame.State.ReadWord(bank + 10));
        }
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChainGun190Frame>(NativeBattleTestSnapshots.Json(actual.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(AdvanceSlots(actual.Frame, ChildBank, Resources, Inputs()).Frame), NativeBattleTestSnapshots.Json(AdvanceSlots(restored, ChildBank, Resources, Inputs()).Frame));
    }

    [Theory]
    [InlineData(0x5BB6F0u, 24, 83, -14, -17, -14)]
    [InlineData(0x5BBC70u, 32, 100, -14, -17, -14)]
    [InlineData(0x5BC1F0u, 0, 83, -12, -12, -12)]
    public void Particle_physics_distinguishes_growth_SAR_and_truncating_division(uint callback, int growth, int y, int vx, int vy, int vz)
    {
        var frame = Frame(callback, 24);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1));
        uint bank = callback == 0x5BC1F0 ? ParticleABase : ParticleBBase;
        frame = frame with { State = frame.State.WriteDword(bank, 1).WriteWord(bank + 6, 1024).WriteWord(bank + 8, 100).WriteWord(bank + 10, 100).WriteWord(bank + 12, 100).WriteWord(bank + 16, unchecked((ushort)-17)).WriteWord(bank + 18, unchecked((ushort)-17)).WriteWord(bank + 20, unchecked((ushort)-17)).WriteWord(bank + 22, unchecked((ushort)-1234)) };
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleChainGun190DrawSprite180>(operation);
            var context = Context(current, draw.TemporaryAddress, 180);
            if (callback == 0x5BC1F0)
            {
                Assert.Equal(-1234, unchecked((int)Dword(context, 8)));
                Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2)));
            }
            return Feedback(current, draw.TemporaryAddress, 0);
        }));
        Assert.Equal((ushort)(1024 + growth), actual.Frame.State.ReadWord(bank + 6));
        Assert.Equal((ushort)y, actual.Frame.State.ReadWord(bank + 10));
        Assert.Equal((short)vx, unchecked((short)actual.Frame.State.ReadWord(bank + 16)));
        Assert.Equal((short)vy, unchecked((short)actual.Frame.State.ReadWord(bank + 18)));
        Assert.Equal((short)vz, unchecked((short)actual.Frame.State.ReadWord(bank + 20)));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(0x5BB6F0u, 7)]
    [InlineData(0x5BBC70u, 4)]
    [InlineData(0x5BC1F0u, 7)]
    public void Particle_births_stop_after_one_free_row_without_extra_RNG(uint callback, int draws)
    {
        var frame = Frame(callback, 0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 2));
        uint bank = callback == 0x5BC1F0 ? ParticleABase : ParticleBBase;
        for (int index = 1; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteDword(bank + (uint)(index * 24), 0x80) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        var random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        Assert.NotEqual(0u, actual.Frame.State.ReadDword(bank));
        Assert.Equal(0x80u, actual.Frame.State.ReadDword(bank + 24));
    }

    [Theory]
    [InlineData(0x5BB6F0u)]
    [InlineData(0x5BBC70u)]
    [InlineData(0x5BC1F0u)]
    public void Particle_feedback_before_physics_and_pause_preserves_live_record(uint callback)
    {
        var frame = Frame(callback, 24);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1));
        uint bank = callback == 0x5BC1F0 ? ParticleABase : ParticleBBase;
        frame = frame with { State = frame.State.WriteDword(bank, 1).WriteWord(bank + 6, 1024).WriteWord(bank + 10, 100).WriteWord(bank + 18, 77) };
        var input = Inputs((current, operation) => Feedback(current, Assert.IsType<NativeBattleChainGun190DrawSprite180>(operation).TemporaryAddress, -1));
        var paused = AdvanceSlots(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 });
        Assert.Equal(1u, paused.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)0, paused.Frame.State.ReadWord(bank + 4));
        var killed = AdvanceSlots(frame, ChildBank, Resources, input);
        Assert.Equal(0u, killed.Frame.State.ReadDword(bank));
        Assert.Equal((ushort)1, killed.Frame.State.ReadWord(bank + 4));
        Assert.Equal((ushort)100, killed.Frame.State.ReadWord(bank + 10));
        Assert.Equal((ushort)1024, killed.Frame.State.ReadWord(bank + 6));
        Assert.Equal(2, killed.Result);
    }

    [Fact]
    public void Particle_exact_signed_group_comparison_does_not_use_bit_membership()
    {
        var frame = Frame(0x5BC1F0, 24);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, ushort.MaxValue));
        frame = frame with { State = frame.State.WriteDword(ParticleABase, 0xFFFF).WriteDword(ParticleABase + 24, uint.MaxValue).WriteWord(ParticleABase + 30, 1024) };
        int draws = 0;
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            draws++;
            return Feedback(current, Assert.IsType<NativeBattleChainGun190DrawSprite180>(operation).TemporaryAddress, 0);
        }));
        Assert.Equal(1, draws);
        Assert.Equal((ushort)0, actual.Frame.State.ReadWord(ParticleABase + 4));
        Assert.Equal((ushort)1, actual.Frame.State.ReadWord(ParticleABase + 28));
    }

    [Fact]
    public void Single_sprite_uses_old_phase_before_pause_and_retires_at_sixteen_without_feedback()
    {
        var frame = Frame(0x5BBBE0, 15);
        int calls = 0;
        var input = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleChainGun190DrawSprite180>(operation);
            Assert.Equal(0xD187A0u, draw.SourceAddress);
            Assert.Equal((ushort)15, BinaryPrimitives.ReadUInt16LittleEndian(Context(current, draw.TemporaryAddress, 180).Read(4, 2)));
            calls++;
            return current;
        });
        Assert.Equal(0, AdvanceSprite(frame, ChildBank, Resources, input with { GraphicsFlags = 0x201 }).Result);
        var actual = AdvanceSprite(frame, ChildBank, Resources, input);
        Assert.Equal(2, calls);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)16, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    private static NativeBattleChainGun190Frame Frame(uint callback, ushort age)
    {
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        var child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        NativeBattleOriginalEffectState effect = new(190, 189, new(190, Entry, Initializer), null, RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleChainGun190Frame frame = new(memory, effect, new(123), NativeBattleChainGun190State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(FirstTargetGlobal, 3).WriteDword(CasterGlobal, 2).WriteView(BonePointGlobal, PointView(new(1000, 2000, 3000), 55), 8) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 77).WriteWord(0x18, 0).WriteWord(0x1A, 0).WriteWord(0x1C, 4096).WriteWord(0x1E, 128));
    }

    private static NativeBattleChainGun190Inputs Inputs(Func<NativeBattleChainGun190Frame, NativeBattleChainGun190Operation, NativeBattleChainGun190Frame>? execute = null, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, actor) => new(actor, 3) { Rotation14 = 0 }, (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleChainGun190DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattleChainGun190Frame Feedback(NativeBattleChainGun190Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleChainGun190Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
