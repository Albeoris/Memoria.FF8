using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum NativeBattleFlagWriter
{
    BattleMode,
    Battle,
    FieldLoad,
    DisplayTimer,
    KillTimer
}

public sealed record NativeBattleFlagWrite(int Version, NativeBattleFlagWriter Writer, int FieldId, string ProgramHash, long Frame, int ObjectId, int ScriptId, int Operation, int? RawOperand, ushort PersistentFlags, ushort ActiveFlags);

public sealed record NativeBattleFlagConsumerReceipt(ushort RequestedFlags, ushort? PersistentModeFlags, bool ReplacedActiveFlags, bool NativeEncounterFlagMergeEmulated);

public sealed partial class EventEngine
{
    private string? UnsupportedBattleMode(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.BATTLEMODE)
        {
            if (fiber.Stack.Count == 0)
            {
                return "BATTLEMODE requires its original WORD operand";
            }
            if (variables.Keys.Any(address => address is >= 179 and <= 183) || services.NativeVariableValues?.Any(pair => pair.Key <= 183 && pair.Key + pair.Value.Width > 182) == true)
            {
                return "BATTLEMODE overlaps an unproved legacy or uncertain WORD182 alias; PC and operand preserved";
            }
        }
        int width = NativeMemoryWidth(operation.Opcode);
        if (Scene.NativeBattleFlagWrites is not null && width > 0 && operation.Parameter <= 183 && (long)operation.Parameter + width > 182)
        {
            return "Native battle-mode WORD182 access requires the shared exact byte/word memory projection; PC and operands preserved";
        }
        return null;
    }

    private int[] ExecuteNativeBattleMode(Fiber fiber)
    {
        int raw = fiber.Stack.Peek();
        ushort word = unchecked((ushort)raw);
        if (services.ExamTimer is NativeExamTimerState timer)
        {
            try
            {
                services.SetExamTimer(timer with { Event182Mask4 = (word & 4) != 0, SaveGlobalMask4 = (word & 4) != 0, Event183Mask1 = (word & 0x100) != 0 });
            }
            catch (NotSupportedException exception)
            {
                fiber.Position--;
                string message = "Native BATTLEMODE timer service rejected its shared WORD182 write; PC and operand preserved.";
                LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.BATTLEMODE.ToString(), message);
                throw new EventScriptException(message, exception);
            }
        }
        AppendBattleFlagWrite(NativeBattleFlagWriter.BattleMode, fiber, raw, word, word);
        return Pop(fiber, 1);
    }

    private void AppendBattleFlagWrite(NativeBattleFlagWriter writer, Fiber? fiber, int? raw, ushort persistent, ushort active)
    {
        Scene.NativeBattleFlagWrites ??= [];
        Scene.NativeBattleFlagWrites.Add(new NativeBattleFlagWrite(1, writer, FieldId, programHash, Frame, fiber?.ObjectId ?? -1, fiber?.Script.Id ?? -1, fiber is null ? -1 : fiber.Position - 1, raw, persistent, active));
    }

    private void CopyNativeBattleMode(SceneEventState previous)
    {
        if (previous.NativeBattleFlagWrites is not { Count: > 0 } history)
        {
            return;
        }
        Scene.NativeBattleFlagWrites = [.. history];
        ushort persistent = history[^1].PersistentFlags;
        AppendBattleFlagWrite(NativeBattleFlagWriter.FieldLoad, null, null, persistent, persistent);
        if (services.ExamTimer is NativeExamTimerState timer)
        {
            services.SetExamTimer(ProjectBattleModeTimer(timer));
        }
    }

    private NativeExamTimerState ProjectBattleModeTimer(NativeExamTimerState timer)
    {
        if (Scene.NativeBattleFlagWrites is not { Count: > 0 } history)
        {
            return timer;
        }
        NativeBattleFlagWrite flags = history[^1];
        return timer with { Event182Mask4 = (flags.PersistentFlags & 4) != 0, Event183Mask1 = (flags.PersistentFlags & 0x100) != 0, SaveGlobalMask4 = (flags.ActiveFlags & 4) != 0 };
    }

    private void RecordBattleModeTimerWrite(Fiber fiber, Jsm.Opcode opcode)
    {
        if (Scene.NativeBattleFlagWrites is not { Count: > 0 } history || opcode is not (Jsm.Opcode.DISPTIMER or Jsm.Opcode.KILLTIMER))
        {
            return;
        }
        NativeBattleFlagWrite prior = history[^1];
        bool set = opcode == Jsm.Opcode.DISPTIMER;
        ushort persistent = unchecked((ushort)(set ? prior.PersistentFlags | 4 : prior.PersistentFlags & ~4));
        ushort active = unchecked((ushort)(set ? prior.ActiveFlags | 4 : prior.ActiveFlags & ~4));
        AppendBattleFlagWrite(set ? NativeBattleFlagWriter.DisplayTimer : NativeBattleFlagWriter.KillTimer, fiber, null, persistent, active);
    }

    private void ValidateNativeBattleModeCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeBattleFlagWrites is not { } history)
        {
            return;
        }
        if (history.Count == 0)
        {
            throw new InvalidDataException("Native battle-mode history cannot be empty or infer its initial WORD.");
        }
        NativeBattleFlagWrite? prior = null;
        int lastLoad = history.FindLastIndex(write => write?.Writer == NativeBattleFlagWriter.FieldLoad);
        for (int index = 0; index < history.Count; index++)
        {
            NativeBattleFlagWrite write = history[index];
            if (write is null || write.Version != 1 || !Enum.IsDefined(write.Writer) || write.FieldId is < 0 or > ushort.MaxValue || write.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || write.Frame < 0 || write.Writer != NativeBattleFlagWriter.FieldLoad && (write.Frame == 0 || write.ObjectId < 0 || write.ScriptId < 0 || write.Operation < 0) || write.Writer == NativeBattleFlagWriter.FieldLoad && (write.Frame != 0 || write.ObjectId != -1 || write.ScriptId != -1 || write.Operation != -1 || write.RawOperand is not null))
            {
                throw new InvalidDataException("Native battle-mode writer has invalid original source metadata.");
            }
            ushort persistent;
            ushort active;
            if (write.Writer == NativeBattleFlagWriter.BattleMode && write.RawOperand is int rawMode)
            {
                persistent = active = unchecked((ushort)rawMode);
            }
            else if (prior is null)
            {
                throw new InvalidDataException("Native battle-mode history requires a reached BATTLEMODE full-WORD write.");
            }
            else if (write.Writer == NativeBattleFlagWriter.Battle && write.RawOperand is int rawBattle)
            {
                persistent = prior.PersistentFlags;
                active = unchecked((ushort)rawBattle);
            }
            else if (write.Writer == NativeBattleFlagWriter.FieldLoad)
            {
                persistent = active = prior.PersistentFlags;
            }
            else if (write.Writer is NativeBattleFlagWriter.DisplayTimer or NativeBattleFlagWriter.KillTimer && write.RawOperand is null)
            {
                bool set = write.Writer == NativeBattleFlagWriter.DisplayTimer;
                persistent = unchecked((ushort)(set ? prior.PersistentFlags | 4 : prior.PersistentFlags & ~4));
                active = unchecked((ushort)(set ? prior.ActiveFlags | 4 : prior.ActiveFlags & ~4));
            }
            else
            {
                throw new InvalidDataException("Native battle-mode writer lacks its original operand contract.");
            }
            if (write.PersistentFlags != persistent || write.ActiveFlags != active)
            {
                throw new InvalidDataException("Native battle-mode writer changed its full-WORD replacement, bit4 mask or field-load copy.");
            }
            if (write.Writer == NativeBattleFlagWriter.Battle)
            {
                NativeBattleCompletion[] matches = saved.Scene.NativeBattleCompletions?.Where(completion => completion.FieldId == write.FieldId && completion.ProgramHash == write.ProgramHash && completion.Frame == write.Frame && completion.ObjectId == write.ObjectId && completion.ScriptId == write.ScriptId && completion.Operation == write.Operation).ToArray() ?? [];
                if (matches.Length != 1 || matches[0].Suppressed || matches[0].RawFlags != write.RawOperand || matches[0].FlagConsumer?.PersistentModeFlags != persistent)
                {
                    throw new InvalidDataException("Native active battle flags lack their matching reached battle consumer receipt.");
                }
            }
            Jsm.Opcode expectedOpcode = write.Writer switch
            {
                NativeBattleFlagWriter.BattleMode => Jsm.Opcode.BATTLEMODE,
                NativeBattleFlagWriter.Battle => Jsm.Opcode.BATTLE,
                NativeBattleFlagWriter.DisplayTimer => Jsm.Opcode.DISPTIMER,
                _ => Jsm.Opcode.KILLTIMER
            };
            if (index >= lastLoad && (write.FieldId != saved.FieldId || write.ProgramHash != saved.ProgramHash || write.Frame > saved.Frame || write.Writer != NativeBattleFlagWriter.FieldLoad && ((uint)write.Operation >= program!.Operations.Count || program.Operations[write.Operation].Opcode != expectedOpcode || (uint)write.ObjectId >= program.Objects.Count || !program.Objects[write.ObjectId].Scripts.Any(script => script.Id == write.ScriptId && write.Operation >= script.Start && write.Operation < script.End))))
            {
                throw new InvalidDataException("Native battle-mode writer disagrees with its reached original command.");
            }
            prior = write;
        }
        if (saved.ExamTimer is NativeExamTimerState timer && (timer.Event182Mask4 != ((prior!.PersistentFlags & 4) != 0) || timer.Event183Mask1 != ((prior.PersistentFlags & 0x100) != 0) || timer.SaveGlobalMask4 != ((prior.ActiveFlags & 4) != 0)))
        {
            throw new InvalidDataException("Native battle-mode WORDs and shared timer masks disagree.");
        }
    }
}
