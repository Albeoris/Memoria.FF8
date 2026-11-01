using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeJunctionEditInputTests
{
    private static PlayerAction Input()
    {
        NativeJunctionEditPlan plan = new(1, new string('A', 64), [new NativeJunctionEditRow(3, new NativeCharacterJunction(0, 0, 0, new byte[20]))]);
        MenuAction menu = new("junction-edit", Value: JsonSerializer.Serialize(plan));
        Assert.True(NativeJunctionMenuProjection.IsAction(menu));
        return NativeJunctionMenuProjection.PlayerAction(menu);
    }

    [Fact]
    public void ExplicitActionNumberAndVersionedPayloadRoundTripThroughTheControlContract()
    {
        PlayerAction action = Input();
        Assert.Equal(39, (int)action.Kind);
        Assert.Equal(32, (int)PlayerActionKind.AutoJunction);
        Assert.Equal(34, (int)PlayerActionKind.ReassignJunctionMagic);
        Assert.False(Enum.IsDefined(typeof(PlayerActionKind), 33));
        Assert.True(NativeJunctionMenuInput.IsMenuAction(action.Kind));
        PlayerAction restored = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(action))!;
        NativeJunctionMenuInput.Validate(restored);
        Assert.Equal(action, restored);
        Assert.Equal(3, JsonSerializer.Deserialize<NativeJunctionEditPlan>(restored.Text!)!.Rows.Single().Record);
    }

    [Fact]
    public void InputRejectsNonMenuOperandsAndMissingOrUnversionedPlans()
    {
        PlayerAction action = Input();
        PlayerAction[] malformed = [action with { Value = 1 }, action with { Quantity = 1 }, action with { Frames = 2 }, action with { Horizontal = 1 }, action with { Vertical = 1 }, action with { CameraHorizontal = 1 }, action with { WorldButtons = 1 }, action with { WaitWhilePlayable = true }, action with { Text = null }, action with { Text = "null" }];
        Assert.All(malformed, input => Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(input)));
        NativeJunctionEditPlan plan = JsonSerializer.Deserialize<NativeJunctionEditPlan>(action.Text!)!;
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(action with { Text = JsonSerializer.Serialize(plan with { Version = 2 }) }));
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(action with { Text = JsonSerializer.Serialize(plan with { Rows = [plan.Rows[0], plan.Rows[0]] }) }));
    }
}
