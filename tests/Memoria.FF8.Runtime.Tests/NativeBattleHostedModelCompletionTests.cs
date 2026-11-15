using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelCompletionTests
{
    private static readonly BattleModelOwner Original = OriginalOwner(0);
    private static readonly BattleModelOwner ExtraAlly = new BattlePartyActorModel(new("extra-ally"));
    private static readonly BattleModelOwner ExtraEnemy = new BattleEnemyActorModel(new("extra-enemy"));

    [Fact]
    public void Sequence_busy_queries_include_bound_extras_in_model_order()
    {
        BattleActorModels models = Graphics().ActorModels
            .Set(ExtraAlly, new(2) { Kind5 = 1, Flags8 = 0 })
            .Set(ExtraEnemy, new(2));
        Assert.Equal(1, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, Original));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, ExtraAlly));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, OriginalOwner(1)));

        models = models.Set(ExtraAlly, new(0)).Set(ExtraEnemy, new(2) { Kind5 = 1, Flags8 = 0 });
        models = NativeBattleTestSnapshots.Independent(models);
        Assert.Equal(1, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, Original));
        Assert.Equal(0, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, ExtraEnemy));
        Assert.Equal(0, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models.Remove(ExtraEnemy), Original));
    }

    [Fact]
    public void Barrier_waits_for_last_hosted_model_before_reading_the_camera()
    {
        BattleActorModels models = Graphics().ActorModels
            .Set(ExtraAlly, new(2) { Kind5 = 0, Flags8 = 0, ResourceFlags44 = 0x40 })
            .Set(ExtraEnemy, new(2) { Kind5 = 1, Flags8 = 0 });
        byte completion = 17;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(1, null, UnavailableCamera, ref completion));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, models, UnavailableCamera, ref completion));
        Assert.Equal((byte)17, completion);
        models = models.Set(ExtraEnemy, models.Get(ExtraEnemy) with { Kind5 = 0 });
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, UnavailableCamera, ref completion));

        models = models.Set(ExtraEnemy, models.Get(ExtraEnemy) with { ResourceFlags44 = 0x40 });
        models = NativeBattleTestSnapshots.Independent(models);
        Assert.Throws<InvalidOperationException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, UnavailableCamera, ref completion));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, models, () => 1, ref completion));
        Assert.Equal((byte)17, completion);
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models, () => 0, ref completion));
        Assert.Equal((byte)255, completion);
    }

    [Fact]
    public void Direct_barrier_uses_all_current_models_and_does_not_require_original_cell_padding()
    {
        byte completion = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, null, 0, ref completion));

        NativeBattleGraphicsModel ready = new(2) { Kind5 = 0, Flags8 = 0, ResourceFlags44 = 0x40 };
        BattleActorModels models = new([new(Original, ready), new(ExtraEnemy, ready with { ResourceFlags44 = 0 })]);
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        Assert.Equal((byte)0, completion);
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models.Set(ExtraEnemy, ready), 0, ref completion));
        Assert.Equal((byte)255, completion);

        completion = 0;
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models.Remove(ExtraEnemy), 0, ref completion));
        Assert.Equal((byte)255, completion);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(36)]
    [InlineData(50)]
    public void Hosted_graphics_accepts_symbolic_texture_aliases_beyond_the_native_rows(int alias)
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(ExtraEnemy) with { TextureResourceIndex = alias };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(ExtraEnemy, model) };
        NativeBattleGraphicsState restored = NativeBattleTestSnapshots.Independent(graphics);
        restored.Validate();
        Assert.Equal(alias, restored.ActorModels.Get(ExtraEnemy).TextureResourceIndex);
    }

    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, NativeBattleModelResourceRowAddress.MaximumEncodedRowCount)]
    [InlineData(false, NativeBattleModelResourceRowAddress.MaximumEncodedRowCount)]
    public void Graphics_rejects_texture_alias_outside_its_symbolic_domain(bool hosted, int alias)
    {
        NativeBattleGraphicsState graphics = hosted ? Graphics() : OriginalGraphics([(0, Original, NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null))]);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(Original) with { TextureResourceIndex = alias };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(Original, model) };
        Assert.Throws<InvalidDataException>(graphics.Validate);
    }

    private static byte UnavailableCamera()
    {
        throw new InvalidOperationException("The camera owner should not be read yet.");
    }

    private static NativeBattleGraphicsState Graphics()
    {
        BattleModelRoster roster = new([Original, ExtraAlly, ExtraEnemy]);
        return NativeBattleHostedTestFixture.Graphics(roster, new(roster, [(NativeBattleModelSlot.Decode(0), Original)]), _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null));
    }
}
