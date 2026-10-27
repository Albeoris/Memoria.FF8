using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot197Tests
{
    [Fact]
    public void Original_phase6_contact_phase22_readiness_and_ordered_child_random_calls_survive_restore()
    {
        NativeBattleActionPacketsState packets = Packets();
        List<NativeBattleShot197Operation> operations = [];
        NativeBattleShot197Inputs inputs = Inputs(packets, operations);
        NativeBattleShot197Frame frame = NativeBattleShot197.Enter(Fixture(), packets);
        for (int callback = 0; callback < 7; callback++)
        {
            frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        }
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        frame = NativeBattleShot197.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 2, Parameter81 = 0 } } }, packets);
        frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 43; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.All(operations.OfType<NativeBattleShot197Sprite>().Where(sprite => sprite.Address == 0xCFDA74), sprite => Assert.Equal((ushort)0, sprite.Flags));
        for (int phase = 1; phase < 6; phase++)
        {
            frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        }
        Assert.Empty(operations.OfType<NativeBattleShot197Contact>());
        frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        Assert.Single(operations.OfType<NativeBattleShot197Contact>());
        expected = new(1);
        for (int draw = 0; draw < 199; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        Assert.Contains(operations.OfType<NativeBattleShot197Sprite>(), sprite => sprite.Address == 0xCFDC20 && sprite.ParentPosition == new NativeBattleVector(100, 20, 300) && sprite.Flags == 0x208);
        for (int phase = 8; phase < 22; phase++)
        {
            frame = NativeBattleShot197.Advance(frame, inputs, Resources());
            Assert.Equal((byte)0, frame.Effect.Parameters!.Parameter81);
        }
        frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        Assert.Equal((byte)1, frame.Effect.Parameters!.Parameter81);
        NativeBattleShot197Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShot197Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleShot197.Advance(frame, inputs, Resources())), NativeBattleTestSnapshots.Json(NativeBattleShot197.Advance(restored, Inputs(packets, []), Resources())));
        frame = NativeBattleShot197.Enter(frame with { Effect = frame.Effect with { Parameters = frame.Effect.Parameters with { Parameter80 = 1, Parameter81 = 0 } } }, packets);
        for (int callback = 0; callback < 100 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            frame = NativeBattleShot197.Advance(frame, inputs, Resources());
            foreach (NativeBattleShot197ReloadCommon reload in operations.OfType<NativeBattleShot197ReloadCommon>())
            {
                frame = frame with { Effect = NativeBattleShot197.WriteCompletionByte(frame.Effect, reload.CompletionAddress, 1) };
            }
        }
        Assert.Equal((uint?)0, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Paused_original_beam_still_draws_both_meshes_and_consumes_its_endpoint_flicker_random()
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot197Frame frame = NativeBattleShot197.Enter(Fixture(), packets);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x22636F8, 0x2263708, 36, 100);
        (children, int beam) = children.Append(0x5AFF30);
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(beam, children.Slots[beam].WriteWord(12, 0).WriteWord(14, 0).WriteWord(28, 1152).WriteWord(32, 1000).WriteWord(34, 1000)) });
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        frame = frame with { Memory = frame.Memory with { Shot197 = frame.Memory.Shot197! with { Anchors = frame.Memory.Shot197!.Anchors.SetItem(0, new(new(10, 20, 30), new(0, 0, -4096), identity)) } }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(0x2261A20, 0x2260C00, 36, 100), children] } };
        List<NativeBattleShot197Operation> operations = [];
        frame = NativeBattleShot197.Advance(frame, Inputs(packets, operations) with { GraphicsFlags = 0x201 }, Resources());
        Assert.Equal(2, operations.OfType<NativeBattleShot197Mesh>().Count());
        NativeBattleShot197Mesh longBeam = operations.OfType<NativeBattleShot197Mesh>().First();
        NativeBattleShot197Mesh endpoint = operations.OfType<NativeBattleShot197Mesh>().Last();
        Assert.Equal(0xCFDD18u, longBeam.Address);
        Assert.Equal((short)682, longBeam.World.Rotation[8]);
        Assert.Equal(0xCFEDE8u, endpoint.Address);
        Assert.Equal(new NativeBattleVector(10, 20, -970), endpoint.CameraAnchor);
        Assert.Equal((short)2089, endpoint.World.Rotation[0]);
        Assert.Equal(-261, endpoint.Depth);
        Assert.True(endpoint.BillboardOnly);
        Assert.Equal((ushort)0, frame.Effect.Pools[2].Slots[beam].ReadWord(12));
        Assert.Equal((ushort)1000, frame.Effect.Pools[2].Slots[beam].ReadWord(32));
        Assert.Equal(new NativeCrtRandomState(1).Draw(out _), frame.Random);
    }

    [Theory]
    [InlineData(0, 0, 3600)]
    [InlineData(1, 3632, 7232)]
    public void Paired_bone_particles_keep_the_unchecked_second_physical_slot_store(int serial, int bank, int overflow)
    {
        NativeBattleActionPacketsState packets = Packets();
        NativeBattleShot197Frame frame = NativeBattleShot197.Enter(Fixture(), packets);
        byte[] bytes = frame.Memory.Shot197!.ParticleStorage.ToArray();
        for (int slot = 0; slot < 149; slot++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bank + slot * 24), 999);
        }
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x22636F8, 0x2263708, 36, 100);
        (particles, int task) = particles.Append(0x5AFBF0);
        particles = NativeBattleTaskFixture.EditPool(particles, data => data with { Slots = particles.Slots.SetItem(task, particles.Slots[task].WriteWord(12, 0).WriteWord(14, (ushort)(6 + serial))) });
        frame = frame with { Memory = frame.Memory with { Shot197 = frame.Memory.Shot197! with { ParticleStorage = bytes.ToImmutableArray() } }, Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(0x2261A20, 0x2260C00, 36, 100), particles] } };
        List<int> bones = [];
        NativeBattleShot197Inputs inputs = Inputs(packets, []) with { ReadBone = (_, bone, _) => { bones.Add(bone); return new(100, 200, 300); } };
        frame = NativeBattleShot197.Advance(frame, inputs, Resources());
        Assert.Equal(new[] { 29, 30 }, bones);
        Assert.Equal(7 + serial, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot197!.ParticleStorage.AsSpan().Slice(bank + 149 * 24)));
        Assert.Equal((short)893, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot197!.ParticleStorage.AsSpan().Slice(bank + 149 * 24 + 6)));
        Assert.Equal(7 + serial, BinaryPrimitives.ReadInt32LittleEndian(frame.Memory.Shot197!.ParticleStorage.AsSpan().Slice(overflow)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot197!.ParticleStorage.AsSpan().Slice(overflow + 4)));
        Assert.Equal((short)1409, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Shot197!.ParticleStorage.AsSpan().Slice(overflow + 6)));
    }


    private static NativeBattleShot197Frame Fixture()
    {
        return new(NativeBattleEffectMemory.FromColdProcess() with { Shot197 = NativeBattleShot197State.FromColdProcess() }, new(197, 196, new(197, 0x5AED30, 0x5AED10), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null), new(1));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 14, 2, -1, 0, 197, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleShot197Inputs Inputs(NativeBattleActionPacketsState packets, List<NativeBattleShot197Operation> operations)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, packets, _ => new(new(10, 20, 30), new(0, 0, -4096), identity), _ => 2, (_, _, _) => new(100, 200, 300), _ => 4096, _ => new(100, 200, 300), _ => new(new string('A', 64), [identity]), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleShot197Resources Resources()
    {
        byte[] bytes = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(44 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), ushort.MaxValue);
        return new(new("test", []), new uint[] { 0xCFD714, 0xCFDA74, 0xCFDC20 }.Select(address => new NativeBattleSearchSpriteResource(address, bytes.ToImmutableArray())).ToImmutableArray(), [], [new(0, 0, -4096), new(0, 0, 4096), new(0, -4096, 0)], [], 123);
    }
}
