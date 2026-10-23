using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEscapeContinuationTests
{
    [Fact]
    public void Selected_standalone_escapes_with_remapped_held_keyboard_buttons()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_ESCAPE_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ESCAPE_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(output), "Select a standalone battle save, installed resources and output directory.");
        GameState source = GameSession.Load(input!);
        BattleSnapshot initial = Assert.IsType<BattleSnapshot>(source.Battle);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, initial.Definition, true);
        BattleSession battle = Restore(initial);
        KeyboardInput keyboard = new()
        {
            Profile = ControlsProfile.CreateDefault()
                .WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new InputChord([Keys.NumPad7], []))
                .WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new InputChord([Keys.NumPad9], []))
        };
        keyboard.Read(default, true);
        Directory.CreateDirectory(output!);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        Publish("before");
        for (int frame = 0; frame < 12; frame++)
        {
            Advance(true);
        }
        Assert.All(battle.NativeGraphics!.Models.Take(3), model => Assert.NotEqual(0u, model.Flags8!.Value & 0x1000000));
        Assert.All(battle.NativeGraphics.Models.Take(3), model => Assert.Equal((byte)16, model.Sequence));
        CompareNext("held");
        Advance(false);
        Assert.All(battle.NativeGraphics!.Models.Take(3), model => Assert.Equal(0u, model.Flags8!.Value & 0x1000000));
        Assert.Equal(0, battle.Escape.HeldFrames);
        CompareNext("released");
        for (int frame = 0; frame < 1200 && battle.Outcome == BattleOutcome.Active; frame++)
        {
            Advance(true);
            if (frame % 60 == 0)
            {
                Console.WriteLine($"callback {frame}: frame {battle.Frame}; escape {JsonSerializer.Serialize(battle.Escape)}; disabled {battle.EscapeDisabled}; result {battle.NativeResult}");
            }
        }
        Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
        Assert.True(battle.CanReturnToField);
        Assert.Equal((byte?)2, battle.NativeResult);
        CompareNext("escaped");

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        void Advance(bool held)
        {
            keyboard.Read(held ? new KeyboardState(Keys.NumPad7, Keys.NumPad9) : default, true);
            Assert.Equal(held ? (ushort)3 : (ushort)0, keyboard.HeldControlButtons);
            battle.PublishNativeEscapeButtons(keyboard.HeldControlButtons);
            battle.AdvanceNativeGraphicsPresentation(callback);
        }

        void CompareNext(string name)
        {
            string path = Publish(name);
            BattleSession cold = Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path))!);
            BattleSession direct = Restore(battle.Capture());
            direct.AdvanceNativeGraphicsPresentation(callback);
            cold.AdvanceNativeGraphicsPresentation(callback);
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(scene.Render(direct, 320, 224).ToArray(), new BattleScenePresentation(assets, initial.Definition, true).Render(cold, 320, 224).ToArray());
        }

        string Publish(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            string path = Path.Combine(output!, name + ".battle.json");
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(snapshot));
            using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(scene.Render(battle, 320, 224).ToArray(), 320, 224);
            bitmap.Save(Path.Combine(output!, name + ".png"));
            Console.WriteLine($"{name}: field {source.FieldId}; frame {snapshot.Frame}; native callback {snapshot.NativeGraphics!.Callback}; outcome {snapshot.Outcome}");
            return path;
        }
    }
}
