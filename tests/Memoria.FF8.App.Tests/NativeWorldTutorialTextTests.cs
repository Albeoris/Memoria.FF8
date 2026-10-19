using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldTutorialTextTests
{
    [Fact]
    public void Original_driving_tutorial_resolves_reverse_and_forward_to_the_native_motor_buttons()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        string text = assets.ReadWorldMessage(49);
        string formatted = DialogueTextFormatter.Format(text, buttonBindingHint: buttons => $"[{buttons:X4}]");
        Assert.Contains("[0010]:Back", formatted, StringComparison.Ordinal);
        Assert.Contains("[0080]:Forward", formatted, StringComparison.Ordinal);
        Assert.Contains("[0040]:Get on/off", formatted, StringComparison.Ordinal);
        Assert.Contains("[0002]:Change POV", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalFieldCheckpointStillLoadsWithoutInventingItsMissingMessageSpeed()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeWorldTutorialTextTests.1")));
        Assert.Null(session.State.InputConfiguration!.FieldMessageSpeedByte);
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(session.State.InputConfiguration.FieldMessageSpeedByte);
    }

    [Theory]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(52)]
    public void OriginalTutorialIgnoresArrowDuringRevealAndClosesOnArrowAfterCompletion(int messageId)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldMessage message = assets.ReadNativeWorldMessage(9, messageId) with { Text = NativeWorldTextState.Create(ImmutableArray.CreateRange(assets.ReadWorldMessageBytes(messageId)), assets.ReadWorldMessageSpeed(2)) };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().WithScriptMode(14).Show(message);
        NativeWorldSalaryState staged = NativeWorldSalaryState.Begin(new GameState { Scene = SceneKind.WorldMap, World = new() { EventUi = ui }, SavedWorld = NativeSavedWorldState.CreateNewGame() with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(0x013F) } });
        void Select(uint buttons)
        {
            WorldFieldInputContext input = new(buttons, 0, false, -1, -1, -1, -1);
            Assert.Null(assets.WorldEvents.SelectWorldEvent(new(24391, -27958, 40, 33), _ => false, staged.SavedWorldEvents!.WorldEventFlags116!, (opcode, operand) => opcode switch
            {
                0xFF25 => staged.EventUi!.ScriptMode == operand,
                0xFF20 => staged.EventUi!.CloseSuppressedThisCallback ? false : input.Matches(operand),
                0xFF2C => staged.EventUi!.MatchesTextComplete(unchecked((sbyte)operand), unchecked((byte)(operand >> 8))),
                _ => throw new NotSupportedException($"Unexpected tutorial predicate {opcode:X4}.")
            }, (opcode, operand) => staged = staged.WithWorldEventCommand(opcode, operand)));
        }
        staged = staged.BeginWorldEventCallback();
        Select(0x8000);
        Assert.False(staged.EventUi!.MatchesTextComplete(9, 1));
        Assert.Equal(14, staged.EventUi.ScriptMode);
        for (int tick = 0; tick < 400 && staged.EventUi!.MatchesTextComplete(9, 1) != true; tick++)
        {
            staged = staged.Advance(null, null, null, 0, 2).BeginWorldEventCallback();
            Select(0);
        }
        Assert.True(staged.EventUi!.MatchesTextComplete(9, 1));
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(staged.EventUi))!;
        Assert.True(restored.MatchesTextComplete(9, 1));
        Select(0x8000);
        Assert.Empty(staged.EventUi!.Windows);
        Assert.Equal(0, staged.EventUi.ScriptMode);
        Assert.True(staged.SavedWorldEvents!.WorldEventFlags116!.Matches(0x003F));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualCarCallbacksRetainReadinessAndRestoreWhileArrowIsHeld(bool nativeButtons)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 30)).Accepted);
        Assert.Equal(49, world.CurrentDialogue!.MessageId);
        PlayerAction hold = nativeButtons ? new(PlayerActionKind.WorldButtons, 0x1000) : new(PlayerActionKind.Move, Horizontal: 1);
        Assert.True(world.Apply(hold).Accepted);
        Assert.Equal(49, world.CurrentDialogue!.MessageId);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld second = new(restored, TestResources.GameDirectory, validateWorldPresentation: false);
        second.EnsureField();
        for (int tick = 0; tick < 250 && session.State.World!.EventUi!.MatchesTextComplete(9, 1) != true; tick++)
        {
            Assert.Equal(world.Apply(hold), second.Apply(hold));
        }
        Assert.True(session.State.World!.EventUi!.MatchesTextComplete(9, 1));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), second.Apply(new PlayerAction(PlayerActionKind.Wait)));
        PlayerAction close = nativeButtons ? new(PlayerActionKind.WorldButtons, 0x4000) : new(PlayerActionKind.Move, Horizontal: -1);
        Assert.Equal(world.Apply(close), second.Apply(close));
        Assert.Null(world.CurrentDialogue);
        if (nativeButtons)
        {
            Assert.Equal(0, session.State.World!.NativeCarRuntime!.Speed);
            Assert.Equal(0x4000, session.State.WorldProcess!.InputHistory!.CurrentButtons);
        }
    }
}

