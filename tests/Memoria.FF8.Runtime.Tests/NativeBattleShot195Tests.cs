using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot195Tests
{
    [Fact]
    public void Original_lifecycle_keeps_first_contact_firing_separate_from_full_header_ending()
    {
        NativeBattleActionPacketsState packets = Packets();
        List<NativeBattleShot195Operation> operations = [];
        NativeBattleShot195Inputs inputs = Inputs(packets, operations);
        NativeBattleShot195Frame frame = NativeBattleShot195.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        frame = NativeBattleShot195.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        for (int phase = 0; phase < 18; phase++)
        {
            frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        }
        Assert.Empty(operations.OfType<NativeBattleShot195Contact>());
        Assert.Single(operations.OfType<NativeBattleShot195Sound>());
        frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        Assert.False(Assert.Single(operations.OfType<NativeBattleShot195Contact>()).All);
        for (int phase = 19; phase < 22; phase++)
        {
            frame = NativeBattleShot195.Advance(frame, inputs, Resources());
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        NativeBattleShot195Frame restored = JsonSerializer.Deserialize<NativeBattleShot195Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleShot195.Advance(frame, inputs, Resources())), JsonSerializer.Serialize(NativeBattleShot195.Advance(restored, Inputs(packets, []), Resources())));
        frame = NativeBattleShot195.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
        Assert.True(operations.OfType<NativeBattleShot195Contact>().Last().All);
    }

    [Theory]
    [InlineData(0, 0, 3600)]
    [InlineData(1, 10032, 13632)]
    public void Paired_bone_particles_keep_the_unchecked_second_physical_slot_store(int serial, int bank, int overflow)
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot195Frame frame = NativeBattleShot195.Enter(Fixture(), packets);
        byte[] bytes = frame.Memory.Shot195!.ParticleStorage.ToArray();
        for (int slot = 0; slot < 149; slot++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bank + slot * 24), 999);
        }
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x226EDB8, 0x226EDC8, 36, 100);
        (particles, int task) = particles.Append(0x5B3A80);
        particles = particles with { Slots = particles.Slots.SetItem(task, particles.Slots[task].WriteWord(12, 0).WriteWord(14, (ushort)serial).WriteWord(22, 7)) };
        frame = frame with { Memory = frame.Memory with { Shot195 = frame.Memory.Shot195! with { ParticleStorage = bytes.ToImmutableArray() } }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(0x226B7E0, 0x226A9C0, 36, 100), particles] } };
        List<int> bones = [];
        NativeBattleShot195Inputs inputs = Inputs(packets, []) with { ReadBone = (_, bone, _) => { bones.Add(bone); return new(100, 200, 300); } };
        frame = NativeBattleShot195.Advance(frame, inputs, Resources());
        Assert.Equal(new[] { 29, 30 }, bones);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot195!.ParticleStorage.AsSpan().Slice(bank + 149 * 24)));
        Assert.Equal((short)893, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot195!.ParticleStorage.AsSpan().Slice(bank + 149 * 24 + 6)));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot195!.ParticleStorage.AsSpan().Slice(overflow)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot195!.ParticleStorage.AsSpan().Slice(overflow + 4)));
        Assert.Equal((short)1409, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot195!.ParticleStorage.AsSpan().Slice(overflow + 6)));
    }

    private static NativeBattleShot195Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot195 = NativeBattleShot195State.FromColdProcess() }, new(195, 194, new(195, 0x5B2A40, 0x5B2A20), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 2, -1, 0, 195, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot195Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot195Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), new(0, 0, -4096), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot195Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xD097CC, 0xD099F0, 0xD09AB4, 0xD09C60, 0xD09E0C }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096), new(0, -4096, 0)], [], 123);
    }
}
