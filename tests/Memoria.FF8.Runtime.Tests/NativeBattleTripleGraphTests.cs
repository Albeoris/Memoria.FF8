using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTripleGraphTests
{
    [Fact]
    public void Same_pass_opposite_pairs_use_larger_banks_two_CRT_draws_each_and_prior_sprite_list_order()
    {
        var fixture = Fixture();
        List<NativeBattleTripleOperation> operations = [];
        NativeBattleTripleInputs inputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            }
        };
        NativeBattleTripleStep first = NativeBattleTripleController.Advance89DFF0(fixture.Frame, fixture.Resources, inputs, NativeCrtRandomState.FromColdProcess());
        Assert.Equal(0x8000, first.Frame.State.DrawCursor);
        Assert.Null(first.Frame.Effect.Pools[1].Head);
        NativeBattleTripleStep second = NativeBattleTripleController.Advance89DFF0(first.Frame, fixture.Resources, inputs, first.Random);
        Assert.Equal(0, second.Frame.State.DrawCursor);
        NativeBattleEffectTaskPool geometry = second.Frame.Effect.Pools[4];
        Assert.Equal(0x89EBD0u, geometry.Slots[0].Callback);
        Assert.Equal(0x89EBD0u, geometry.Slots[1].Callback);
        Assert.Equal((ushort)0, geometry.Slots[0].ReadWord(0x68));
        Assert.Equal((ushort)2048, geometry.Slots[1].ReadWord(0x68));
        Assert.Equal((ushort)1, geometry.Slots[0].ReadWord(0x24));
        Assert.Equal((ushort)1, geometry.Slots[1].ReadWord(0x24));
        NativeBattleEffectTaskPool sprites = second.Frame.Effect.Pools[3];
        Assert.Equal(new uint[] { 0x89F180, 0x89F180, 0x89F410, 0x89F410 }, sprites.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal(new ushort[] { 1, 1, 0, 0 }, sprites.Slots.Take(4).Select(task => task.ReadWord(0x24)));
        NativeCrtRandomState expected = first.Random.Draw(out _).Draw(out _).Draw(out _).Draw(out _);
        Assert.Equal(expected, second.Random);
        Assert.Collection(operations, operation => Assert.IsType<NativeBattleTripleSprite>(operation), operation => Assert.IsType<NativeBattleTripleSprite>(operation), operation => Assert.IsType<NativeBattleTriplePackets>(operation), operation => Assert.IsType<NativeBattleTriplePackets>(operation));
        Assert.Equal(300027u, Assert.Single(second.Sounds).Sound);
    }

    [Fact]
    public void Complete_private_graph_waits_for_native_twenty_four_gate_and_preserves_frame_valued_contact()
    {
        var fixture = Fixture();
        List<NativeBattleTripleOperation> operations = [];
        List<int> contacts = [];
        List<NativeBattleModuleSound> sounds = [];
        HashSet<uint> callbacks = [];
        NativeBattleTripleInputs inputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            },
            ApplyContact = (frame, _) =>
            {
                Assert.Equal((ushort)25, frame.Effect.Pools[1].Slots[0].ReadWord(0x24));
                return frame with { Memory = frame.Memory with { Parameter80 = 99 } };
            }
        };
        NativeBattleTripleFrame current = fixture.Frame;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        int ticks = 0;
        while (current.Effect.ReturnedDescriptor != 0 && ticks < 256)
        {
            NativeBattleTripleStep step = NativeBattleTripleController.Advance89DFF0(current, fixture.Resources, inputs, random);
            current = step.Frame;
            random = step.Random;
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            callbacks.UnionWith(current.Effect.Pools.SelectMany(pool => pool.Slots).Where(task => task.Callback != 0).Select(task => task.Callback));
            ticks++;
        }
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.InRange(ticks, 27, 255);
        Assert.Equal((byte?)99, current.Memory.Parameter80);
        Assert.Single(contacts);
        Assert.Single(sounds);
        Assert.Single(operations, operation => operation is NativeBattleTripleFlash);
        Assert.Equal(new uint[] { 0x89DFF0, 0x89E110, 0x89E250, 0x89E670, 0x89E7D0, 0x89EA70, 0x89EBD0, 0x89EDB0, 0x89F180, 0x89F410, 0x89F5E0, 0x89F950 }, callbacks.Order());
        Assert.All(current.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void Independently_restored_mid_graph_keeps_tasks_random_and_immediate_operations_equal()
    {
        var fixture = Fixture();
        NativeBattleTripleFrame current = fixture.Frame;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        for (int tick = 0; tick < 13; tick++)
        {
            NativeBattleTripleStep step = NativeBattleTripleController.Advance89DFF0(current, fixture.Resources, fixture.Inputs, random);
            current = step.Frame;
            random = step.Random;
        }
        string saved = NativeBattleTestSnapshots.Json(current);
        NativeBattleTripleFrame first = NativeBattleTestSnapshots.RestoreJson<NativeBattleTripleFrame>(saved)!;
        NativeBattleTripleFrame second = NativeBattleTestSnapshots.RestoreJson<NativeBattleTripleFrame>(saved)!;
        List<string> firstOperations = [];
        List<string> secondOperations = [];
        NativeBattleTripleInputs firstInputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                firstOperations.Add(NativeBattleTestSnapshots.Json(operation, operation.GetType()));
                return frame;
            }
        };
        NativeBattleTripleInputs secondInputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                secondOperations.Add(NativeBattleTestSnapshots.Json(operation, operation.GetType()));
                return frame;
            }
        };
        NativeBattleTripleStep a = NativeBattleTripleController.Advance89DFF0(first, fixture.Resources, firstInputs, random);
        NativeBattleTripleStep b = NativeBattleTripleController.Advance89DFF0(second, fixture.Resources, secondInputs, random);
        Assert.Equal(NativeBattleTestSnapshots.Json(a), NativeBattleTestSnapshots.Json(b));
        Assert.Equal(firstOperations, secondOperations);
    }

    private static (NativeBattleTripleFrame Frame, NativeBattleTripleResources Resources, NativeBattleTripleInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 34, 21, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        byte[] meshBytes = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(meshBytes, 8);
        ImmutableArray<byte> mesh = meshBytes.ToImmutableArray();
        NativeBattleTripleResources resources = NativeBattleTripleControllerTests.Resources() with { Meshes = new uint[] { 0x1626CF0, 0x1626F00, 0x1627160, 0x1627380, 0x1627970, 0x1628218 }.Select(address => new NativeBattleStaticMeshResource(0, address, mesh)).ToImmutableArray(), Sprites = [Sprite(0x16269DC, 13), Sprite(0x1626B3C, 8), Sprite(0x1626C18, 10), Sprite(0x1626CC8, 1)], MorphFirst = mesh.AsSpan().Slice(0, 8).ToArray().ToImmutableArray(), MorphSecond = mesh.AsSpan().Slice(0, 8).ToArray().ToImmutableArray() };
        NativeBattleOriginalEffectState effect = new(21, 20, new(21, 0x89DEF0, 0x89DED0), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(20) with { Textures = null, TripleMorph = new(mesh) };
        NativeBattleTripleFrame frame = NativeBattleTripleController.Initialize89DEF0(memory, effect, packets, resources);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleTripleInputs inputs = new(packets, new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 10000 }, 216, 160, 108), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => pose, (current, _) => current);
        return (frame, resources, inputs);
    }

    private static NativeBattleModuleResource Sprite(uint address, int count)
    {
        int offset = 12 + 2 * count;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int phase = 0; phase <= count; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * phase), (ushort)offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
