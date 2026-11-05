using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldScriptsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_scripts_keep_completed_reward_state_local_widths_operand_wait_and_remote_links_without_init_replay(bool remote)
    {
        byte[] program = Program(remote);
        var (sourceSession, source) = Create(program);
        source.Step();
        sourceSession.SetEventMemory(sourceSession.EventMemory.Write(500, 4, NativeScalarValue.Exact(99)).Write(1100, 2, NativeScalarValue.Exact(1234)).Write(1104, 1, NativeScalarValue.Exact(0x91)));
        source.Step();
        GameplaySaveFieldScripts saved = source.CaptureGameplaySaveScripts();
        Assert.Contains(saved.Fibers, fiber => fiber.Wait == (remote ? GameplaySaveScriptWait.RequestEnd : GameplaySaveScriptWait.Frames));
        if (remote)
        {
            Assert.Contains(saved.Fibers, fiber => fiber.RequestSequence is not null);
        }
        else
        {
            Assert.Contains(saved.Fibers, fiber => fiber.Stack.Contains(91));
        }
        Assert.Contains(saved.Locals, cell => cell.Address == 1100 && cell.Width == 2 && cell.Value == 1234);
        Assert.Contains(saved.Locals, cell => cell.Address == 1104 && cell.Width == 1 && cell.Value == 0x91);
        string encoded = JsonSerializer.Serialize(saved);
        GameplaySaveFieldScripts decoded = JsonSerializer.Deserialize<GameplaySaveFieldScripts>(encoded)!;
        var (targetSession, target) = Create(program, sourceSession.State);
        Assert.Equal(0, target.Frame);
        target.RestoreGameplaySaveScripts(decoded);
        Assert.Equal(encoded, JsonSerializer.Serialize(target.CaptureGameplaySaveScripts()));
        for (int tick = 0; tick < 8; tick++)
        {
            source.Step();
            target.Step();
            Assert.Equal(99, targetSession.EventMemory.Read(500, 4).RequireExact());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(target.CaptureGameplaySaveScripts())));
        }
        Assert.Equal(remote ? 123 : 91, targetSession.EventMemory.Read(1100, 4).RequireExact());
    }

    [Fact]
    public void Physical_popped_operand_words_depths_and_shared_word398_are_retained_without_native_resource_data()
    {
        byte[] program = Program(false);
        var (_, source) = Create(program);
        source.Step();
        NativeScalarValue?[] words = Enumerable.Repeat<NativeScalarValue?>(NativeScalarValue.Exact(0), 80).ToArray();
        words[17] = NativeScalarValue.Exact(unchecked((int)0x89ABCDEF));
        byte?[] depths = [255, 12, 9, 8, 7, 6, 5, 4];
        typeof(ActorState).GetProperty(nameof(ActorState.NativeOperandMemory))!.SetValue(source.Actors[0], new NativeActorOperandMemory(1, words, depths, -1));
        typeof(ActorState).GetProperty(nameof(ActorState.NativeActorWord398))!.SetValue(source.Actors[0], (ushort)0x1234);
        GameplaySaveFieldScripts saved = source.CaptureGameplaySaveScripts();
        var (_, target) = Create(program);
        target.RestoreGameplaySaveScripts(saved);
        Assert.Equal(unchecked((int)0x89ABCDEF), target.Actors[0].NativeOperandMemory!.Words[17]!.RequireExact());
        Assert.Equal(depths, target.Actors[0].NativeOperandMemory!.SavedDepths);
        Assert.Equal((ushort)0x1234, target.Actors[0].NativeActorWord398);
        target.Actors[0].NativeOperandMemory!.Words[17] = NativeScalarValue.Exact(1);
        Assert.Equal(unchecked((int)0x89ABCDEF), saved.Operands[0].Words[17]);
        Assert.DoesNotContain("NativeFieldRegistration", JsonSerializer.Serialize(saved));
        Assert.DoesNotContain("SourceFrame", JsonSerializer.Serialize(saved));
    }

    [Fact]
    public void Malformed_program_pc_request_or_wait_fails_before_installation()
    {
        byte[] program = Program(false);
        var (_, source) = Create(program);
        source.Step();
        source.Step();
        GameplaySaveFieldScripts saved = source.CaptureGameplaySaveScripts();
        var (_, target) = Create(program);
        string before = JsonSerializer.Serialize(target.Capture());
        Assert.Throws<InvalidOperationException>(() => target.RestoreGameplaySaveScripts(saved with { ProgramSha256 = new('A', 64) }));
        GameplaySaveScriptFiber[] invalid = saved.Fibers.ToArray();
        invalid[0] = invalid[0] with { Position = int.MaxValue };
        Assert.Throws<InvalidDataException>(() => target.RestoreGameplaySaveScripts(saved with { Fibers = invalid }));
        invalid[0] = saved.Fibers[0] with { RequestSequence = long.MaxValue };
        Assert.Throws<InvalidDataException>(() => target.RestoreGameplaySaveScripts(saved with { Fibers = invalid }));
        invalid[0] = saved.Fibers[0] with { Wait = (GameplaySaveScriptWait)255 };
        Assert.Throws<InvalidDataException>(() => target.RestoreGameplaySaveScripts(saved with { Fibers = invalid }));
        Assert.Equal(before, JsonSerializer.Serialize(target.Capture()));
    }

    [Fact]
    public void Prepared_movie_selection_and_input_axis_phase_are_logical_script_owners()
    {
        byte[] program = Program(false);
        var (session, source) = Create(program);
        source.Step();
        void Set(string name, object value) => typeof(EventEngine).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(source, value);
        Set("preparedMovie", 3);
        Set("axisStart", 10);
        Set("axisTarget", 20);
        Set("axisDuration", 4);
        Set("axisElapsed", 2);
        typeof(EventEngine).GetProperty(nameof(EventEngine.InputAxisDirection))!.SetValue(source, 15);
        session.SetMovieSource(new(0, 3));
        GameplaySaveFieldScripts saved = source.CaptureGameplaySaveScripts();
        Assert.Equal(3, saved.PreparedMovie);
        Assert.Equal(new GameplaySaveFieldInputAxis(10, 20, 4, 2, 15), saved.InputAxis);
        var (_, target) = Create(program, session.State);
        target.RestoreGameplaySaveScripts(saved);
        Assert.Equal(saved.PreparedMovie, target.CaptureGameplaySaveScripts().PreparedMovie);
        Assert.Equal(saved.InputAxis, target.CaptureGameplaySaveScripts().InputAxis);
    }

    private static (GameSession, EventEngine) Create(byte[] program, GameState? saved = null)
    {
        GameSession session;
        if (saved is null)
        {
            session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.Restore(session.State with { SeeD = null });
        }
        else
        {
            session = new(saved);
        }
        EventEngine engine = new(session);
        engine.LoadField(106, program, []);
        return (session, engine);
    }

    private static byte[] Program(bool remote)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = remote ? [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.REQEW, 1), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode Opcode, int Parameter)[]> scripts = [initialize, ordinary];
        ushort[] groups = remote ? [(0 << 7) | 1, (8 << 7) | 2] : [(0 << 7) | 1];
        if (remote)
        {
            scripts.Add([(Jsm.Opcode.LBL, 8), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 10), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_L, 1108), (Jsm.Opcode.RET, 8)]);
        }
        return Encode(groups, scripts);
    }

    internal static byte[] Encode(ushort[] groups, List<(Jsm.Opcode Opcode, int Parameter)[]> scripts)
    {
        int scriptOffset = 8 + groups.Length * 2;
        int operationsOffset = scriptOffset + (scripts.Count + 1) * 2;
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[3] = checked((byte)groups.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), (ushort)scriptOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)operationsOffset);
        for (int index = 0; index < groups.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), groups[index]);
        }
        int operation = 0;
        for (int index = 0; index < scripts.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptOffset + index * 2), (ushort)operation);
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[index])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.SETMODEL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + 4 * operation++), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptOffset + scripts.Count * 2), (ushort)operation);
        return bytes;
    }
}
