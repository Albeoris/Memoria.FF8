using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa88ControllerTests
{
    [Fact]
    public void Root_waits_then_clears_exactly_twenty_emitters_and_seventy_geometry_slots()
    {
        var fixture = Fixture();
        NativeBattleRinoa88Frame frame = fixture.Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(41, 2).WriteByte(40, 1));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        int first = frame.State.AuxiliaryCursor;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(first - 1, Enumerable.Repeat((byte)0xA5, 49202).ToArray()) } };
        frame = NativeBattleRinoa88Controller.Advance(frame, fixture.Inputs);
        Assert.Equal(first, frame.State.AuxiliaryCursor);
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(first, 1)[0]);
        hierarchy = new(frame.Effect.Pools);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(root, hierarchy.Read(root).WriteByte(40, 0)).Pools } };
        NativeBattleRinoa88Frame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleRinoa88Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleRinoa88Controller.Advance(frame, fixture.Inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleRinoa88Controller.Advance(clone, fixture.Inputs)));
        Assert.Equal(first, frame.State.EmitterOffset);
        Assert.Equal(first + 2160, frame.State.GeometryOffset);
        Assert.Equal(first + 49200, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(first, 49200), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(first - 1, 1)[0]);
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(first + 49200, 1)[0]);
    }

    [Fact]
    public void Entry_preserves_unassigned_projected_and_morph_tails()
    {
        var fixture = Fixture();
        NativeBattleRinoa88State retained = fixture.Frame.State with { BodyProjected = Enumerable.Repeat((byte)0xA5, 2416).ToImmutableArray(), MorphScratch = Enumerable.Repeat((byte)0x5A, 1440).ToImmutableArray() };
        NativeBattleRinoa88Frame frame = NativeBattleRinoa88Controller.Initialize(fixture.Frame.Memory with { Rinoa88 = retained }, fixture.Frame.Effect, fixture.Inputs.Packets);
        Assert.Equal(retained.BodyProjected, frame.State.BodyProjected);
        Assert.Equal(retained.MorphScratch, frame.State.MorphScratch);
        frame.State.Validate();
    }

    [Fact]
    public void Missing_process_owner_is_not_silently_recreated()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleRinoa88Controller.Initialize(fixture.Frame.Memory with { Rinoa88 = null }, fixture.Frame.Effect, fixture.Inputs.Packets));
    }

    internal static (NativeBattleRinoa88Frame Frame, NativeBattleRinoa88Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 19, 0, 27, 88, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(88, 87, new(88, 0x7684F0, 0x7684D0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x25A4D98, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(87) with { Rinoa88 = NativeBattleRinoa88State.FromColdProcess() };
        NativeBattleRinoa88Frame frame = NativeBattleRinoa88Controller.Initialize(memory, selected, packets);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleRinoa88Inputs inputs = new(packets, camera, index => new(index, 0), _ => throw new InvalidOperationException("Inactive model does not read a pose."), (_, _) => throw new NotSupportedException("The selected scene leaf has no test renderer."), (_, _) => throw new NotSupportedException("Texture release has no test completion producer."));
        return (frame, inputs);
    }
}
