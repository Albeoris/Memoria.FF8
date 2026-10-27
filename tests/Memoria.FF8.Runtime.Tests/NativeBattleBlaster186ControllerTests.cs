using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Controller;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlaster186ControllerTests
{
    [Fact]
    public void Main_updates_capture_geometry_before_pause_but_morphs_only_after_unpaused_gate()
    {
        NativeBattleBlaster186Frame frame = Frame(MainCallback);
        frame = frame with { State = frame.State.WriteWord(MutableFrameAddresses[0], 0x7777).WriteWord(MatrixAddress + 18, 0xBEEF).WriteDword(MatrixAddress + 20, 0x12345678) };
        NativeBattleBlaster186Inputs inputs = Inputs([]) with { GraphicsFlags = 0x201 };
        var paused = AdvanceMain(frame, ChildBank, Resources(), inputs);
        Assert.Equal((short)-300, unchecked((short)paused.Frame.State.ReadWord(CasterPointGlobal + 4)));
        Assert.Equal((short)-4600, unchecked((short)paused.Frame.State.ReadWord(TargetPointAddress + 4)));
        Assert.Equal((ushort)4700, paused.Frame.State.ReadWord(CasterPointGlobal + 6));
        Assert.Equal((ushort)0x7777, paused.Frame.State.ReadWord(MutableFrameAddresses[0]));
        Assert.Equal((ushort)0, Read(paused.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)0xBEEF, paused.Frame.State.ReadWord(MatrixAddress + 18));
        Assert.Equal(0x12345678u, paused.Frame.State.ReadDword(MatrixAddress + 20));
        var active = AdvanceMain(paused.Frame, ChildBank, Resources(), inputs with { GraphicsFlags = 0 });
        Assert.Equal((ushort)19, active.Frame.State.ReadWord(MutableFrameAddresses[0]));
        Assert.Equal((short)-4600, unchecked((short)active.Frame.State.ReadWord(TargetPointAddress + 4)));
        Assert.Equal(frame.Random, active.Frame.Random);
    }

    [Theory]
    [InlineData(7, 1, 0)]
    [InlineData(39, 3, 0)]
    [InlineData(40, 3, 0)]
    [InlineData(60, 0, 0)]
    [InlineData(65, 0, 2)]
    public void Main_uses_original_spawn_contact_sound_and_terminal_stage_ages(ushort age, int births, int resultCode)
    {
        NativeBattleBlaster186Frame frame = Frame(MainCallback);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age));
        List<NativeBattleBlaster186Operation> operations = [];
        var result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations));
        Assert.Equal(births + 1, result.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(resultCode, result.Result);
        if (age == 7)
        {
            Assert.Equal(0x5BFCF0u, Read(result.Frame, ChildBank + 36).Callback);
            Assert.Equal((ushort)1, Read(result.Frame, ChildBank + 36).ReadWord(14));
            Assert.Equal(0x61B44u, Assert.Single(operations.OfType<NativeBattleBlaster186Sound>()).Packed);
        }
        if (age == 39)
        {
            Assert.Equal(new uint[] { 0x5BFC50, 0x5C0410, 0x5C0690 }, result.Frame.Effect.Pools[1].Slots.Skip(1).Take(3).Select(task => task.Callback));
            Assert.Equal((ushort)0xE10, Read(result.Frame, ChildBank + 36).ReadWord(14));
        }
        if (age == 40)
        {
            Assert.Equal(new uint[] { 0x5C0190, 0x5C0750, 0x5C0900 }, result.Frame.Effect.Pools[1].Slots.Skip(1).Take(3).Select(task => task.Callback));
            NativeBattleModulePoolTask mesh = Read(result.Frame, ChildBank + 72);
            Assert.Equal(new byte[] { 100, 0, 200, 0, 44, 1, 52, 18 }, TaskView(mesh, 16, 8).Read(0, 8));
            Assert.Equal((ushort)2148, mesh.ReadWord(24));
        }
        if (age == 60)
        {
            Assert.Equal(0, Assert.Single(operations.OfType<NativeBattleBlaster186ContactsAll>()).HeaderIndex);
        }
        if (age == 65)
        {
            Assert.Equal(new int[] { 0, 0 }, operations.OfType<NativeBattleBlaster186Stage5713E0>().Select(operation => operation.Value));
        }
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Beam_live_bones_update_during_pause_and_three_timing_intervals_emit_exact_pairs()
    {
        NativeBattleBlaster186Frame frame = Frame(0x5BFCF0);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1));
        NativeBattleBlaster186Inputs inputs = Inputs([]);
        var paused = AdvanceBeam(frame, ChildBank, Resources(), inputs with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)200, paused.Frame.State.ReadWord(MovingPointAddress + 6));
        Assert.Equal((ushort)0, Read(paused.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)1, Read(paused.Frame, ChildBank).ReadWord(14));
        int result = 0;
        for (int age = 0; age < 40; age++)
        {
            (frame, result) = AdvanceBeam(frame, ChildBank, Resources(), inputs);
        }
        Assert.Equal(2, result);
        Assert.Equal((ushort)6, Read(frame, ChildBank).ReadWord(14));
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[1].Slots.Skip(1).Take(6).ToArray();
        Assert.Equal(new uint[] { 0x5BFEC0, 0x5C0040, 0x5BFEC0, 0x5C0040, 0x5BFEC0, 0x5C0040 }, children.Select(task => task.Callback));
        Assert.Equal(new ushort[] { 8, 8, 4, 4, 4, 4 }, children.Select(task => task.ReadWord(14)));
        Assert.Equal(new ushort[] { 4, 2, 2 }, children.Where(task => task.Callback == 0x5C0040).Select(task => task.ReadWord(34)));
        Assert.Equal(AfterDraws(new(55), 3), frame.Random);
    }

    [Fact]
    public void Pulse_emits_six_alternating_spins_with_three_draws_each_then_retires_at_thirty()
    {
        NativeBattleBlaster186Frame frame = Frame(0x5C0190);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 1));
        NativeBattleBlaster186Inputs inputs = Inputs([]);
        Assert.Equal(frame, AdvancePulse(frame, ChildBank, inputs with { GraphicsFlags = 0x201 }).Frame);
        int result = 0;
        for (int age = 0; age < 30; age++)
        {
            (frame, result) = AdvancePulse(frame, ChildBank, inputs);
        }
        Assert.Equal(2, result);
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[1].Slots.Skip(1).Take(6).ToArray();
        Assert.All(children, task => Assert.Equal(0x5C0280u, task.Callback));
        Assert.Equal(new short[] { -512, 512, -512, 512, -512, 512 }, children.Select(task => Signed(task, 24)));
        Assert.All(children, task =>
        {
            Assert.Equal((ushort)(task.ReadWord(14) - 4), task.ReadWord(32));
            Assert.Equal((ushort)4, task.ReadWord(34));
            Assert.Equal((ushort)(task.ReadWord(28) / 4), task.ReadWord(30));
        });
        Assert.Equal(AfterDraws(new(55), 18), frame.Random);
    }

    [Fact]
    public void Envelope_draws_fifteen_COMMON_rectangles_with_divide_before_multiply_and_no_negative_draw()
    {
        NativeBattleBlaster186Frame frame = Frame(0x5BFC50);
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(14, 0xE10).WriteWord(28, 255));
        List<NativeBattleBlaster186Operation> operations = [];
        int result = 0;
        for (int age = 0; age < 16; age++)
        {
            if (age == 15)
            {
                Assert.Equal(frame, AdvanceEnvelope(frame, ChildBank, Inputs(operations) with { GraphicsFlags = 0x201 }).Frame);
            }
            (frame, result) = AdvanceEnvelope(frame, ChildBank, Inputs(operations));
        }
        Assert.Equal(2, result);
        NativeBattleBlaster186DrawCommonRectangle571250[] rectangles = operations.Cast<NativeBattleBlaster186DrawCommonRectangle571250>().ToArray();
        Assert.Equal(15, rectangles.Length);
        Assert.Equal(new int[] { 255, 255, 237, 219 }, rectangles.Take(4).Select(rectangle => rectangle.Red));
        Assert.Equal(21, rectangles[^1].Red);
        Assert.All(rectangles, rectangle =>
        {
            Assert.Equal(rectangle.Red >> 2, rectangle.Green);
            Assert.Equal(rectangle.Green, rectangle.Blue);
        });
    }

    [Fact]
    public void Drained_root_restores_asynchronous_waiter_and_transfers_banks_only_after_completion()
    {
        NativeBattleBlaster186Frame frame = Frame(MainCallback);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, NativeBattleTaskFixture.EditTask(children.Slots[0], taskData => taskData with { Flags = 0 })), Head = null, Tail = null });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        int requests = 0;
        NativeBattleBlaster186Inputs inputs = Inputs([]) with { Execute = (current, operation) =>
        {
            NativeBattleBlaster186ReleaseCommon release = Assert.IsType<NativeBattleBlaster186ReleaseCommon>(operation);
            Assert.Equal(ArenaAddress, release.SourceAddress);
            Assert.Equal(RootBank + 16, release.TaskAddress);
            requests++;
            return current;
        } };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0, requests);
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleBlaster186Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = frame with { Effect = WriteCompletionByte(frame.Effect, RootBank + 30, 0xFF) };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(new byte[] { 0xFF, 0 }, frame.State.RootBankStorage!.Read(30, 2));
        Assert.NotNull(frame.State.ChildBankStorage);
    }

    private static NativeBattleBlaster186Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(callback);
        NativeBattleOriginalEffectState effect = new(186, 185, new(186, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [roots, children] };
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(FirstTargetGlobal, 0).WriteDword(WorkingGlobal, ArenaAddress).WriteWord(TargetPointAddress + 4, unchecked((ushort)-5000)) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }

    private static NativeBattleBlaster186Resources Resources()
    {
        ImmutableArray<NativeBattleBlaster186VertexShape> sources = SourceAddresses.Select((address, index) =>
        {
            byte[] bytes = new byte[VertexFrameBytes];
            for (int vertex = 0; vertex < VertexCount; vertex++)
            {
                BitConverter.GetBytes((short)(index * 100)).CopyTo(bytes, vertex * 8);
            }
            return new NativeBattleBlaster186VertexShape(address - 8, address, 0x338, 102, bytes.ToImmutableArray());
        }).ToImmutableArray();
        return new(new(0, MeshAAddress, []), new(0, MeshBAddress, []), new(0, MeshCAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new(0, SpriteDAddress, 0, 0, []), new("unused", []), 0x61B44, sources, [], [], [0, 8, 12, 16, 18, 22, -1], [0, 0, -4096]);
    }

    private static NativeBattleBlaster186Inputs Inputs(List<NativeBattleBlaster186Operation> operations) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 3) { Rotation14 = 100, Transform = new(new(0, 0, 0), new(100, 200, 300), 0x1234, 0, 4096) }, (current, actor, selector, distance) =>
    {
        Assert.Equal(3, actor);
        Assert.Equal(4096, distance);
        NativeBattleVector point = selector switch
        {
            0x21 => new(0, 0, 0),
            0x31 => new(0, 0, -100),
            0x32 => new(0, 0, 100),
            _ => throw new InvalidOperationException()
        };
        return new(current, PointView(point));
    }, (current, actor) =>
    {
        Assert.Equal(0, actor);
        return new(current, PointView(new(0, 0, -5000)));
    }, (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });
}
