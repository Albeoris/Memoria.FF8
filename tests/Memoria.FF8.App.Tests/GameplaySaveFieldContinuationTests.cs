using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Script_menu_publication_reopens_the_panel_and_returns_without_replaying_initialization(bool save)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (save ? Jsm.Opcode.MENUSAVE : Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] script = GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary]);
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, script));
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        session.SetEventMemory(session.EventMemory.Write(1100, 4, NativeScalarValue.Exact(0)));
        for (int tick = 0; source.Events.PartyMenu?.Phase != NativePartyMenuPhase.Active && tick < 30; tick++)
        {
            source.Events.Step();
        }
        Assert.Equal(NativePartyMenuPhase.Active, source.Events.PartyMenu?.Phase);
        Assert.False(source.Events.CanAcceptMovementInput);
        GameState before = session.State;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-script-menu-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameState restored = source.WriteGameplaySave(path);
            Assert.Same(before, session.State);
            Assert.Equal(before.Economy!.Gil, restored.Economy!.Gil);
            GameSession resumedSession = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(resumedSession, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(source.Events.PartyMenu!.Kind, resumed.Events.PartyMenu!.Kind);
            Assert.Equal(NativePartyMenuPhase.Active, resumed.Events.PartyMenu.Phase);
            Assert.Equal(0, resumedSession.EventMemory.Read(1100, 4).RequireExact());
            Assert.True(source.Events.Apply(new(PlayerActionKind.Cancel)).Accepted);
            Assert.True(resumed.Events.Apply(new(PlayerActionKind.Cancel)).Accepted);
            for (int tick = 0; tick < 30; tick++)
            {
                source.Events.Step();
                resumed.Events.Step();
                Assert.Equal(source.Events.PartyMenu?.Phase, resumed.Events.PartyMenu?.Phase);
                Assert.Equal(session.EventMemory.Read(1100, 4).RequireExact(), resumedSession.EventMemory.Read(1100, 4).RequireExact());
            }
            Assert.Null(resumed.Events.PartyMenu);
            Assert.Equal(9, resumedSession.EventMemory.Read(1100, 4).RequireExact());
            Assert.Equal(before.Economy.Gil, resumedSession.Economy!.Gil);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Selected_resource_field_continuation_restores_wait_locals_and_reward_once_without_initialization_replay()
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program()));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameState loaded = GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets);
        Assert.Equal(123463u, loaded.Economy!.Gil);
        GameSession sourceSession = new(loaded);
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        sourceSession.SetEventMemory(sourceSession.EventMemory.Write(500, 4, NativeScalarValue.Exact(99)).Write(1100, 2, NativeScalarValue.Exact(0x1234)).Write(1104, 1, NativeScalarValue.Exact(0x91)));
        Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Null(source.Events.LastFault);
        Assert.False(source.Events.IsDialogueOpen);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.Frames);
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume);
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        Assert.DoesNotContain(document.Progression.Story.Cells, cell => cell.Address >= 1024 && cell.Address < 2304);
        Assert.Contains(document.Progression.FieldContinuation!.Scripts.Locals, cell => cell.Address == 1100 && cell.Width == 2 && cell.Value == 0x1234);
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        Assert.True(encoded.Length < 1024 * 1024);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameState restored = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.Economy!.Gil, restored.Economy!.Gil);
        Assert.Equal(99, restored.EventMemory.Read(500, 4).RequireExact());
        Assert.Equal(0x1234, restored.EventMemory.Read(1100, 2).RequireExact());
        Assert.Equal(0x91, restored.EventMemory.Read(1104, 1).RequireExact());
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restored.ExamTimer!.ClockCalls);
        Assert.Equal(prepared.Playtime, restored.Playtime);
        Assert.Equal(NativeFieldLoadOrigin.GameplayContinuation, restored.FieldLoadContext!.Origin);
        Assert.Equal(NativeFieldInputOrigin.GameplayContinuationReset, restored.FieldButtonInput!.Origin);
        GameSession resumedSession = new(restored);
        using GameWorld resumed = new(resumedSession, directory, sharedAssets: assets);
        resumed.EnsureField();
        Assert.Equal(NativeFieldInitializationOrigin.GameplayContinuation, resumed.Events.Scene.NativeFieldInitialization!.Origin);
        Assert.Equal(continuation.Scripts.Frame, resumed.Events.Frame);
        for (int tick = 0; tick < 5; tick++)
        {
            PlayerAction wait = new(PlayerActionKind.Wait);
            Assert.Equal(source.Apply(wait), resumed.Apply(wait));
            Assert.Equal(NativeFieldInputOrigin.Sampled, resumedSession.FieldButtonInput!.Origin);
            Assert.Equal(prepared.Economy.Gil, resumedSession.State.Economy!.Gil);
            Assert.Equal(99, resumedSession.EventMemory.Read(500, 4).RequireExact());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
        }
        Assert.Equal(9, resumedSession.EventMemory.Read(1100, 4).RequireExact());
        Assert.NotNull(resumed.Events.Scene.NativeFieldRegistration);
        Assert.NotNull(assets.LoadField(165));
    }

    [Fact]
    public void Transitional_local_aliases_normalize_only_when_equivalent_and_never_mutate_source()
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program()));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        session.SetEventMemory(session.EventMemory.Write(1100, 4, NativeScalarValue.Exact(1234)));
        GameplaySaveFieldContinuation continuation = world.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(session.PrepareSnapshot(), initial.Resume);
        GameplaySaveProgression duplicate = document.Progression! with { FieldContinuation = continuation };
        string before = JsonSerializer.Serialize(duplicate);
        duplicate.Validate();
        GameplaySaveProgression canonical = duplicate.NormalizeFieldContinuation();
        Assert.DoesNotContain(canonical.Story.Cells, cell => cell.Address >= 1024 && cell.Address < 2304);
        Assert.Equal(before, JsonSerializer.Serialize(duplicate));
        GameplaySaveEventCell[] corrupt = duplicate.Story.Cells.ToArray();
        int index = Array.FindIndex(corrupt, cell => cell.Address == 1100);
        corrupt[index] = corrupt[index] with { Value = 999 };
        Assert.Throws<InvalidDataException>(() => (duplicate with { Story = duplicate.Story with { Cells = corrupt } }).NormalizeFieldContinuation());
        GameState restored = GameplaySaveSessionLoader.Restore(document with { Progression = duplicate }, directory, sharedAssets: assets);
        Assert.Equal(1234, restored.EventMemory.Read(1100, 4).RequireExact());
        Assert.Null(canonical.FieldLocation);
        GameplaySaveComponentDocument contradictory = document with { Progression = canonical, Resume = document.Resume with { Field = document.Resume.Field! with { X = document.Resume.Field.X + 1 } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(contradictory, "missing-selected-installation"));
    }

    private static byte[] Program()
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        return GameplaySaveFieldScriptsTests.Encode([(0 << 7) | 1], [initialize, ordinary]);
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
