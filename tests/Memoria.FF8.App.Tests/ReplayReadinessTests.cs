using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ReplayReadinessTests
{
    [Fact]
    public void Rejected_confirmation_of_a_nonconfirming_window_preserves_its_site_without_advancing()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 100 });
        var engine = new EventEngine(session);
        engine.LoadField(100, Program(confirming: false), ["Announcement"]);
        for (int callback = 0; callback < 10 && engine.CurrentDialogue is null; callback++)
        {
            engine.Step();
        }
        DialogueState site = Assert.IsType<DialogueState>(engine.CurrentDialogue);
        Assert.False(site.CanConfirm);
        ReplayJournalEntry attempt = new(ReplayEntryKind.Action, SceneKind.Field, 100, engine.Frame)
        {
            Code = (int)PlayerActionKind.Confirm,
            Accepted = false,
            MessageId = site.MessageId,
            ObjectId = site.ObjectId,
            ScriptId = site.ScriptId,
            Operation = site.Operation
        };
        string before = engine.Capture().GetRawText();
        GameWorld.AwaitRecordedDialogueSite(engine, attempt, () => throw new InvalidOperationException("Rejected input must not wait for a confirming window."));
        Assert.Equal(before, engine.Capture().GetRawText());
        Assert.Throws<InvalidDataException>(() => GameWorld.AwaitRecordedDialogueSite(engine, attempt with { Operation = site.Operation + 1 }, () => throw new InvalidOperationException("A mismatched rejected input must not advance.")));
        Assert.Throws<InvalidDataException>(() => GameWorld.AwaitRecordedDialogueSite(engine, attempt with { FieldId = 101 }, () => throw new InvalidOperationException("A mismatched field must not advance.")));
        Assert.Equal(before, engine.Capture().GetRawText());
        Assert.False(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(site, engine.CurrentDialogue);
    }

    [Fact]
    public void Recorded_visible_decision_waits_for_real_callbacks_and_rejects_a_different_site()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 100 });
        var engine = new EventEngine(session);
        engine.LoadField(100, Program(), ["Question"]);
        ReplayJournalEntry decision = new(ReplayEntryKind.Action, SceneKind.Field, 100, 0)
        {
            Code = (int)PlayerActionKind.Confirm,
            MessageId = 0,
            ObjectId = 0,
            ScriptId = 1,
            Operation = 11
        };
        int advanced = 0;
        GameWorld.AwaitRecordedDialogueSite(engine, decision, () =>
        {
            engine.Step();
            advanced++;
        });
        Assert.True(advanced > 2);
        Assert.Equal(advanced, engine.Frame);
        Assert.Equal(42, session.State.Variables[5000]);
        DialogueState site = Assert.IsType<DialogueState>(engine.CurrentDialogue);
        Assert.Empty(engine.DialogueOutcomes);
        long readyFrame = engine.Frame;
        Assert.Throws<InvalidDataException>(() => GameWorld.AwaitRecordedDialogueSite(engine, decision with { Operation = site.Operation + 1 }, () => throw new InvalidOperationException("A different visible question must not be advanced or answered.")));
        Assert.Equal(readyFrame, engine.Frame);
        Assert.Equal(site, engine.CurrentDialogue);
        Assert.Throws<InvalidDataException>(() => GameWorld.AwaitRecordedDialogueSite(engine, decision with { FieldId = 101 }, () => throw new InvalidOperationException("A different field must not be advanced.")));
    }

    private static byte[] Program(bool confirming = true)
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (confirming ? Jsm.Opcode.AMESW : Jsm.Opcode.AMES, 0), (Jsm.Opcode.RET, 0)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode == Jsm.Opcode.PSHN_L ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + 4 * index), encoded);
        }
        return bytes;
    }
}
