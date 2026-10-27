using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275ControllerTests
{
    [Fact]
    public void Eight_subpasses_append_points_before_projection_and_retain_unwritten_material()
    {
        Fixture fixture = new(1);
        NativeBattleElectrocute275Frame frame = fixture.Frame;
        frame = NativeBattleElectrocute275Controller.Write(frame, fixture.TaskAddress, NativeBattleElectrocute275Controller.Read(frame, fixture.TaskAddress).WriteWord(0x1E, 0).WriteWord(0x26, 512));
        var actual = NativeBattleElectrocute275Controller.AdvanceWorldTrail(frame, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(actual.Frame, fixture.TaskAddress);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)4096, task.ReadWord(0x1E));
        Assert.Equal((ushort)9, task.ReadWord(14));
        Assert.Equal(Enumerable.Range(0, 9).Select(fixture.Point), fixture.Chain(actual.Frame));
        Assert.Collection(fixture.Operations, operation => Assert.Equal((short)9, Assert.IsType<NativeBattleElectrocute275ProjectWorld>(operation).Count), operation => Assert.Equal((short)9, Assert.IsType<NativeBattleElectrocute275DrawWorld>(operation).Count));
        Assert.Equal((uint?)0xAB123456, actual.Frame.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(new uint?[] { 0xAB000000, 0xAB000000, 0xAB000000 }, actual.Frame.Memory.ScriptedColorRegisters.Rgb);
    }

    [Fact]
    public void Project_and_draw_observe_pre_motion_positions_and_returned_memory_is_retained()
    {
        Fixture fixture = new(2);
        NativeBattleElectrocute275Frame frame = fixture.Word(fixture.Frame, fixture.Point(0) + 8, 7);
        fixture.OnExecute = (current, operation) =>
        {
            Assert.Equal((short)0, fixture.Short(current, fixture.Point(0)));
            return current with { State = current.State.WriteDword(FadeGlobal, 123) };
        };
        var actual = NativeBattleElectrocute275Controller.AdvanceWorldTrail(frame, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, actual.Result);
        Assert.Equal((short)7, fixture.Short(actual.Frame, fixture.Point(0)));
        Assert.Equal(123u, actual.Frame.State.ReadDword(FadeGlobal));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(2, fixture.Operations.Count);
    }

    [Fact]
    public void Fade_countdown_trims_only_dead_adjacent_heads_after_drawing()
    {
        Fixture fixture = new(3);
        NativeBattleElectrocute275Frame frame = fixture.Frame;
        for (int index = 0; index < 3; index++)
        {
            frame = fixture.Word(fixture.Word(frame, fixture.Point(index) + 0x20, 10), fixture.Point(index) + 0x22, -20);
        }
        var actual = NativeBattleElectrocute275Controller.AdvanceWorldTrail(frame, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(actual.Frame, fixture.TaskAddress);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)1, task.ReadWord(14));
        Assert.Equal(fixture.Point(2), task.ReadDword(0x10));
        Assert.Equal(fixture.Point(1), fixture.Dword(actual.Frame, ArenaAddress + WorldFreePointerOffset));
        Assert.Equal((short)-1, fixture.Short(actual.Frame, fixture.Point(0) + 6));
        Assert.Equal((short)-1, fixture.Short(actual.Frame, fixture.Point(1) + 6));
        Assert.Equal((short)80, fixture.Short(actual.Frame, fixture.Point(2) + 6));
        Assert.Equal((short)3, Assert.IsType<NativeBattleElectrocute275DrawWorld>(fixture.Operations[1]).Count);
    }

    [Theory]
    [InlineData(2, -64, 1, -64)]
    [InlineData(0, 64, 0, -256)]
    [InlineData(0, -384, 0, -384)]
    public void Point_fade_updates_native_life_and_signed_acceleration(short life, short fade, short expectedLife, short expectedFade)
    {
        Fixture fixture = new(2);
        NativeBattleElectrocute275Frame frame = fixture.Word(fixture.Word(fixture.Frame, fixture.Point(0) + 14, life), fixture.Point(0) + 0x22, fade);
        var actual = NativeBattleElectrocute275Controller.AdvanceWorldTrail(frame, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedLife, fixture.Short(actual.Frame, fixture.Point(0) + 14));
        Assert.Equal(expectedFade, fixture.Short(actual.Frame, fixture.Point(0) + 0x22));
    }

    [Fact]
    public void Cold_JSON_chain_repeats_next_callback_without_replaying_births()
    {
        Fixture fixture = new(2);
        NativeBattleElectrocute275Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleElectrocute275Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Frame)))!);
        var expected = NativeBattleElectrocute275Controller.AdvanceWorldTrail(fixture.Frame, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        var actual = NativeBattleElectrocute275Controller.AdvanceWorldTrail(restored, fixture.TaskAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected.Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual.Frame)));
    }

    private sealed class Fixture
    {
        public NativeBattleElectrocute275Frame Frame { get; }
        public uint TaskAddress => ArenaAddress + 0x13A0;
        public List<NativeBattleElectrocute275Operation> Operations { get; } = [];
        public Func<NativeBattleElectrocute275Frame, NativeBattleElectrocute275Operation, NativeBattleElectrocute275Frame>? OnExecute { get; set; }
        public NativeBattleElectrocute275Resources Resources => new(new("unused", []), new(0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new("unused", []), [], new short[4096].ToImmutableArray(), [], [], 0, 0, 4096);
        public uint Point(int index) => ArenaAddress + (uint)(WorldPointsOffset + index * PointStride);

        public Fixture(int count)
        {
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + 0x22A0, 52, 1),
                NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, ArenaAddress + 0x13A0, 60, 64),
                NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, ArenaAddress + 0x9A0, 40, 64),
                NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress + 0x6A0, 24, 32),
                NativeBattleEffectTaskPool.Initialize(ActDescriptor, ArenaAddress, 212, 8)
            ];
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x2C474).Write(WorldFreePointerOffset, BitConverter.GetBytes(0u));
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(WorldPointsOffset + index * PointStride + 6, BitConverter.GetBytes((short)-1));
            }
            for (int index = 0; index < count; index++)
            {
                byte[] point = new byte[40];
                BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(6), 80);
                BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(14), 2);
                BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(32), 512);
                BinaryPrimitives.WriteUInt32LittleEndian(point.AsSpan(36), index + 1 == count ? 0 : Point(index + 1));
                arena = arena.Write(WorldPointsOffset + index * PointStride, point);
            }
            NativeBattleOriginalEffectState effect = new(275, 275, new(275, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAB123456, Rgb = [0x112233, null, 0x778899] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(1) with { Arena = arena, ScriptedColorRegisters = colors };
            NativeBattleElectrocute275Frame frame = new(memory, effect, new(123), NativeBattleElectrocute275State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
            (frame, _) = NativeBattleElectrocute275Controller.Append(frame, 2, 0x6887B0);
            NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(frame, TaskAddress).WriteBytes(12, new byte[48]).WriteWord(14, checked((ushort)count)).WriteDword(0x10, Point(0)).WriteDword(0x14, Point(count - 1)).WriteWord(0x1E, 4096).WriteWord(0x36, ushort.MaxValue).WriteWord(0x38, 80);
            Frame = NativeBattleElectrocute275Controller.Write(frame, TaskAddress, task);
        }

        public short Short(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 2));
        public uint Dword(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 4));
        public NativeBattleElectrocute275Frame Word(NativeBattleElectrocute275Frame frame, uint address, short value) => frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(checked((int)(address - ArenaAddress)), BitConverter.GetBytes(value)) } };
        public IEnumerable<uint> Chain(NativeBattleElectrocute275Frame frame)
        {
            for (uint point = Point(0); point != 0; point = Dword(frame, point + 36))
            {
                yield return point;
            }
        }
        public NativeBattleElectrocute275Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            Operations.Add(operation);
            return OnExecute?.Invoke(frame, operation) ?? frame;
        }) { Colors = new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L)) };
    }
}
