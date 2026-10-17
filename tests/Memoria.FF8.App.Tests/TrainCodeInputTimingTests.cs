using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class TrainCodeInputTimingTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 3)]
    public void Original_visible_keypad_accepts_three_sequences_at_real_time_with_speed_toggle(bool accelerated, int pressDelay)
    {
        string fixture = TestResources.FixturePath("App.Tests.TrainCodeInputTimingTests.1");
        byte[] original = File.ReadAllBytes(fixture);
        GameSession session = new(GameSession.Load(fixture));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        EventEngine engine = world.Events;
        Assert.Equal(902, engine.FieldId);
        Assert.Contains(engine.Actors, actor => actor.Active && actor.Visible && actor.RenderModelId >= 0);
        GameplayClock clock = new();
        if (accelerated)
        {
            clock.ToggleSpeed();
        }
        Queue<int> digits = new();
        int submitted = 0;
        int errors = 0;
        int successfulSequences = 0;
        engine.CommandExecuted += command =>
        {
            if (command is { ObjectId: 12, ScriptId: 83, Name: "REQSW" } && command.Arguments is [1, >= 88 and <= 91])
            {
                digits.Enqueue(new[] { 32, 64, 128, 16 }[command.Arguments[1] - 88]);
            }
            if (command is { ObjectId: 11, ScriptId: 74, Name: "AMES" } && command.Arguments.Count > 1)
            {
                errors += command.Arguments[1] == 26 ? 1 : 0;
                successfulSequences += command.Arguments[1] == 27 ? 1 : 0;
            }
        };
        Memoria.FF8.KeyboardInput keyboard = new();
        keyboard.Read(default, true);
        int remaining = 0;
        int held = 0;
        bool codeStarted = false;
        for (int sample = 0; sample < 1800; sample++)
        {
            if (engine.CurrentDialogue is { CanConfirm: true } dialogue)
            {
                world.Apply(new(dialogue.FirstChoice is null ? PlayerActionKind.Confirm : PlayerActionKind.Choose, dialogue.FirstChoice ?? 0));
            }
            if (remaining == 0 && digits.TryDequeue(out int digit))
            {
                remaining = 8 + pressDelay;
                held = digit;
                submitted++;
                codeStarted = true;
            }
            int physical = remaining is > 4 and <= 8 ? held : !codeStarted ? 16384 : 0;
            Keys key = physical switch
            {
                32 => Keys.V,
                64 => Keys.X,
                128 => Keys.S,
                16 => Keys.C,
                16384 => Keys.Down,
                _ => Keys.None
            };
            keyboard.Read(key == Keys.None ? default : new KeyboardState(key), true);
            world.Apply(new(PlayerActionKind.FieldButtons, keyboard.HeldFieldButtons));
            if (remaining > 0)
            {
                remaining--;
            }
            bool scripted = engine.IsScriptedInterface;
            engine.Update(clock.Advance(TimeSpan.FromTicks(333334), SceneKind.Field, scripted));
            Assert.Null(engine.LastFault);
            Assert.Equal(0, errors);
            if (successfulSequences == 3 && engine.IsUserControlEnabled)
            {
                break;
            }
        }
        Assert.Equal(12, submitted);
        Assert.Equal(3, successfulSequences);
        Assert.Equal(3, engine.EventMemory.Read(1038, 1).RequireExact());
        Assert.True(engine.IsUserControlEnabled);
        Assert.False(engine.IsScriptedInterface);
        Assert.Equal(accelerated ? 3 : 1, clock.SpeedMultiplier);
        Assert.Equal(original, File.ReadAllBytes(fixture));
    }
}
