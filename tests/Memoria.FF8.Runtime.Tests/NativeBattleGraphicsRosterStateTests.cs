using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsRosterStateTests
{
    [Fact]
    public void Old_native_state_serializes_without_a_presentation_roster()
    {
        NativeBattleGraphicsState original = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), [null, "party", null, "enemy", null, null, null], 0, 0);
        string json = JsonSerializer.Serialize(original);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattleGraphicsState.PresentationRoster), out _));

        NativeBattleGraphicsState restored = JsonSerializer.Deserialize<NativeBattleGraphicsState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(7, restored.Models.Length);
        Assert.True(restored.IsPartyActor(0));
        Assert.True(restored.IsPartyActor(2));
        Assert.False(restored.IsPartyActor(3));
        Assert.False(restored.IsPartyActor(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.IsPartyActor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.IsPartyActor(7));

        NativeBattleGraphicsState hosted = CreateHostedState(25);
        Assert.Throws<InvalidDataException>((hosted with { PresentationRoster = null }).Validate);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(25)]
    public void Hosted_state_preserves_explicit_roles_and_holes_through_serialization(int count)
    {
        NativeBattleGraphicsState state = CreateHostedState(count);
        state.Validate();

        NativeBattleGraphicsState restored = JsonSerializer.Deserialize<NativeBattleGraphicsState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(count, restored.Models.Length);
        Assert.Equal(count, restored.PresentationRoster!.ActorCount);
        Assert.False(restored.IsPartyActor(0));
        Assert.True(restored.IsPartyActor(count - 1));
        Assert.Null(restored.ActorIds[1]);
        Assert.Throws<InvalidDataException>(() => restored.IsPartyActor(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.IsPartyActor(count));
    }

    [Fact]
    public void Hosted_state_rejects_inconsistent_rows_and_duplicate_bindings()
    {
        NativeBattleGraphicsState state = CreateHostedState(8);
        Assert.Throws<InvalidDataException>((state with { Models = state.Models.RemoveAt(7) }).Validate);
        Assert.Throws<InvalidDataException>((state with { Models = state.Models.SetItem(7, state.Models[7] with { Index = 6 }) }).Validate);
        Assert.Throws<InvalidDataException>((state with { ActorIds = state.ActorIds.RemoveAt(7) }).Validate);
        Assert.Throws<InvalidDataException>((state with { ActorIds = state.ActorIds.SetItem(7, "other") }).Validate);
        Assert.Throws<InvalidDataException>((state with { ActorIds = state.ActorIds.SetItem(1, "unbound") }).Validate);
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(state.PresentationRoster!.Rows.SetItem(7, new("enemy", true))));
    }

    [Fact]
    public void Hosted_loaded_rows_still_require_their_actor_resource_and_sequence_fields()
    {
        NativeBattleGraphicsState state = CreateHostedState(8);
        NativeBattleGraphicsModel loaded = new(2) { Flags2 = 4, Flags8 = 0, ResourceFlags44 = 1, SequenceState = new() };
        (state with { Models = state.Models.SetItem(7, loaded) }).Validate();
        Assert.Throws<InvalidDataException>((state with { Models = state.Models.SetItem(1, loaded with { Index = 1 }) }).Validate);
        Assert.Throws<InvalidDataException>((state with { Models = state.Models.SetItem(7, loaded with { ResourceFlags44 = null }) }).Validate);
        Assert.Throws<InvalidDataException>((state with { Models = state.Models.SetItem(7, loaded with { SequenceState = new() { Locals = [] } }) }).Validate);
    }

    [Fact]
    public void Expanded_bank_preserves_each_native_retained_row_and_leaves_extra_fields_unknown()
    {
        BattleModelRoster roster = CreateHostedState(25).PresentationRoster!;
        ImmutableArray<NativeBattleModelRetainedFields> retained = Enumerable.Range(0, 7).Select(row =>
            NativeBattleModelRetainedFields.Capture(new(ushort.MaxValue) { Flags2 = (ushort)(0x8000 + row), Rotation14 = (short)(200 + row), Color44 = (uint)(100 + row) })).ToImmutableArray();
        NativeBattleGraphicsModel[] native = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: retained);
        NativeBattleGraphicsModel[] hosted = NativeBattleGraphicsModels.ClearPresentationBank(roster, retained);
        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(hosted[..7]));

        Assert.Equal(25, hosted.Length);
        for (int row = 0; row < 7; row++)
        {
            Assert.Equal((ushort)0, hosted[row].Flags0);
            Assert.Equal((ushort)(0x8000 + row), hosted[row].Flags2);
            Assert.Equal((short)(200 + row), hosted[row].Rotation14);
            Assert.Equal((uint)(100 + row), hosted[row].Color44);
        }
        for (int row = 7; row < hosted.Length; row++)
        {
            NativeBattleGraphicsModel model = hosted[row];
            Assert.Equal(row, model.Index);
            Assert.Equal((ushort)0, model.Flags0);
            Assert.Null(model.Flags2);
            Assert.Null(model.Rotation14);
            Assert.Null(model.Color44);
            Assert.Null(model.Color40);
            Assert.Null(model.Transform);
            Assert.Null(model.GeometryWorldMatrix);
            Assert.Null(model.SequenceState);
            Assert.Equal(32, model.StatusTimers.Length);
            Assert.All(model.StatusTimers, value => Assert.Null(value));
            Assert.False(hosted[row - 1].StatusTimers == model.StatusTimers);
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleGraphicsModels.ClearPresentationBank(roster, retained.RemoveAt(6)));
    }

    private static NativeBattleGraphicsState CreateHostedState(int count)
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[count];
        rows[0] = new BattleEnemyActorModel(new("enemy"));
        rows[count - 1] = new BattlePartyActorModel(new("party"));
        BattleModelRoster roster = new(rows.ToImmutableArray());
        return new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
    }
}
