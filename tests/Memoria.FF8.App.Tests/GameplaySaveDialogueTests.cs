using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveDialogueTests
{
    [Theory]
    [InlineData(Jsm.Opcode.MESW)]
    [InlineData(Jsm.Opcode.AMESW)]
    [InlineData(Jsm.Opcode.ASK)]
    [InlineData(Jsm.Opcode.AASK)]
    [InlineData(Jsm.Opcode.MESSYNC)]
    public void Player_save_restores_issued_native_dialogue_selection_and_close_without_replaying_initialization(Jsm.Opcode opcode)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(opcode)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < 20 && source.Events.Dialogues.Count == 0; tick++)
        {
            source.Events.Step();
        }
        Assert.Single(source.Events.Dialogues);
        bool choice = opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
        for (int tick = 0; tick < 30 && (source.Events.Dialogues.Single().NativeWindow!.Progress < 4096 || source.CurrentDialogue is null); tick++)
        {
            source.Events.Step();
        }
        Assert.Equal(4096, source.Events.Dialogues.Single().NativeWindow!.Progress);
        Assert.NotNull(source.CurrentDialogue);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-dialogue-save-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore store = new(Path.Combine(folder, "diagnostic.json"), Path.Combine(folder, "saves"), discoverOriginalSaves: false, loadMemoria: path => PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        try
        {
            for (int phase = 0; phase < 3; phase++)
            {
                if (phase == 1)
                {
                    Assert.True(source.Apply(new(choice ? PlayerActionKind.Choose : PlayerActionKind.Confirm, choice ? 1 : 0)).Accepted);
                }
                if (phase == 2)
                {
                    source.Events.Step();
                }
                GameplaySaveFieldScripts before = source.Events.CaptureGameplaySaveScripts();
                Assert.NotNull(before.Dialogues);
                Assert.Contains(before.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.Dialogue);
                if (phase == 0)
                {
                    AssertMalformedDialoguesRejected(before, opcode, assets, session.State);
                }
                store.Save(1, session, writeGameplay: source.WriteGameplaySave);
                GameSession copySession = new(store.Load(1));
                using GameWorld copy = new(copySession, directory, sharedAssets: assets);
                copy.EnsureField();
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(copy.Events.CaptureGameplaySaveScripts()));
                Assert.Equal(session.State.Economy!.Gil, copySession.State.Economy!.Gil);
                Assert.Equal(session.State.Playtime, copySession.State.Playtime);
                Assert.Equal(JsonSerializer.Serialize(session.State.ExamTimer), JsonSerializer.Serialize(copySession.State.ExamTimer));
                Assert.Equal(source.Events.Dialogues.Single().Text, copy.Events.Dialogues.Single().Text);
                if (phase == 0)
                {
                    Assert.True(copy.Apply(new(choice ? PlayerActionKind.Choose : PlayerActionKind.Confirm, choice ? 1 : 0)).Accepted);
                }
                for (int tick = 0; tick < 16; tick++)
                {
                    copy.Events.Step();
                }
                Assert.Empty(copy.Events.Dialogues);
                Assert.Equal(77, copySession.EventMemory.Read(1100, 4).RequireExact());
                if (choice)
                {
                    Assert.Equal(1, copySession.EventMemory.Read(1104, 4).RequireExact());
                }
                Assert.Equal(session.State.Economy.Gil, copySession.State.Economy!.Gil);
            }
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void Legacy_dialogue_free_scripts_omit_the_optional_owner()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.Restore(session.State with { SeeD = null });
        EventEngine engine = new(session);
        engine.LoadField(106, GameplaySaveFieldScriptsTests.Encode([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]), []);
        engine.Step();
        GameplaySaveFieldScripts saved = engine.CaptureGameplaySaveScripts();
        Assert.Null(saved.Dialogues);
        Assert.DoesNotContain("DialogueWait", JsonSerializer.Serialize(saved));
        Assert.DoesNotContain("Dialogues", JsonSerializer.Serialize(saved));
    }

    private static void AssertMalformedDialoguesRejected(GameplaySaveFieldScripts saved, Jsm.Opcode opcode, GameAssets assets, GameState state)
    {
        GameplaySaveFieldDialogues owner = saved.Dialogues!;
        GameplaySaveDialogue window = Assert.Single(owner.Windows);
        GameplaySaveScriptFiber waiter = Assert.Single(saved.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.Dialogue);
        GameplaySaveScriptFiber[] withoutConsumer = saved.Fibers.Select(fiber => fiber == waiter ? fiber with { DialogueWait = fiber.DialogueWait! with { Consumer = null } } : fiber).ToArray();
        GameplaySaveScriptFiber[] wrongChannel = saved.Fibers.Select(fiber => fiber == waiter ? fiber with { DialogueWait = fiber.DialogueWait! with { Channel = 7 } } : fiber).ToArray();
        GameplaySaveFieldScripts[] invalid =
        [
            saved with { Dialogues = null },
            saved with { Dialogues = owner with { Windows = [window, window] } },
            saved with { Dialogues = owner with { Windows = [window with { Window = window.Window! with { Delta = 1 } }] } },
            saved with { Fibers = withoutConsumer },
            saved with { Fibers = wrongChannel }
        ];
        EventEngine target = new(new GameSession(state));
        target.LoadField(165, Program(opcode), assets.LoadField(165).Messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        string before = JsonSerializer.Serialize(target.Capture());
        foreach (GameplaySaveFieldScripts malformed in invalid)
        {
            Assert.Throws<InvalidDataException>(() => target.RestoreGameplaySaveScripts(malformed));
            Assert.Equal(before, JsonSerializer.Serialize(target.Capture()));
        }
    }
    private static byte[] Program(Jsm.Opcode opcode)
    {
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0)];
        if (opcode == Jsm.Opcode.MESSYNC)
        {
            ordinary.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0)]);
        }
        int[] operands = opcode switch
        {
            Jsm.Opcode.MESW => [0, 0],
            Jsm.Opcode.AMESW => [0, 0, 20, 30],
            Jsm.Opcode.ASK => [0, 0, 0, 1, 0, 1],
            Jsm.Opcode.AASK => [0, 0, 0, 1, 0, 1, 20, 30],
            _ => [0]
        };
        ordinary.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
        ordinary.AddRange([(opcode, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1104), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)], ordinary.ToArray()]);
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
