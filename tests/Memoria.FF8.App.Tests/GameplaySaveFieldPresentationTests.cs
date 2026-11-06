using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldPresentationTests
{
    [Fact]
    public void Fresh_load_resets_shake_while_logical_continuation_restores_its_current_phase()
    {
        NativeFieldShake active = NativeFieldShake.FromColdProcess() with { X = new(true, true, 17, -3, 80, -12, 20, 30, 7), Y = new(false, true, 31, 6, 60, 11, -15, 24, 5) };
        NativeFieldShake loaded = active.ResetForLoad();
        Assert.False(loaded.X.Enabled);
        Assert.False(loaded.X.Oscillating);
        Assert.Equal(0, loaded.X.Duration);
        Assert.Equal(0, loaded.X.Counter);
        GameplaySaveFieldPresentation logical = GameplaySaveFieldPresentation.Capture(NativeFieldBarsState.CreateHidden(), active)!;
        GameplaySaveFieldPresentation decoded = JsonSerializer.Deserialize<GameplaySaveFieldPresentation>(JsonSerializer.Serialize(logical))!;
        Assert.Equal(active, decoded.RestoreShake(NativeFieldShake.FromColdProcess()));
        Assert.DoesNotContain("ExecutableHash", JsonSerializer.Serialize(decoded));
        Assert.DoesNotContain("randomBytes", JsonSerializer.Serialize(decoded));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Semantic_write_load_preserves_bar_operands_and_shake_phase_before_the_next_callback(bool hidden)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 25), (Jsm.Opcode.DISPBAR, 0), (Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.SHAKE, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 75), (Jsm.Opcode.SETBAR, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] script = GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary]);
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, script));
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; session.FieldShake?.X.Enabled != true && tick < 10; tick++)
        {
            source.Events.Step();
        }
        Assert.True(session.FieldShake!.X.Enabled);
        Assert.NotNull(session.FieldBars!.First);
        if (hidden)
        {
            session.SetFieldBars(session.FieldBars.Hide(0));
        }
        NativeFieldBarsState bars = session.FieldBars;
        NativeFieldShake shake = session.FieldShake;
        uint gil = session.State.Economy!.Gil;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-field-presentation-" + Guid.NewGuid().ToString("N"));
        try
        {
            source.WriteGameplaySave(path);
            GameSession restored = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(restored, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(NativeFieldInitializationOrigin.GameplayContinuation, resumed.Events.Scene.NativeFieldInitialization!.Origin);
            Assert.Equal(bars, restored.FieldBars);
            Assert.Equal(shake, restored.FieldShake);
            Assert.Equal(gil, restored.State.Economy!.Gil);
            for (int tick = 0; tick < 10; tick++)
            {
                source.Events.Step();
                resumed.Events.Step();
                Assert.Equal(session.FieldBars, restored.FieldBars);
                Assert.Equal(session.FieldShake, restored.FieldShake);
            }
            Assert.Equal((short)75, restored.FieldBars!.First!.Mirror.Value);
            Assert.Equal(!hidden, restored.FieldBars.First.Display.Visible);
            GameplaySaveFieldPresentation presentation = resumed.Events.CaptureGameplaySaveFieldContinuation().Scene.Presentation!;
            string encoded = JsonSerializer.Serialize(presentation);
            Assert.DoesNotContain("ExecutableHash", encoded);
            Assert.DoesNotContain("AdjacentMirrorWord", encoded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
