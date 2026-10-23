using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Controller;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlaster186ChildrenTests
{
    private static readonly NativeBattleBlaster186Resources Resources = new(null!, null!, null!, null!, null!, null!, null!, null!, 0, [], Enumerable.Range(0, 20).Select(index => 0x22B7FB0u + (uint)index * 0x330).ToImmutableArray(), [0, 0x4000, 0x8000, 0xC000, 0x80, 0x4080, 0x8080, 0xC080], [], []);

    [Theory]
    [InlineData(0x5C0040u, 2, 0xD20204u, 0xF3u, 0)]
    [InlineData(0x5C0280u, 3, 0xD1EF4Cu, 0x20F3u, 1024)]
    [InlineData(0x5C0750u, 17, 0xD1F754u, 0x14F3u, 512)]
    public void Mesh_publishes_exact_caller_fields_before_pause(uint callback, ushort age, uint source, uint flags, int cue)
    {
        NativeBattleBlaster186Frame frame = Frame(callback, age);
        int calls = 0;
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBlaster186DrawStatic88>(operation);
            Assert.Equal(source, draw.SourceAddress);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(flags, Dword(context, 0x1C));
            Assert.Equal(cue, unchecked((int)Dword(context, 12)));
            if (callback == 0x5C0280)
            {
                Assert.Equal(Resources.VertexPointers[7], Dword(context, 4));
            }
            if (callback == 0x5C0750)
            {
                Assert.Equal(0u, Dword(context, 0x10));
                Assert.Equal(0x4000u, Dword(context, 0x18));
            }
            calls++;
            return current;
        }, 0x201));
        Assert.Equal(1, calls);
        Assert.Equal(0, actual.Result);
        Assert.Equal(age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)7, Read(actual.Frame, ChildBank).ReadWord(0x1A));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Theory]
    [InlineData(0x5BFEC0u, 1, 0xD1DD5Cu, 8, 1)]
    [InlineData(0x5C0690u, 13, 0xD1DDD0u, 0, 13)]
    [InlineData(0x5C0690u, 21, 0xD1DDD0u, 4, 5)]
    public void Sprite_flags_frame_and_partial_tint_are_native(uint callback, ushort age, uint source, ushort flags, ushort phase)
    {
        var actual = AdvanceSprite(Frame(callback, age), ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBlaster186DrawSprite180>(operation);
            Assert.Equal(source, draw.SourceAddress);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 180);
            Assert.Equal(flags, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2)));
            Assert.Equal(phase, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(4, 2)));
            if (flags == 4)
            {
                Assert.Equal(new byte[] { 16, 16, 16 }, context.Read(0x1C, 3));
                Assert.Throws<NotSupportedException>(() => context.Read(0x1F, 1));
            }
            return current;
        }, 0x201));
        Assert.Equal(age, Read(actual.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Fact]
    public void Fade_division_is_deferred_until_threshold_and_shape_phase_advances_after_draw()
    {
        NativeBattleBlaster186Frame frame = Frame(0x5C0280, 1);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(0x22, 0));
        var actual = AdvanceMesh(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBlaster186DrawStatic88>(operation);
            Assert.Throws<NotSupportedException>(() => Context(current, draw.TemporaryAddress, 88).Read(12, 4));
            return current;
        }));
        Assert.Equal((ushort)8, Read(actual.Frame, ChildBank).ReadWord(0x1A));
        Assert.Throws<DivideByZeroException>(() => AdvanceMesh(actual.Frame, ChildBank, Resources, Inputs()));
    }

    [Fact]
    public void Actor_Z_is_written_after_draw_and_restored_on_native_completion()
    {
        List<NativeBattleBlaster186SetActorZWord> changes = [];
        int draws = 0;
        var actual = AdvanceMesh(Frame(0x5C0750, 23), ChildBank, Resources, Inputs((current, operation) =>
        {
            if (operation is NativeBattleBlaster186DrawStatic88)
            {
                draws++;
            }
            else
            {
                Assert.Equal(1, draws);
                changes.Add(Assert.IsType<NativeBattleBlaster186SetActorZWord>(operation));
            }
            return current;
        }));
        Assert.Equal(2, actual.Result);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(3, change.Actor));
        Assert.Equal((short)300, changes[^1].Value);
        Assert.Equal((ushort)24, Read(actual.Frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(0x5C0410u, 2, 8, 1u)]
    [InlineData(0x5C0900u, 4, 32, 2u)]
    public void Births_retain_W_suffix_and_consume_exact_random_draws(uint callback, int births, int draws, uint flags)
    {
        NativeBattleBlaster186Frame frame = Frame(callback, 0);
        for (int index = 0; index < 100; index++)
        {
            frame = frame with { State = frame.State.WriteView(ParticleBase + (uint)index * 24 + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(ParticleBase + (uint)index * 24 + 22, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        }
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs());
        Assert.Equal(births, Enumerable.Range(0, 100).Count(index => actual.Frame.State.ReadDword(ParticleBase + (uint)index * 24) == flags));
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Frame.Random);
        if (callback == 0x5C0410)
        {
            Assert.Equal((ushort)5000, actual.Frame.State.ReadWord(ParticleBase + 14));
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(ParticleBase + 24 + 14));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(ParticleBase + 14));
        }
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(ParticleBase + 22));
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
        NativeBattleBlaster186Frame restored = JsonSerializer.Deserialize<NativeBattleBlaster186Frame>(JsonSerializer.Serialize(actual.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(AdvanceSlots(actual.Frame, ChildBank, Resources, Inputs()).Frame), JsonSerializer.Serialize(AdvanceSlots(restored, ChildBank, Resources, Inputs()).Frame));
    }

    [Fact]
    public void Local_particles_use_actual_LIGHT_full_MAC_and_COMMON_cursor_with_180_then_72()
    {
        NativeBattleBlaster186Frame frame = Frame(0x5C0900, 15);
        frame = frame with { State = frame.State.WriteWord(TargetPointAddress, 30000).WriteDword(ParticleBase, 2).WriteWord(ParticleBase + 6, 1024).WriteWord(ParticleBase + 8, 30000).WriteWord(ParticleBase + 10, 1000).WriteWord(ParticleBase + 12, 2000).WriteView(ParticleBase + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteWord(ParticleBase + 16, unchecked((ushort)-9)) };
        var actual = AdvanceSlots(frame, ChildBank, Resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBlaster186DrawCommonSprite180>(operation);
            Assert.Equal(NativeBattleTemporaryMemory.Address, draw.TemporaryAddress);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 252, Cursor(current));
            Assert.Equal(60000, current.Memory.ScriptedColorRegisters!.Mac[0]);
            Assert.Equal(32767, current.Memory.ScriptedColorRegisters.Ir[0]);
            Assert.True(current.Memory.GeometryRegisters!.Translation[0] > 50000);
            Assert.Equal((byte)0, current.Memory.GeometryRegisters.Rotation20[18]);
            Assert.False(current.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
            return Feedback(current, draw.TemporaryAddress, 0);
        }));
        Assert.Equal((ushort)29991, actual.Frame.State.ReadWord(ParticleBase + 8));
        Assert.Equal((short)-6, unchecked((short)actual.Frame.State.ReadWord(ParticleBase + 16)));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
    }

    [Theory]
    [InlineData(0x5C0410u, 1u)]
    [InlineData(0x5C0900u, 2u)]
    public void Paused_draw_keeps_physics_and_rng_then_feedback_retires_only_unpaused(uint callback, uint flags)
    {
        NativeBattleBlaster186Frame frame = Frame(callback, 17);
        frame = frame with { State = frame.State.WriteDword(ParticleBase, flags).WriteWord(ParticleBase + 6, 1024).WriteWord(ParticleBase + 12, 1000) };
        NativeBattleBlaster186Frame Draw(NativeBattleBlaster186Frame current, NativeBattleBlaster186Operation operation) => operation switch
        {
            NativeBattleBlaster186DrawSprite180 sprite => Feedback(current, sprite.TemporaryAddress, -1),
            NativeBattleBlaster186DrawCommonSprite180 sprite => Feedback(current, sprite.TemporaryAddress, -1),
            _ => throw new InvalidOperationException()
        };
        var paused = AdvanceSlots(frame, ChildBank, Resources, Inputs(Draw, 0x201));
        Assert.Equal(flags, paused.Frame.State.ReadDword(ParticleBase));
        Assert.Equal((ushort)0, paused.Frame.State.ReadWord(ParticleBase + 4));
        Assert.Equal(frame.Random, paused.Frame.Random);
        var retired = AdvanceSlots(frame, ChildBank, Resources, Inputs(Draw));
        Assert.Equal(2, retired.Result);
        Assert.Equal(0u, retired.Frame.State.ReadDword(ParticleBase));
        Assert.Equal((ushort)1, retired.Frame.State.ReadWord(ParticleBase + 4));
    }

    private static NativeBattleBlaster186Frame Frame(uint callback, ushort age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (pool, _) = pool.Append(callback);
        NativeBattleOriginalEffectState effect = new(186, 185, new(186, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleBlaster186Frame frame = new(memory, effect, new(123), NativeBattleBlaster186State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(FirstTargetGlobal, 3).WriteView(MatrixAddress, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null)), 32).WriteView(MovingPointAddress, PointView(new(100, 200, 300), 820), 8).WriteView(TargetPointAddress, PointView(new(100, 200, 300)), 8).WriteView(CasterPointGlobal, PointView(new(100, 200, 300), 5000), 8).WriteDword(DirectionAddress, unchecked((uint)-4096)).WriteDword(DirectionAddress + 4, 0).WriteDword(DirectionAddress + 8, 0) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 24).WriteWord(0x14, 300).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 1024).WriteWord(0x20, 2).WriteWord(0x22, 4));
    }

    private static NativeBattleBlaster186Inputs Inputs(Func<NativeBattleBlaster186Frame, NativeBattleBlaster186Operation, NativeBattleBlaster186Frame>? execute = null, uint flags = 0) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, flags, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation switch { NativeBattleBlaster186DrawSprite180 draw => Feedback(current, draw.TemporaryAddress, 0), NativeBattleBlaster186DrawCommonSprite180 draw => Feedback(current, draw.TemporaryAddress, 0), _ => current }));
    private static NativeBattleBlaster186Frame Feedback(NativeBattleBlaster186Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleBlaster186Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
