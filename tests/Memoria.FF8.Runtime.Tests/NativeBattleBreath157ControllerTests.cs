using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath157Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath157ControllerTests
{
    [Fact]
    public void Producer_consumes_banks_without_resetting_retained_rotation_or_record_suffix()
    {
        NativeBattleBreath157Frame frame = Frame();
        frame = frame with { State = frame.State.WriteView(0x23ECE20, NativeBattleEffectArena.Clear().MarkUnknown(0, 20), 20).WriteDword(0x23EB628, 0xFFFFFFFF).WriteWord(0x23EB62C, 0xBEEF) with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 32), ChildBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, 3600) } };
        frame = Initialize(frame, Resources(), Inputs());
        Assert.Null(frame.State.RootBankStorage);
        Assert.Null(frame.State.ChildBankStorage);
        Assert.Equal(0u, frame.State.ReadDword(0x23EB628));
        Assert.Equal((ushort)0xBEEF, frame.State.ReadWord(0x23EB62C));
        Assert.Throws<NotSupportedException>(() => frame.State.Read(0x23ECE20, 20));
        Assert.Equal(6u, frame.State.ReadDword(0x23EB5F0));
        Assert.Equal(0x20DFAB8u, frame.State.ReadDword(0x23ECE18));
        Assert.Equal(2, frame.Effect.Pools[0].Slots.Length);
    }

    [Fact]
    public void Paused_director_still_recomputes_both_poses_and_preserves_padding_and_scratch_rotation()
    {
        NativeBattleBreath157Frame frame = Active(0x603F50);
        frame = Put(Put(frame, 0x23ECDF8, new(1, 2, 3)), 0x23EBF88, new(4, 5, 6));
        frame = frame with { State = frame.State.WriteDword(0x23ECDD8, 0).WriteDword(0x23ECDDC, 0).WriteDword(0x23ECDE0, unchecked((uint)-4096)).WriteDword(0x23EB5E0, 0).WriteDword(0x23EB5E4, 0).WriteDword(0x23EB5E8, unchecked((uint)-4096)).WriteView(0x23ECDB0 + 18, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(0x23ECE20, NativeBattleEffectArena.Clear().MarkUnknown(0, 20), 20) };
        NativeBattleBreath157Inputs inputs = Inputs() with { GraphicsFlags = 0x200, ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }, null), ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException(), ReadAnchor571400 = (_, _) => throw new InvalidOperationException() };
        var actual = AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs);
        Assert.Equal(11u, actual.Frame.State.ReadDword(0x23ECDC4));
        Assert.Equal(22u, actual.Frame.State.ReadDword(0x23ECDC8));
        Assert.Equal(33u, actual.Frame.State.ReadDword(0x23ECDCC));
        Assert.Equal(14u, actual.Frame.State.ReadDword(0x23EB5D4));
        Assert.Equal(25u, actual.Frame.State.ReadDword(0x23EB5D8));
        Assert.Equal(36u, actual.Frame.State.ReadDword(0x23EB5DC));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(0x23ECDB0 + 18));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.Read(0x23ECE20, 20));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.Effect, actual.Frame.Effect);
    }

    [Theory]
    [InlineData(0, 0x23ECDF8u, 0x23ECDD8u, 0x23ECDE8u, 550, -640, 360)]
    [InlineData(19, 0x23EBF88u, 0x23EB5E0u, 0x23EB5F4u, 900, -1350, -350)]
    public void Emitter_queries_XYZ_only_and_publishes_normalized_direction_after_old_pose(short age, uint position, uint direction, uint length, short y, short z, short targetZ)
    {
        NativeBattleBreath157Frame frame = Active(0x603F50);
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, (ushort)age));
        frame = Put(frame, 0x23ECDD0, new(0, y, targetZ));
        frame = frame with { State = frame.State.WriteView(position + 6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        NativeBattleBreath157Inputs inputs = Inputs() with { ReadBone502170 = (current, actor, selector, scale) =>
        {
            Assert.Equal(3, actor);
            Assert.Equal(0xF0, selector);
            Assert.Equal(4096, scale);
            return new(current, PointView(new(0, 0, 0)));
        }, ReadAnchor571400 = (current, actor) =>
        {
            Assert.Equal(6, actor);
            return new(current, PointView(new(0, y, targetZ), 1234));
        } };
        var actual = AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs);
        Assert.Equal((ushort)y, actual.Frame.State.ReadWord(position + 2));
        Assert.Equal(unchecked((ushort)z), actual.Frame.State.ReadWord(position + 4));
        Assert.Equal(0u, actual.Frame.State.ReadDword(direction));
        Assert.Equal(0u, actual.Frame.State.ReadDword(direction + 4));
        Assert.Equal(4096u, actual.Frame.State.ReadDword(direction + 8));
        Assert.Equal(1000u, actual.Frame.State.ReadDword(length));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.ReadWord(position + 6));
        Assert.Equal(0u, actual.Frame.State.ReadDword((age == 0 ? 0x23ECDB0u : 0x23EB5C0u) + 24));
    }

    [Fact]
    public void Director_uses_four_CRT_draws_three_indices_and_one_first_contact_operation()
    {
        NativeBattleBreath157Frame frame = Active(0x603F50);
        List<NativeBattleBreath157Operation> operations = [];
        NativeBattleBreath157Inputs inputs = Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 4; draw++)
        {
            expected = expected.Draw(out _);
        }
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 1));
        (frame, _) = AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(2, frame.Effect.Pools[1].Slots.Count(task => task.Callback == 0x6043A0));
        foreach (ushort age in new ushort[] { 20, 32, 43 })
        {
            frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, age));
            (frame, _) = AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs);
        }
        Assert.Equal(new ushort[] { 0, 1, 2 }, frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x604780).Select(task => task.ReadWord(14)));
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 65));
        (frame, _) = AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs);
        Assert.Single(operations, operation => operation is NativeBattleBreath157ContactFirst);
        Assert.Equal(2, operations.Count(operation => operation is NativeBattleBreath157Sound));
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 70));
        Assert.Equal(2, AdvanceDirector(frame, 0x23EBFA0, Resources(), inputs).Result);
    }

    [Theory]
    [InlineData(1000, 171)]
    [InlineData(-101, -17)]
    public void Main_distance_uses_shift_then_signed_truncating_division_and_inherits_raw_sprite_distance(int length, short distance)
    {
        NativeBattleBreath157Frame frame = Active(0x604780);
        frame = frame with { State = frame.State.WriteDword(0x23EB5F4, unchecked((uint)length)) };
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 8).WriteWord(14, 2));
        (frame, _) = AdvanceMain(frame, 0x23EBFA0, Resources(), Inputs());
        Assert.Equal(unchecked((ushort)distance), frame.Effect.Pools[1].Slots[1].ReadWord(20));
        (frame, _) = AdvanceMain(frame, 0x23EBFA0, Resources(), Inputs());
        Assert.Equal(0x604C80u, frame.Effect.Pools[1].Slots[2].Callback);
        Assert.Equal(unchecked((ushort)distance), frame.Effect.Pools[1].Slots[2].ReadWord(20));
        Assert.Equal((ushort)2, frame.Effect.Pools[1].Slots[2].ReadWord(14));
    }

    [Fact]
    public void Main_clears_only_travel_XYZ_and_consumes_nine_CRT_values_at_age12()
    {
        NativeBattleBreath157Frame frame = Active(0x604780);
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(14, 2));
        frame = frame with { State = frame.State.WriteWord(0x23ECE16, 0xCAFE) };
        (frame, _) = AdvanceMain(frame, 0x23EBFA0, Resources(), Inputs());
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(0x23ECE16));
        Assert.Equal((ushort)16384, frame.Effect.Pools[1].Slots[1].ReadWord(28));
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 12));
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 9; draw++)
        {
            expected = expected.Draw(out _);
        }
        (frame, _) = AdvanceMain(frame, 0x23EBFA0, Resources(), Inputs());
        Assert.Equal(expected, frame.Random);
        Assert.Equal(frame.Effect.Pools[1].Slots[2].ReadWord(30), frame.Effect.Pools[1].Slots[3].ReadWord(30));
        frame = Write(frame, 0x23EBFA0, Read(frame, 0x23EBFA0).WriteWord(12, 21));
        Assert.Equal(2, AdvanceMain(frame, 0x23EBFA0, Resources(), Inputs()).Result);
    }

    [Fact]
    public void Common_waiter_runs_after_root_retirement_and_waits_for_single_byte_completion_across_restore()
    {
        NativeBattleBreath157Frame frame = Active(0x603F50);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(children, data => data with { Head = null, Tail = null, Slots = children.Slots.SetItem(0, NativeBattleTaskFixture.EditTask(children.Slots[0], taskData => taskData with { Flags = 0 })) })) } };
        int requests = 0;
        NativeBattleBreath157Inputs inputs = Inputs((current, operation) =>
        {
            var release = Assert.IsType<NativeBattleBreath157ReleaseCommon>(operation);
            Assert.Equal(0x23EB608u, release.TaskAddress);
            requests++;
            return current;
        });
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0, requests);
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[1].ReadWord(12));
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleBreath157Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(1, requests);
        frame = frame with { Effect = WriteCompletionByte(frame.Effect, 0x23EB616, 255) };
        frame = Advance(frame, Resources(), inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal((byte)255, frame.State.RootBankStorage!.Read(30, 1)[0]);
    }

    private static NativeBattleBreath157Frame Active(uint callback)
    {
        NativeBattleBreath157Frame frame = Frame();
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x23EB618, 0x23EB5F8, 16, 2);
        (roots, _) = roots.Append(0x605460);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23EBF90, 0x23EBFA0, 36, 100);
        (children, _) = children.Append(callback);
        return frame with { Effect = frame.Effect with { Pools = [roots, children] }, State = frame.State.WriteDword(0x23ECE18, 0x20DFAB8).WriteDword(0x23ECDF0, 3).WriteDword(0x23EB5F0, 6) with { RootBankStorage = null, ChildBankStorage = null } };
    }
    private static NativeBattleBreath157Frame Frame()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(157, 156, new(157, 0x603E90, 0x603E70), new(3, 0, 1, 0, 0, 0), 0x23EB618) { ResourcePointers = [new(0x23ECDF4, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleBreath157State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleBreath157Frame Put(NativeBattleBreath157Frame frame, uint address, NativeBattleVector point) => frame with { State = frame.State.WriteView(address, PointView(point), 6) };
    private static NativeBattleBreath157Inputs Inputs(Func<NativeBattleBreath157Frame, NativeBattleBreath157Operation, NativeBattleBreath157Frame>? execute = null)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        byte[] payload = new byte[20];
        payload[16] = 1;
        packets = packets with { Headers = packets.Headers.SetItem(0, new(payload.ToImmutableArray(), 0, 2, null)), Contacts = packets.Contacts.SetItem(2, new(new(6, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))) };
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (current, _, _, _) => new(current, PointView(new(0, 0, 0))), (current, _) => new(current, PointView(new(0, 550, 360), 1234)), execute ?? ((current, _) => current));
    }
    private static NativeBattleBreath157Resources Resources()
    {
        byte[] camera = new byte[0xEC];
        BitConverter.GetBytes((ushort)0xEC).CopyTo(camera, 6);
        return new(Mesh(0xDB0DBC, 0x1320, 145, [0, 0, 12, 108, 0, 0, 0, 24]), Mesh(0xDB20DC, 0xE78, 110, [0, 0, 24, 96, 0, 0, 0, 0]), Mesh(0xDB2F54, 0xC88, 108, [0, 0, 0, 96, 0, 0, 0, 0]), Mesh(0xDB3BDC, 0x6E8, 72, [0, 0, 0, 48, 0, 0, 0, 0]), Mesh(0xDB42C4, 0x9E8, 96, [0, 0, 0, 0, 0, 72, 0, 0]), Sprite(0xDB0B34, 0x1AC, 16), Sprite(0xDB0AC0, 0x74, 4), Sprite(0xDB0CE0, 0xDC, 8), Sprite(0xDB0914, 0x1AC, 16), new("FF8_EN:DB0828", camera.ToImmutableArray()), 0x61ABA, 0x61ABD, new(0, 0, -4096));
    }
    private static NativeBattleStaticMeshResource Mesh(uint address, int length, int vertices, int[] counts)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes(8 + vertices * 8).CopyTo(data, 0);
        BitConverter.GetBytes(vertices).CopyTo(data, 4);
        int cursor = 8 + vertices * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            BitConverter.GetBytes(counts[group]).CopyTo(data, cursor);
            cursor += 4 + counts[group] * strides[group];
        }
        return new(0, address, data.ToImmutableArray());
    }
    private static NativeBattleModuleResource Sprite(uint address, int length, int count)
    {
        byte[] data = new byte[length];
        BitConverter.GetBytes((ushort)count).CopyTo(data, 8);
        for (int index = 0; index <= count; index++)
        {
            BitConverter.GetBytes((ushort)0x40).CopyTo(data, 10 + index * 2);
        }
        return new(0, address, 0, 0, data.ToImmutableArray());
    }
}
