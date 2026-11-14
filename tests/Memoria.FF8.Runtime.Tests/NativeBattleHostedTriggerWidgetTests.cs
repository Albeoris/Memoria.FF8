using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedTriggerWidgetTests
{
    [Theory]
    [InlineData((byte)7)]
    [InlineData((byte)24)]
    public void HostedWidgetRegistersAndPublishesTheNativeTimedReceiptAtItsBoundRow(byte row)
    {
        NativeBattleGraphicsRoster roster = Roster();
        BattleNativeRenzokukenBlock block = Block();
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        NativeBattleTriggerWidget widget = NativeBattleTriggerWidget.Register(row, block, 0, ui, roster);
        NativeBattleTriggerWidget native = NativeBattleTriggerWidget.Register(6, block, 0, ui);
        Assert.Equal(native with { ActorIndex = row }, widget);

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
                NativeBattleTriggerWidgetStep expected = NativeBattleTriggerWidgetRules.Advance(native with { PendingTrigger = press }, ui.Frame33!.Value, false, null);
                NativeBattleTriggerWidgetStep actual = NativeBattleTriggerWidgetRules.Advance(widget with { PendingTrigger = press }, ui.Frame33.Value, false, null, roster: roster);
                Assert.Equal(expected with { Widget = expected.Widget with { ActorIndex = row } }, actual);
                widget = actual.Widget;
                native = expected.Widget;
                contact = actual.Contact ?? contact;
            }
        }
        Assert.NotNull(contact);
        Assert.Equal((short)25, contact.TriggerTiming);
        Assert.Equal((byte)8, widget.ClosingPhase);
        var receipt = new NativeBattleTriggerReceipt(row, contact.Index, contact.Total, contact.TriggerTiming, 32);
        receipt.Validate(roster);
        Assert.Throws<InvalidDataException>(() => receipt.Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { ActorIndex = 8 }).Validate(roster));
        Assert.Throws<InvalidDataException>(() => (receipt with { WordD96 = 31 }).Validate(roster));
    }

    [Fact]
    public void RegistrationAndAdvancementRejectHolesAndOutOfExtentRows()
    {
        NativeBattleGraphicsRoster roster = Roster();
        BattleNativeRenzokukenBlock block = Block();
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidget.Register(8, block, 0, ui, roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidget.Register(25, block, 0, ui, roster));
        NativeBattleTriggerWidget bound = NativeBattleTriggerWidget.Register(24, block, 0, ui, roster);
        Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidgetRules.Advance(bound, false, true, new(123)));
        Assert.Throws<InvalidDataException>(() => NativeBattleTriggerWidgetRules.Advance(bound with { ActorIndex = 8 }, false, true, new(123), roster: roster));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(24)]
    public void SessionPreparationKeepsOriginalCueAndUiFieldsWithoutPublishingLogicalState(int row)
    {
        BattleSession owner = Session();
        BattleSnapshot before = owner.Capture();
        NativeBattleGraphicsState graphics = Graphics();
        var cue = new NativeBattleSequenceCue(NativeBattleSequenceCueKind.TriggerWindow, 12, 4) { TriggerWindow = Block() };
        NativeBattleGraphicsState result = Prepare(owner, graphics, row, cue);
        NativeBattleTriggerWidget widget = Assert.IsType<NativeBattleTriggerWidget>(result.TriggerWidget);
        Assert.Equal((byte)row, widget.ActorIndex);
        Assert.Equal((byte?)12, widget.SourceSequence);
        Assert.Equal(4, widget.SourceAbOffset);
        Assert.Equal((ushort?)159, widget.SourceMagicId);
        Assert.Equal(graphics.UiServices!.Menu957, widget.PreviousMenu957);
        Assert.Equal(graphics.UiServices.Menu955, widget.PreviousMenu955);
        Assert.True(widget.Visible);
        Assert.Equal((sbyte)6, result.UiServices!.Menu957);
        Assert.Equal((sbyte?)-2, result.UiServices.Menu955);
        Assert.Null(result.PendingTriggerSoundActorIndex);
        result.Validate();
        (result with { PendingTrigger = new((byte)row, 1, 1, 25, 32) }).Validate();
        Assert.Throws<InvalidDataException>(() => (result with { PendingTrigger = new(8, 1, 1, 25, 32) }).Validate());
        Assert.Null(graphics.TriggerWidget);
        Assert.Equal(NativeBattleTestSnapshots.Json(before), NativeBattleTestSnapshots.Json(owner.Capture()));
    }

    [Fact]
    public void SessionPreparationRejectsHolesAndConcreteBankMismatchBeforePublishing()
    {
        BattleSession owner = Session();
        NativeBattleGraphicsState graphics = Graphics();
        var cue = new NativeBattleSequenceCue(NativeBattleSequenceCueKind.TriggerWindow) { TriggerWindow = Block() };
        TargetInvocationException hole = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics, 8, cue));
        Assert.IsType<InvalidDataException>(hole.InnerException);
        TargetInvocationException extent = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { Models = graphics.Models[..8] }, 7, cue));
        Assert.IsType<InvalidDataException>(extent.InnerException);
        TargetInvocationException identity = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { ActorIds = graphics.ActorIds.SetItem(7, "retired-party") }, 7, cue));
        Assert.IsType<InvalidDataException>(identity.InnerException);
        TargetInvocationException modelIndex = Assert.Throws<TargetInvocationException>(() => Prepare(owner, graphics with { Models = graphics.Models.SetItem(24, graphics.Models[24] with { Index = 23 }) }, 24, cue));
        Assert.IsType<InvalidDataException>(modelIndex.InnerException);
        Assert.Null(owner.NativeGraphics);
        Assert.Null(graphics.TriggerWidget);
    }

    private static BattleNativeRenzokukenBlock Block() => NativeBattleRenzokukenRules.ReadBlock([0xAB, 1, 0, 0xA0, 1, 0xA1], 0, new byte[] { 15, 9 });

    private static BattleSession Session() => new(new BattleDefinition
    {
        Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
        LimitProgress = new(new string('0', 64), 0, 0, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]) { Options978 = 0 }
    }, BattleAtbMode.Normal);

    private static NativeBattleGraphicsRoster Roster() => new(Enumerable.Range(0, 25).Select(row => row switch
    {
        7 => new NativeBattleGraphicsActor("party", true),
        24 => new NativeBattleGraphicsActor("enemy", false),
        _ => null
    }).ToImmutableArray());

    private static NativeBattleGraphicsState Graphics()
    {
        NativeBattleGraphicsRoster roster = Roster();
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, roster.ActorCount).Select(row => new NativeBattleGraphicsModel(row, 0) { ResourceMagicId = roster.ContainsRow(row) ? (ushort)159 : null }).ToImmutableArray();
        return new(models, roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = roster,
            UiServices = NativeBattleUiServices.Initialize() with { Menu957 = 2, Menu955 = -1 },
            PendingTriggerSoundActorIndex = 7
        };
    }

    private static NativeBattleGraphicsState Prepare(BattleSession owner, NativeBattleGraphicsState graphics, int row, NativeBattleSequenceCue cue)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("PrepareNativeTriggerWidget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<NativeBattleGraphicsState>(method.Invoke(owner, [graphics, row, cue]));
    }
}
