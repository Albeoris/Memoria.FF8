using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTriggerWidgetTests
{
    [Fact]
    public void Actual_registration_consumes_phase_zero_and_hidden_widget_still_reaches_one_timed_contact()
    {
        NativeBattleTriggerWidget widget = Window();
        Assert.Equal((byte)1, widget.Input.Phase);
        Assert.False(widget.Visible);
        BattleNativeRenzokukenContact? contact = null;
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize();
        for (int frame = 0; frame < 30 && contact is null; frame++)
        {
            for (int service = 0; service < 4; service++)
            {
                if (service == 3)
                {
                    ui = ui.QueuePublication(4);
                }
                ui = ui.AdvancePublication();
                int distance = widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[0];
                widget = widget with { PendingTrigger = distance == 20 };
                NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(widget, ui.Frame33!.Value, false, null);
                widget = step.Widget;
                contact = step.Contact ?? contact;
            }
        }
        Assert.NotNull(contact);
        Assert.Equal((short)25, contact.TriggerTiming);
        Assert.Equal((byte)1, contact.Index);
        Assert.Equal((byte)1, contact.Total);
        Assert.Equal((byte)8, widget.ClosingPhase);
        Assert.Equal((byte)8, widget.Input.Phase);
    }

    [Fact]
    public void Contact_completion_keeps_the_widget_registered_through_tail_twenty_four_callbacks_and_the_final_frame_edge()
    {
        NativeBattleTriggerWidget widget = Window() with { Input = new(8, 104, 0, 1, 0), ClosingPhase = 8 };
        NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(widget, true, false, null);
        Assert.Equal((byte)10, step.Widget.ClosingPhase);
        Assert.Equal((short)23, step.Widget.ClosingRemaining);
        Assert.False(step.Unregistered);
        widget = JsonSerializer.Deserialize<NativeBattleTriggerWidget>(JsonSerializer.Serialize(step.Widget))!;
        for (int callback = 0; callback < 23; callback++)
        {
            step = NativeBattleTriggerWidgetRules.Advance(widget, false, false, null);
            Assert.Equal(callback == 22, step.BeginClose);
            widget = step.Widget;
        }
        Assert.Equal((byte)12, widget.ClosingPhase);
        Assert.False(NativeBattleTriggerWidgetRules.Advance(widget, false, false, null).Unregistered);
        Assert.True(NativeBattleTriggerWidgetRules.Advance(widget, true, false, null).Unregistered);
    }

    [Fact]
    public void Native_automatic_option_draws_only_the_owned_UI_random_stream_at_distance_twenty_five()
    {
        NativeBattleTriggerWidget widget = Window() with { Input = new(6, 68, 2, 0, 0) };
        NativeBattleUiRandomState random = new(123);
        NativeBattleTriggerWidgetStep first = NativeBattleTriggerWidgetRules.Advance(widget, false, true, random);
        NativeBattleUiRandomState expected = random.Draw(out ushort roll);
        Assert.Equal(expected, first.Random);
        Assert.Equal(roll <= 0x2000 ? (short)1 : (short)25, first.Widget.Input.TriggerTiming);
        NativeBattleTriggerWidget restored = JsonSerializer.Deserialize<NativeBattleTriggerWidget>(JsonSerializer.Serialize(first.Widget))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleTriggerWidgetRules.Advance(first.Widget, false, true, first.Random)), JsonSerializer.Serialize(NativeBattleTriggerWidgetRules.Advance(restored, false, true, first.Random)));
    }

    [Fact]
    public void Timing_success_has_no_invented_upper_bound_and_retains_the_separate_fourth_WORD()
    {
        NativeBattleTriggerReceipt receipt = new(0, 1, 1, short.MaxValue, 32);
        receipt.Validate();
        NativeBattleTriggerReceipt restored = JsonSerializer.Deserialize<NativeBattleTriggerReceipt>(JsonSerializer.Serialize(receipt))!;
        Assert.Equal(receipt, restored);
        Assert.Equal((short)32, restored.WordD96);
        Assert.Throws<InvalidDataException>(() => (receipt with { Timing = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { WordD96 = 31 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { ContactIndex = -1 }).Validate());
    }

    private static NativeBattleTriggerWidget Window()
    {
        BattleNativeRenzokukenBlock block = new(0, [16, 9], NativeBattleRenzokukenRules.Timeline([16, 9]));
        return NativeBattleTriggerWidget.Register(0, block, 0, NativeBattleUiServices.Initialize());
    }
}
