using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedTriggerWidgetTests
{
    private static readonly BattlePartyModelOwner Party = new BattlePartyActorModel(new("party"));
    private static readonly BattleEnemyModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattlePartyModelOwner Unbound = new BattlePartyActorModel(new("unbound"));

    [Fact]
    public void AdditionalOwnersRegisterAndPublishTheSameTimedReceipt()
    {
        BattlePartyActorModel baseline = new(new("party"));
        BattlePartyActorModel additional = new(new("additional-party"));
        IEnumerable<BattleEnemyActorModel> otherOwners = Enumerable.Range(0, 30).Select(index => new BattleEnemyActorModel(new($"enemy-{index}")));
        BattleModelRoster roster = new([baseline, .. otherOwners, additional]);
        BattleNativeRenzokukenBlock block = Block();
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        NativeBattleTriggerWidget widget = NativeBattleTriggerWidget.Register(additional, block, 0, ui, roster);
        NativeBattleTriggerWidget native = NativeBattleTriggerWidget.Register(baseline, block, 0, ui, roster);
        Assert.Equal(native with { Actor = additional }, widget);

        BattleNativeRenzokukenContact? contact = null;
        for (int frame = 0; frame < 30 && contact is null; frame++)
        {
            for (int service = 0; service < 4; service++)
            {
                if (service == 3)
                {
                    ui = ui.QueuePublication(4);
                }
                ui = ui.AdvancePublication();
                int distance = widget.Input.PoseClock + widget.Input.SubframeClock - block.Timeline.InputThresholds[0];
                bool press = distance == 20;
                NativeBattleTriggerWidgetStep expected = NativeBattleTriggerWidgetRules.Advance(native with { PendingTrigger = press }, ui.Frame33!.Value, false, null, roster);
                NativeBattleTriggerWidgetStep actual = NativeBattleTriggerWidgetRules.Advance(widget with { PendingTrigger = press }, ui.Frame33.Value, false, null, roster);
                Assert.Equal(expected with { Widget = expected.Widget with { Actor = additional } }, actual);
                widget = actual.Widget;
                native = expected.Widget;
                contact = actual.Contact ?? contact;
            }
        }
        Assert.NotNull(contact);
        Assert.Equal((short)25, contact.TriggerTiming);
        Assert.Equal((byte)8, widget.ClosingPhase);
        var receipt = new NativeBattleTriggerReceipt(additional, contact.Index, contact.Total, contact.TriggerTiming, 32);
        receipt.Validate(roster);
        Assert.Throws<InvalidDataException>(() => receipt.Validate(new BattleModelRoster([baseline])));
        Assert.Throws<InvalidDataException>(() => (receipt with { WordD96 = 31 }).Validate(roster));
    }

    [Fact]
    public void WidgetAndReceiptRejectRetiredOwnersAndTheSameTextOnTheWrongSide()
    {
        BattlePartyActorModel current = new(new("actor-allocation-2"));
        BattlePartyActorModel retired = new(new("actor-allocation-1"));
        BattleEnemyActorModel wrongSide = new(new("actor-allocation-2"));
        BattleModelRoster roster = new([current]);
        BattleNativeRenzokukenBlock block = Block();
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        NativeBattleTriggerWidget bound = NativeBattleTriggerWidget.Register(current, block, 0, ui, roster);
        NativeBattleTriggerReceipt receipt = new(current, 1, 1, 25, 32);
        foreach (BattleModelOwner absent in new BattleModelOwner[] { retired, wrongSide })
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidget.Register(absent, block, 0, ui, roster));
            Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidgetRules.Advance(bound with { Actor = absent }, false, true, new(123), roster));
            Assert.Throws<InvalidDataException>(() => (receipt with { Actor = absent }).Validate(roster));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SessionPreparationKeepsOriginalCueAndUiFieldsWithoutPublishingLogicalState(bool party)
    {
        BattleModelOwner model = party ? Party : Enemy;
        BattleSession owner = Session();
        BattleSnapshot before = owner.Capture();
        NativeBattleGraphicsState graphics = Graphics();
        var cue = new NativeBattleSequenceCue(NativeBattleSequenceCueKind.TriggerWindow, 12, 4) { TriggerWindow = Block() };
        NativeBattleGraphicsState result = Prepare(owner, graphics, model, cue);
        NativeBattleTriggerWidget widget = Assert.IsType<NativeBattleTriggerWidget>(result.TriggerWidget);
        Assert.Equal(model, widget.Actor);
        Assert.Equal((byte?)12, widget.SourceSequence);
        Assert.Equal(4, widget.SourceAbOffset);
        Assert.Equal((ushort?)159, widget.SourceMagicId);
        Assert.Equal(graphics.UiServices!.Menu957, widget.PreviousMenu957);
        Assert.Equal(graphics.UiServices.Menu955, widget.PreviousMenu955);
        Assert.True(widget.Visible);
        Assert.Equal((sbyte)6, result.UiServices!.Menu957);
        Assert.Equal((sbyte?)-2, result.UiServices.Menu955);
        Assert.Null(result.PendingTriggerSoundActor);
        result.Validate();
        (result with { PendingTrigger = new(model, 1, 1, 25, 32) }).Validate();
        Assert.Throws<InvalidDataException>(() => (result with { PendingTrigger = new(Unbound, 1, 1, 25, 32) }).Validate());
        Assert.Null(graphics.TriggerWidget);
        Assert.Equal(NativeBattleTestSnapshots.Json(before), NativeBattleTestSnapshots.Json(owner.Capture()));
    }

    [Fact]
    public void SessionPreparationRejectsMissingRetiredAndWrongSideOwnersBeforePublishing()
    {
        BattleSession owner = Session();
        NativeBattleGraphicsState graphics = Graphics();
        var cue = new NativeBattleSequenceCue(NativeBattleSequenceCueKind.TriggerWindow) { TriggerWindow = Block() };
        TargetInvocationException hole = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics, Unbound, cue));
        Assert.IsType<InvalidDataException>(hole.InnerException);
        TargetInvocationException extent = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { ActorModels = graphics.ActorModels.Remove(Party) }, Party, cue));
        Assert.IsType<InvalidDataException>(extent.InnerException);
        TargetInvocationException identity = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { ActorModels = graphics.ActorModels.ReplaceOwner(Party, new BattlePartyActorModel(new("replacement")), graphics.ActorModels.Get(Party)) }, Party, cue));
        Assert.IsType<InvalidDataException>(identity.InnerException);
        TargetInvocationException wrongSide = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { ActorModels = graphics.ActorModels.ReplaceOwner(Enemy, new BattlePartyActorModel(new("enemy")), graphics.ActorModels.Get(Enemy)) }, Enemy, cue));
        Assert.IsType<InvalidDataException>(wrongSide.InnerException);
        Assert.Null(owner.NativeGraphics);
        Assert.Null(graphics.TriggerWidget);
    }

    private static BattleNativeRenzokukenBlock Block() => NativeBattleRenzokukenRules.ReadBlock([0xAB, 1, 0, 0xA0, 1, 0xA1], 0, new byte[] { 15, 9 });

    private static BattleSession Session() => new(new BattleDefinition
    {
        Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        LimitProgress = new(new string('0', 64), 0, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]) { Options978 = 0 }
    }, BattleAtbMode.Normal);

    private static NativeBattleGraphicsState Graphics()
    {
        BattleModelRoster roster = new([Party, Enemy]);
        return NativeBattleHostedTestFixture.Graphics(roster, new(roster, []), _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ResourceMagicId = 159 }) with
        {
            UiServices = NativeBattleUiServices.Initialize() with { Menu957 = 2, Menu955 = -1 },
            PendingTriggerSoundActor = Party
        };
    }

    private static NativeBattleGraphicsState Prepare(BattleSession owner, NativeBattleGraphicsState graphics, BattleModelOwner model, NativeBattleSequenceCue cue)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("PrepareNativeTriggerWidget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<NativeBattleGraphicsState>(method.Invoke(owner, [graphics, model, cue]));
    }
}
