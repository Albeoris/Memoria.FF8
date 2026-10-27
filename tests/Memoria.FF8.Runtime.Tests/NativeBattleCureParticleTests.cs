using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureParticleTests
{
    [Fact]
    public void First_ring_callback_initializes_and_hides_the_actor_before_actor_and_sprite_operations_without_drawing_random()
    {
        var fixture = Fixture(0x8D6D80);
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(fixture.Frame, fixture.Address, fixture.Inputs, operations);
        Assert.Equal(fixture.Inputs.CrtRandom, step.CrtRandomAfter);
        Assert.Collection(operations,
            operation => Assert.Equal(new NativeBattleCureActorInitializeOperation(3), operation),
            operation => Assert.Equal(new NativeBattleCureActorVisibilityOperation(3, true), operation),
            operation => Assert.Equal(new NativeBattleCureActorDrawOperation(3, new(100, 191, 300), 191), operation),
            operation => Assert.Equal(0x1641F78u, Assert.IsType<NativeBattleCureSpriteOperation>(operation).Draw.Resource!.Address),
            operation => Assert.Equal(0x1642504u, Assert.IsType<NativeBattleCureSpriteOperation>(operation).Draw.Resource!.Address));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((short)-3, unchecked((short)task.ReadWord(0x5A)));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        NativeBattleModuleDraw ground = Assert.IsType<NativeBattleCureSpriteOperation>(operations[4]).Draw;
        Assert.Equal(new NativeBattleModuleAnchor(100, 0, 300, 0), ground.Anchor);
        Assert.Equal(0x3C3C3Cu, ground.SpriteColor);
        Assert.Equal((ushort)4, ground.SpriteFlags);
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(1024, 0, 0)).ToArray(), ground.SpriteWorldRotation.ToArray());
    }

    [Fact]
    public void Ring_counter_four_appends_four_trails_one_spark_and_two_wedges_with_the_original_random_order()
    {
        var fixture = Fixture(0x8D6D80);
        NativeBattleCureParticleStep initialized = Advance(fixture.Frame, fixture.Address, fixture.Inputs, []);
        NativeBattleEffectHierarchy hierarchy = new(initialized.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, 4));
        NativeBattleCureFrame frame = initialized.Frame with { Effect = initialized.Frame.Effect with { Pools = hierarchy.Pools } };
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(frame, fixture.Address, fixture.Inputs, operations);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom.Draw(out _).Draw(out ushort yRandom).Draw(out ushort firstWedge).Draw(out ushort secondWedge);
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal(4, step.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x8D80B0));
        NativeBattleModulePoolTask spark = Assert.Single(step.Frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x8D7F60));
        Assert.Equal((short)(143 + (-57 * (yRandom & 31) / -48)), unchecked((short)spark.ReadWord(0x1E)));
        NativeBattleModulePoolTask[] wedges = step.Frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(2, wedges.Length);
        Assert.Equal((short)((firstWedge & 1) != 0 ? 64 : -64), unchecked((short)wedges[0].ReadWord(0x58)));
        Assert.Equal((short)((secondWedge & 1) != 0 ? 64 : -64), unchecked((short)wedges[1].ReadWord(0x58)));
        Assert.All(wedges, task =>
        {
            Assert.Equal(0x8DCC20u, task.Callback);
            Assert.Equal((ushort)8, task.ReadWord(0x56));
            Assert.Equal((ushort)256, task.ReadWord(0x3C));
        });
        Assert.Equal((byte)7, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address).ReadByte(0x28));
        Assert.Equal(new NativeBattleCureActorDrawOperation(3, new(100, 143, 300), 143), Assert.IsType<NativeBattleCureActorDrawOperation>(operations[0]));
    }

    [Fact]
    public void Final_ring_frame_restores_visibility_and_releases_its_parent_without_emitting_an_extra_sprite_or_actor()
    {
        var fixture = Fixture(0x8D6D80);
        NativeBattleCureParticleStep initialized = Advance(fixture.Frame, fixture.Address, fixture.Inputs, []);
        NativeBattleEffectHierarchy hierarchy = new(initialized.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, 25).WriteByte(0x29, 2).WriteWord(0x50, 4).WriteWord(0x52, 4));
        uint root = hierarchy.Read(fixture.Address).ReadDword(0x10);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x63, 1));
        NativeBattleCureFrame frame = initialized.Frame with { Effect = initialized.Frame.Effect with { Pools = hierarchy.Pools } };
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(frame, fixture.Address, fixture.Inputs, operations);
        Assert.Equal(new NativeBattleCureActorVisibilityOperation(3, false), Assert.Single(operations));
        Assert.Equal(2, step.Result);
        hierarchy = new(step.Frame.Effect.Pools);
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(0x63));
        Assert.Equal((byte)0, hierarchy.Read(hierarchy.Read(fixture.Address).ReadDword(0x18)).ReadByte(0x28));
        Assert.Equal((byte)5, hierarchy.Read(fixture.Address).ReadByte(0x26));
    }

    [Theory]
    [InlineData(0x8D7F60u, 1, 18, 0x164205Cu)]
    [InlineData(0x8D80B0u, 3, 10, 0x1642238u)]
    public void Moving_sprite_initializers_consume_the_exact_random_draws_and_release_after_the_native_inclusive_frame_limit(uint callback, int draws, int callbacks, uint resource)
    {
        var fixture = Fixture(callback);
        NativeBattleCureFrame frame = fixture.Frame;
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(frame, fixture.Address, fixture.Inputs, operations);
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModuleDraw draw = Assert.IsType<NativeBattleCureSpriteOperation>(Assert.Single(operations)).Draw;
        Assert.Equal(resource, draw.Resource!.Address);
        Assert.Equal((ushort)0, draw.Phase);
        string json = NativeBattleTestSnapshots.Json(step.Frame);
        NativeBattleCureFrame independent = NativeBattleTestSnapshots.RestoreJson<NativeBattleCureFrame>(json)!;
        frame = step.Frame;
        int count = 1;
        while (step.Result == 0 && count < 30)
        {
            operations.Clear();
            NativeBattleCureParticleInputs inputs = fixture.Inputs with { CrtRandom = expected };
            step = Advance(frame, fixture.Address, inputs, operations);
            NativeBattleCureParticleStep restored = Advance(independent, fixture.Address, inputs, []);
            Assert.Equal(NativeBattleTestSnapshots.Json(step.Frame), NativeBattleTestSnapshots.Json(restored.Frame));
            Assert.Equal(expected, step.CrtRandomAfter);
            frame = step.Frame;
            independent = restored.Frame;
            count++;
        }
        Assert.Equal(callbacks, count);
        Assert.Equal(2, step.Result);
        Assert.Empty(operations);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        Assert.Equal((byte)0, hierarchy.Read(hierarchy.Read(fixture.Address).ReadDword(0x18)).ReadByte(0x28));
    }

    private static NativeBattleCureParticleStep Advance(NativeBattleCureFrame frame, uint address, NativeBattleCureParticleInputs inputs, List<NativeBattleCureOperation> operations)
    {
        return NativeBattleCureParticles.AdvancePrivate(frame, address, Resources(), inputs with { Execute = (current, operation) =>
        {
            operations.Add(operation);
            return current;
        } });
    }

    private static (NativeBattleCureFrame Frame, uint Address, NativeBattleCureParticleInputs Inputs) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 21, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(1, 0, new(1, 0x8D6A00, 0x8D69E0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x277AEC0, 0)] };
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(NativeBattleEffectMemory.FromColdProcess().BeginLoad(0), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, 0x8D6CC0, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = first.Hierarchy;
        hierarchy = hierarchy.Write(first.Address, hierarchy.Read(first.Address).WriteWord(0x30, 100).WriteWord(0x34, 300).WriteWord(0x4A, unchecked((ushort)-50)));
        NativeBattleEffectHierarchyAppend particle = hierarchy.Append8DC540(2, callback, 108, first.Address, (_, _, _) => new(0, 3));
        hierarchy = particle.Hierarchy;
        hierarchy = hierarchy.Write(particle.Address, hierarchy.Read(particle.Address).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096);
        NativeBattleCureControllerInputs controller = new(packets, camera, index => new(index, 0) { Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096) }, _ => throw new InvalidOperationException("Private particles read already-published first-level bounds."));
        return (frame, particle.Address, new(controller, NativeCrtRandomState.FromColdProcess(), (current, _) => current));
    }

    private static NativeBattleCureResources Resources() => new(new("synthetic-animation-unused", []), [Sprite(0x1641F78, 2), Sprite(0x1641FCC, 5), Sprite(0x164205C, 17), Sprite(0x1642238, 27), Sprite(0x1642504, 1)], 0x493F2);

    private static NativeBattleModuleResource Sprite(uint address, int count)
    {
        int offset = 12 + 2 * count;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int index = 0; index <= count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), (ushort)offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
