using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionGraphicsTests
{
    [Fact]
    public void Normalization_accumulates_hosted_targets_from_all_header_groups()
    {
        NativeBattleGraphicsRoster roster = CreateRoster();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(roster);
        (packets, _) = packets.Prepare(new(7, 0, 0, -1, 0, 0, null));
        packets = packets.AppendContacts([Contact(24, 0x30, 0x48), Contact(24, 0x30, 0x48)]);
        (packets, _) = packets.Publish(2);
        (packets, _) = packets.Prepare(new(7, 0, 0, -1, 0, 0, null));
        (packets, _) = packets.AppendContact(Contact(16));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 2);

        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, 0);
        Assert.Equal((byte)7, action.ActorIndex);
        Assert.Equal((byte)7, action.ActorSelector56);
        Assert.Equal((byte)24, action.FirstTarget);
        Assert.Equal(2, action.FirstHeaderTargetCount);
        Assert.Equal(new[] { 16, 24 }, action.TargetActors.EnumerateRows().ToArray());
        Assert.Equal((ushort)0, action.TargetMask);
        Assert.Equal((ushort)2, action.CoverMask);
        Assert.Equal((byte)1, action.CoverSource);
        Assert.Equal((byte)2, action.HitFlags);
        Assert.Equal(0x50BD80u, action.Callback);

        NativeBattleGraphicsState graphics = CreateGraphics(roster, action);
        graphics.Validate();
        string json = JsonSerializer.Serialize(graphics);
        NativeBattleGraphicsState restored = JsonSerializer.Deserialize<NativeBattleGraphicsState>(json)!;
        restored.Validate();
        Assert.Equal(action, restored.ActionGraphics);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(300)]
    public void First_header_semantic_count_drives_service_selection_and_survives_json(int count)
    {
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("target", Contact(24)), count), CreateRoster());
        (NativeBattleActionPacketsState packets, _) = bridge.PrepareHosted(0, 0, 0, 0, source: 7);
        Assert.Equal(unchecked((byte)count), packets.Headers[0].Payload[16]);
        Assert.Equal(0x50BD80u, NativeBattleActionGraphics.SelectService(packets.Headers[0]).Callback);

        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, 0);
        Assert.Equal(count, NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(action));
        Assert.Equal((byte)24, action.FirstTarget);
        Assert.True(action.TargetActors.IsSingle(24));
        Assert.Equal((ushort)0, action.TargetMask);
        Assert.Equal(0x50BD80u, action.Callback);
        Assert.Equal(action, JsonSerializer.Deserialize<NativeBattleActionGraphicsState>(JsonSerializer.Serialize(action)));
    }

    [Fact]
    public void Zero_contact_header_retains_previous_target_and_cover_source()
    {
        NativeBattleGraphicsRoster roster = CreateRoster();
        NativeBattleEffectContactBridge<string> bridge = new([("target", Contact(24, 0x30, 0x48))], roster);
        (NativeBattleActionPacketsState packets, _) = bridge.PrepareHosted(0, 0, 0, 0, source: 7);
        NativeBattleActionGraphicsState previous = NativeBattleActionGraphics.Normalize(packets, 0);
        (packets, _) = packets.Prepare(new(7, 0, 0, -1, 0, 0, null));
        (packets, _) = packets.Publish(0);

        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, 1, previous);
        Assert.Equal(0, action.FirstHeaderTargetCount);
        Assert.Equal((byte)24, action.FirstTarget);
        Assert.Equal((byte)1, action.CoverSource);
        Assert.Equal((ushort)0, action.CoverMask);
        Assert.Equal((byte)0, action.HitFlags);
        Assert.NotNull(action.PresentationTargets);
        Assert.True(action.TargetActors.IsEmpty);
        Assert.Equal(0x50BD00u, action.Callback);
        CreateGraphics(roster, action).Validate();
    }

    [Fact]
    public void Hosted_normalization_requires_consistent_masks_and_bound_references()
    {
        NativeBattleGraphicsRoster roster = CreateRoster();
        NativeBattleEffectContactBridge<string> bridge = new([("target", Contact(24))], roster);
        (NativeBattleActionPacketsState packets, _) = bridge.PrepareHosted(0, 0, 0, 0, source: 7);
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, 0);
        NativeBattleGraphicsState graphics = CreateGraphics(roster, action);
        graphics.Validate();
        Assert.Throws<InvalidDataException>(() => (action with { TargetMask = 1 }).TargetActors);
        Assert.Throws<InvalidDataException>((graphics with { PresentationRoster = null }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { TargetMask = 1 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { ActorIndex = 2 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { PresentationTargets = action.TargetActors.Add(23) } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { FirstTarget = 2 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { CoverSource = 25 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { FirstHeaderTargetCount = -1 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionGraphics = action with { FirstHeaderTargetCount = NativeBattleContactAddress.MaximumRowCount + 1 } }).Validate);
        NativeBattleGraphicsRoster shortRoster = new(roster.Rows[..8]);
        Assert.Throws<InvalidDataException>(CreateGraphics(shortRoster, action).Validate);
    }

    private static NativeBattleGraphicsRoster CreateRoster()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        rows[1] = new("cover", true);
        rows[7] = new("source", true);
        rows[16] = new("other-target", false);
        rows[24] = new("target", false);
        return new(rows.ToImmutableArray());
    }

    private static NativeBattleGraphicsState CreateGraphics(NativeBattleGraphicsRoster roster, NativeBattleActionGraphicsState action)
    {
        return new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster, ActionGraphics = action };
    }

    private static NativeBattleContactPacket Contact(byte row, byte flags = 0, byte hitFlags = 0) => new(new(row, 0, flags, hitFlags, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
}
