using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelCompletionTests
{
    [Fact]
    public void Sequence_busy_queries_include_bound_extras_and_preserve_ascending_first_hit()
    {
        BattleModelRoster roster = Roster();
        ImmutableArray<NativeBattleGraphicsModel> models = Models()
            .SetItem(7, new(7, 2) { Kind5 = 1, Flags8 = 0 })
            .SetItem(24, new(24, 2));
        Assert.Equal(1, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 0, roster));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 7, roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 1, roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models[..7], 0, roster));

        models = models.SetItem(7, new(7, 0)).SetItem(24, new(24, 2) { Kind5 = 1, Flags8 = 0 });
        models = NativeBattleTestSnapshots.RestoreJson<ImmutableArray<NativeBattleGraphicsModel>>(NativeBattleTestSnapshots.Json(models));
        Assert.Equal(1, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 0, roster));
        Assert.Equal(0, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, 24, roster));
        Assert.Equal(0, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models[..7].SetItem(1, new(1, 0)), 0));
    }

    [Fact]
    public void Barrier_waits_for_last_hosted_model_before_reading_the_camera()
    {
        BattleModelRoster roster = Roster();
        ImmutableArray<NativeBattleGraphicsModel> models = Models()
            .SetItem(7, new(7, 2) { Kind5 = 0, Flags8 = 0, ResourceFlags44 = 0x40 })
            .SetItem(24, new(24, 2) { Kind5 = 1, Flags8 = 0 });
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollHostedTask112(1, null, UnavailableCamera, ref completion, roster));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollHostedTask112(0, models, UnavailableCamera, ref completion, roster));
        Assert.Equal((byte)17, completion);
        models = models.SetItem(24, models[24] with { Kind5 = 0 });
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollHostedTask112(0, models, UnavailableCamera, ref completion, roster));

        models = models.SetItem(24, models[24] with { ResourceFlags44 = 0x40 });
        models = NativeBattleTestSnapshots.RestoreJson<ImmutableArray<NativeBattleGraphicsModel>>(NativeBattleTestSnapshots.Json(models));
        Assert.Throws<InvalidOperationException>(() => NativeBattleGraphicsCompletion.PollHostedTask112(0, models, UnavailableCamera, ref completion, roster));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollHostedTask112(0, models, () => 1, ref completion, roster));
        Assert.Equal((byte)17, completion);
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollHostedTask112(0, models, () => 0, ref completion, roster));
        Assert.Equal((byte)255, completion);
    }

    [Fact]
    public void Direct_barrier_requires_exactly_seven_native_rows_or_its_explicit_hosted_extent()
    {
        BattleModelRoster roster = Roster();
        NativeBattleGraphicsModelCompletion[] models = Enumerable.Repeat(new NativeBattleGraphicsModelCompletion(0, 0, 0, null), 25).ToArray();
        models[1] = new(2, 0, 0, null);
        models[24] = new(2, 0, 0, 0);
        byte completion = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models[..7], 0, ref completion, roster));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion, roster));
        models[24] = models[24] with { ResourceFlags44 = 0x40 };
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion, roster));
        Assert.Equal((byte)255, completion);

        models[1] = new(0, 0, 0, null);
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models[..7], 0, ref completion));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(36)]
    [InlineData(50)]
    public void Hosted_graphics_accepts_symbolic_texture_aliases_beyond_the_native_rows(int alias)
    {
        BattleModelRoster roster = Roster();
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray();
        models = models.SetItem(24, models[24] with { TextureResourceIndex = alias });
        NativeBattleGraphicsState graphics = new(models, roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        NativeBattleGraphicsState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGraphicsState>(NativeBattleTestSnapshots.Json(graphics));
        restored.Validate();
        Assert.Equal(alias, restored.Models[24].TextureResourceIndex);
    }

    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, 51)]
    [InlineData(false, 11)]
    public void Graphics_rejects_texture_alias_outside_its_symbolic_domain(bool hosted, int alias)
    {
        BattleModelRoster? roster = hosted ? Roster() : null;
        ImmutableArray<NativeBattleGraphicsModel> models = (roster is null ? NativeBattleGraphicsModels.ClearBank() : NativeBattleGraphicsModels.ClearPresentationBank(roster)).ToImmutableArray();
        models = models.SetItem(0, models[0] with { TextureResourceIndex = alias });
        ImmutableArray<string?> ids = roster?.Rows.Select(actor => actor?.ActorId).ToImmutableArray() ?? Enumerable.Repeat<string?>(null, models.Length).ToImmutableArray();
        NativeBattleGraphicsState graphics = new(models, ids, 0, 0) { PresentationRoster = roster };
        Assert.Throws<InvalidDataException>(graphics.Validate);
    }

    private static byte UnavailableCamera()
    {
        throw new InvalidOperationException("The camera owner should not be read yet.");
    }

    private static ImmutableArray<NativeBattleGraphicsModel> Models()
    {
        return Enumerable.Range(0, 25).Select(row => new NativeBattleGraphicsModel(row, row == 1 ? (ushort)2 : (ushort)0)).ToImmutableArray();
    }

    private static BattleModelRoster Roster()
    {
        BattleModelOwner?[] actors = new BattleModelOwner?[25];
        actors[0] = new("native", true);
        actors[7] = new("extra-ally", true);
        actors[24] = new("extra-enemy", false);
        return new(actors.ToImmutableArray());
    }
}
