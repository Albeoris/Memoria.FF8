using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Controller;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyMicroMissiles284ControllerTests
{
    [Theory]
    [InlineData(0, -500)]
    [InlineData(1024, -500)]
    [InlineData(-1025, -500)]
    [InlineData(65537, -500)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Two_coordinate_helper_preserves_Y_W_and_full_integer_arithmetic(int yaw, int radius)
    {
        var retained = PointView(new(1, -222, 3), -333);
        var actual = SourcePoint67BB70(yaw, radius, retained);
        Assert.Equal(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(yaw) * radius) >> 12)), BinaryPrimitives.ReadInt16LittleEndian(actual.Read(0, 2)));
        Assert.Equal(unchecked((short)(unchecked(-unchecked(NativeBattleCameraMath.Cosine(yaw) * radius)) >> 12)), BinaryPrimitives.ReadInt16LittleEndian(actual.Read(4, 2)));
        Assert.Equal(retained.Read(2, 2), actual.Read(2, 2));
        Assert.Equal(retained.Read(6, 2), actual.Read(6, 2));
        var unknown = SourcePoint67BB70(yaw, radius, retained.MarkUnknown(2, 2).MarkUnknown(6, 2));
        Assert.Throws<NotSupportedException>(() => unknown.Read(2, 2));
        Assert.Throws<NotSupportedException>(() => unknown.Read(6, 2));
    }

    [Fact]
    public void Entry_defers_all_common_pool_resets_and_clears_only_root_payload()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(ModelAddress(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(0x1518, 1)[0]);
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
    }

    [Theory]
    [InlineData(0u, 0x147A8u, 1u)]
    [InlineData(1u, 0x47A8u, 0u)]
    [InlineData(0x80000000u, 0x47A8u, 0u)]
    public void Root_packet_parity_is_full_DWORD_and_has_no_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Old_one_births_all_six_with_own_cache_steps_source_and_ten_CRT_draws()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        Assert.Equal(4, actual.Effect.Pools.Length);
        Assert.Equal((byte)1, Read(actual, RootBank).ReadByte(15));
        var random = frame.Random;
        for (int index = 0; index < 10; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
        for (int index = 0; index < 6; index++)
        {
            var task = actual.Effect.Pools[3].Slots[index];
            ushort step = checked((ushort)(4096 / (20 + index * 4)));
            Assert.Equal(0x67BBB0u, task.Callback);
            Assert.Equal(step, task.ReadWord(50));
            Assert.Equal(unchecked((ushort)(step * 2)), task.ReadWord(42));
            Assert.Equal(unchecked((ushort)100), task.ReadWord(36));
            Assert.Equal(unchecked((ushort)(777 - 1000)), task.ReadWord(38));
            Assert.Equal(unchecked((ushort)1500), task.ReadWord(40));
            Assert.Equal(index % 3 == 1 ? (ushort)1 : (ushort)0, task.ReadWord(34));
            Assert.Equal(MissileAddress, task.ReadDword(52));
            Assert.Equal(1u, task.ReadDword(56));
        }
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0x1518, 1));
    }

    [Fact]
    public void Endpoint_retries_current_count_and_parent_without_reading_rejected_matrix()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        int rejected = 0;
        int countReads = 0;
        int selectedQueries = 0;
        var inputs = Inputs([]) with
        {
            ReadBoneCount = (_, _) =>
            {
                countReads++;
                return 2;
            },
            ReadBoneParent = (_, _, _) =>
            {
                selectedQueries++;
                return selectedQueries == 1 ? (short)-1 : (short)0;
            },
            ReadCurrentBoneMatrix = (_, _, _) =>
            {
                if (selectedQueries == 1)
                {
                    rejected++;
                }
                return Bone();
            }
        };
        var actual = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal(0, rejected);
        Assert.Equal(7, selectedQueries);
        Assert.Equal(7, countReads);
        var random = frame.Random;
        for (int index = 0; index < 11; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
    }

    [Fact]
    public void Contact_uses_current_first_physical_slot_once_before_particle_scan_and_waits_for_drain()
    {
        var frame = EmptyActive(1);
        (frame, uint particle) = Append(frame, Pools[1].Descriptor, 0x67C240);
        frame = ClearDwords701200(frame, particle + 12, 6);
        List<NativeBattleEnemyMicroMissiles284Operation> operations = [];
        var packets = Packets();
        var header = packets.Headers[1];
        packets = packets with { Headers = packets.Headers.SetItem(1, header with { Payload = header.Payload.SetItem(16, 0) }) };
        var inputs = Inputs(operations) with { ReadPackets = _ => packets };
        var actual = Advance(frame, Resources(), inputs);
        Assert.Equal(1, Assert.Single(operations.OfType<NativeBattleEnemyMicroMissiles284Contact506690>()).ContactIndex);
        Assert.IsType<NativeBattleEnemyMicroMissiles284Contact506690>(operations[0]);
        Assert.IsType<NativeBattleEnemyMicroMissiles284DrawSprite180>(operations[1]);
        Assert.Equal((byte)2, Read(actual, RootBank).ReadByte(15));
        Assert.NotEqual(0u, actual.Effect.ReturnedDescriptor);
        operations.Clear();
        actual = Advance(actual, Resources(), inputs);
        Assert.Empty(operations.OfType<NativeBattleEnemyMicroMissiles284Contact506690>());
        Assert.NotEqual(0u, actual.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Sound_is_old_two_after_scans_and_root_waits_until_all_banks_are_empty()
    {
        var frame = EmptyActive(1);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 2));
        List<NativeBattleEnemyMicroMissiles284Operation> operations = [];
        var actual = Advance(frame, Resources(), Inputs(operations));
        Assert.IsType<NativeBattleEnemyMicroMissiles284Contact506690>(operations[0]);
        Assert.Equal(new NativeBattleEnemyMicroMissiles284Sound501330(Resources().Sound, 0, 128), operations[1]);
        Assert.Empty(actual.Effect.Pools);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.NotNull(actual.State.RootBankStorage);
        var cold = JsonSerializer.Deserialize<NativeBattleEnemyMicroMissiles284Frame>(JsonSerializer.Serialize(actual))!;
        Assert.Single(Initialize(cold, Resources(), Inputs([])).Effect.Pools);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void Warm_old_one_with_nonzero_BYTE15_never_resets_or_rebirths(byte latch)
    {
        var frame = EmptyActive(latch);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]));
        Assert.Equal(2, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Null(actual.Frame.Effect.Pools[3].Head);
    }

    private static NativeBattleEnemyMicroMissiles284Frame EmptyActive(byte latch)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        var pools = frame.Effect.Pools.ToBuilder();
        foreach (var profile in Pools.Skip(1))
        {
            pools.Add(NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity));
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools.ToImmutable() }, Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0, 0x45A8) } };
        return Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, latch));
    }

    private static NativeBattleEnemyMicroMissiles284Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0x1518, new byte[] { 0xA5 }), ModuleSpriteBillboard21DFED0 = Identity() };
        NativeBattleOriginalEffectState effect = new(284, 283, new(284, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleEnemyMicroMissiles284State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEnemyMicroMissiles284Inputs Inputs(List<NativeBattleEnemyMicroMissiles284Operation> operations)
    {
        var packets = Packets();
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, model) => Model(model), (_, _) => 1, (_, _, _) => 0, (_, _, _) => 0, (_, _, _) => Bone(), (_, _) => Identity(), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 201, 284, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(3, 8, 13, -1, 201, 284, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleEnemyMicroMissiles284Resources Resources() => NativeBattleEnemyMicroMissiles284StateTests.Resources();
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattlePackedMatrix32 Bone() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 300, Y = 400, Z = 500 }, null);
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(100, 200, 1000), (short)(444 + actor), 777, 4096) };
    }
}
