using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSpawnTests
{
    internal const int Emitter = 0xC000;
    internal const int Node = Emitter + 0x25F8;

    [Fact]
    public void Equal_parameter_bounds_preserve_CRT_state_and_initialize_all_four_instance_vectors_without_touching_padding()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x27] = 0;
            foreach (int offset in new[] { 0x4C, 0x50 })
            {
                BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(offset), 5);
            }
            foreach (int offset in new[] { 0x54, 0x58 })
            {
                BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(offset), 7);
            }
            foreach (int offset in new[] { 0x5C, 0x60 })
            {
                BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(offset), 11);
            }
            for (int vector = 0; vector < 3; vector++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(0x68 + vector * 32 + axis * 4), vector * 10 + axis + 1);
                    BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(0x78 + vector * 32 + axis * 4), vector * 10 + axis + 1);
                }
            }
        });
        frame = NativeBattleCuragaEmitterTests.Dwords(frame, Emitter + 0x4C, [65536, 131072, 196608, 0xDEADBEEF]);
        NativeBattleCuragaSpawnStep step = NativeBattleCuragaNodeKinematics.Spawn8818A0(frame, fixture.Task, Emitter, 4, new(99));
        Assert.Equal(99u, step.Random.Seed);
        Assert.Equal(Node, step.Node);
        for (int instance = 0; instance < 4; instance++)
        {
            Assert.Equal(new uint[] { 65536, 131072, 196608, 0xDEADBEEF }, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0xDC + instance * 16, 4));
            Assert.Equal(new uint[] { 65536, 131072, 196608, 0xDEADBEEF }, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x12C + instance * 16, 4));
            Assert.Equal(5u, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x174 + instance * 4, 1)[0]);
            Assert.Equal(7u, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x184 + instance * 4, 1)[0]);
            for (int vector = 0; vector < 3; vector++)
            {
                Assert.Equal(new uint[] { (uint)(vector * 10 + 1), (uint)(vector * 10 + 2), (uint)(vector * 10 + 3), 0 }, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x1E0 + vector * 64 + instance * 16, 4));
            }
        }
        Assert.Equal(11u, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x1DC, 1)[0]);
        Assert.Equal(new byte[] { 4, 0 }, step.Frame.Memory.Arena!.Read(Node + 0x1D8, 2));
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(1, 0, 6)]
    [InlineData(2, 1, 3)]
    [InlineData(2, 0, 5)]
    public void Placement_preserves_literal_constant_radius_selector_and_exact_CRT_draw_count(byte placement, byte constant, int draws)
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x34] = placement;
            source[0x33] = constant;
            source[0x21] = 1;
        });
        frame = Channel(frame, 0xCC, 5);
        frame = Channel(frame, 0xD0, -7);
        frame = Channel(frame, 0xD4, 9);
        frame = NativeBattleCuragaEmitterTests.Dwords(frame, Emitter + 0x4C, [10u << 16, 20u << 16, 30u << 16, 123]);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleCuragaSpawnStep result = NativeBattleCuragaNodeKinematics.Spawn8818A0(frame, fixture.Task, Emitter, 1, new(1));
        Assert.Equal(expected, result.Random);
        Assert.Equal(new uint[] { 10u << 16, 20u << 16, 30u << 16, 123 }, NativeBattleCuragaEmitterTests.Dwords(result.Frame, Node + 0xDC, 4));
        //Seed1's first six original CRT results are41,18467,6334,26500,19169,15724.
        //The random sphere therefore has X radius26500%5==0; retaining the original X is correct.
        NativeBattleVector euler = placement == 1 ? new(41, 2083, 2238) : new(41, 0, 1024);
        NativeBattleVector unit = NativeBattleCuragaMatrices.RotateVector45DF80(NativeBattleCuragaMatrices.Euler8818A0(euler), new(0, -4096, 0));
        int[] radius = placement == 1 ? constant == 1 ? [5, -7, 9] : [0, 3, 1] : constant == 1 ? [5, 1, 9] : [2, -6, 4];
        uint[] position = [unchecked((uint)((10 << 16) + (radius[0] * unit.X << 4))), unchecked((uint)((20 << 16) + (placement == 1 ? radius[1] * unit.Y << 4 : radius[1] << 16))), unchecked((uint)((30 << 16) + (radius[2] * unit.Z << 4))), 123];
        Assert.Equal(position, NativeBattleCuragaEmitterTests.Dwords(result.Frame, Node + 0x12C, 4));
        Assert.Equal(123u, NativeBattleCuragaEmitterTests.Dwords(result.Frame, Node + 0x138, 1)[0]);
    }

    [Fact]
    public void Missing_placement_Euler_is_an_explicit_unknown_owner_and_does_not_invent_zero_angles()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source => source[0x21] = 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodeKinematics.Spawn8818A0(frame, fixture.Task, Emitter, 1, new(1)));
    }

    [Fact]
    public void Sprite_loop_metadata_uses_signed_byte_bounds_and_original_resource_frame_count()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x16] = 2;
            source[0x17] = 2;
            source[0x18] = 5;
            source[0x19] = 255;
            source[0x1A] = 255;
        });
        frame = Bank(frame, bytes =>
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x408), 9);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x44), NativeBattleCuragaExecutableState.Address + 0x400);
        });
        frame = Task(frame, fixture.Task, task => task.WriteDword(0x34 + 0x228, NativeBattleCuragaExecutableState.Address + 0x44));
        NativeBattleCuragaSpawnStep result = NativeBattleCuragaNodeKinematics.Spawn8818A0(frame, fixture.Task, Emitter, 1, new(321));
        Assert.Equal(321u, result.Random.Seed);
        Assert.Equal(new byte[] { 8, 0, 2, 5, 255 }, result.Frame.Memory.Arena!.Read(Node + 0x1D1, 5));
    }

    internal static (NativeBattleCuragaFrame Frame, uint Task) Fixture()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(0, 0, false)]);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x12] = 20;
            source[0x16] = 4;
            source[0x1E] = 1;
            source[0x1F] = 2;
            source[0x20] = 2;
            source[0x27] = 1;
            source[0x36] = 1;
        });
        frame = frame with { State = frame.State with { CopiedCamera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 } } };
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 0).Frame;
        frame = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(frame, fixture.Task, Emitter, _ => throw new InvalidOperationException());
        frame = Task(frame, fixture.Task, task => task.WriteDword(0x34 + 0x22C, NativeBattleCuragaExecutableState.Address + 0x84));
        frame = Bank(frame, bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x84), NativeBattleCuragaExecutableState.Address + 0x400));
        foreach (int field in new[] { 0xC8, 0xCC, 0xD0, 0xD4, 0xD8, 0xDC, 0xE0, 0xE4, 0xE8, 0xEC, 0xF0, 0xF4, 0xF8, 0xFC, 0x12C, 0x13C, 0x140, 0x144 })
        {
            frame = Channel(frame, field, field is 0xE4 or 0xE8 or 0xEC ? (short)4096 : (short)0);
        }
        return (frame, fixture.Task);
    }

    internal static NativeBattleCuragaFrame Channel(NativeBattleCuragaFrame frame, int field, short value, bool bytes = false)
    {
        return Bank(frame, data =>
        {
            int offset = 0x1000 + field * 64;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x104 + field), NativeBattleCuragaExecutableState.Address + (uint)offset);
            for (int index = 0; index < 40; index++)
            {
                if (bytes)
                {
                    data[offset + index] = unchecked((byte)value);
                }
                else
                {
                    BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + index * 2), value);
                }
            }
        });
    }

    internal static NativeBattleCuragaFrame Bank(NativeBattleCuragaFrame frame, Action<byte[]> update)
    {
        byte[] bytes = NativeBattleStorageSnapshots.Capture(frame.Memory.CuragaExecutable!.Data!).Data.ToArray();
        update(bytes);
        return frame with { Memory = frame.Memory with { CuragaExecutable = new(1, bytes.ToImmutableArray()) } };
    }

    internal static NativeBattleCuragaFrame Task(NativeBattleCuragaFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, update(hierarchy.Read(address))).Pools } };
    }

    internal static NativeBattleCuragaFrame Clone(NativeBattleCuragaFrame frame) => RoundTrip(frame);
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
