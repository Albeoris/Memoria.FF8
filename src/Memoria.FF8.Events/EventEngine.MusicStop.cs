using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeMusicChannelState(int ChannelId, byte? MusicId);
public sealed record NativeMusicStop(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawSlot, int Slot, int? PriorChannelId, bool? StopRequested, GlobalSoundBackendStatus BackendStatus);

public sealed partial class EventEngine
{
    private NativeMusicChannelState? ReadMusicChannel(int slot) => slot == 0 ? Scene.MusicChannel108 : Scene.MusicChannel112;

    private bool TryReadMusicStopMemory(JsmOperation operation, out int value)
    {
        value = 0;
        if (operation.Opcode == Jsm.Opcode.PSHM_L && operation.Parameter is 108 or 112 && ReadMusicChannel(operation.Parameter == 108 ? 0 : 1) is NativeMusicChannelState channel)
        {
            value = channel.ChannelId;
            return true;
        }
        if (operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && operation.Parameter is 199 or 200 && ReadMusicChannel(operation.Parameter - 199)?.MusicId is byte music)
        {
            value = music;
            return true;
        }
        return false;
    }

    private string? UnsupportedMusicStopAccess(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.MUSICSTOP)
        {
            if (fiber.Stack.Count == 0 || FieldId < 72)
            {
                return "Native MUSICSTOP requires its original slot operand in an ordinary field";
            }
            int slot = fiber.Stack.Peek() & 1;
            int address = 108 + slot * 4;
            foreach ((int key, NativeMemoryValue value) in EventMemory.Cells)
            {
                if (key < address + 4 && (long)key + value.Width > address || key <= 199 + slot && (long)key + value.Width > 199 + slot)
                {
                    return "Native MUSICSTOP channel intersects an ambiguous bare alias; original PC/operand preserved";
                }
            }
            return null;
        }
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        if (width == 0 || !((operation.Parameter <= 115 && (long)operation.Parameter + width - 1 >= 108) || (operation.Parameter <= 200 && (long)operation.Parameter + width - 1 >= 199)))
        {
            return null;
        }
        return TryReadMusicStopMemory(operation, out _) ? null : "Music stop memory requires known exact channel DWORD or music BYTE authority; unknown/overlapping access preserves PC and operands";
    }

    private int[] ExecuteMusicStop(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        int slot = args[0] & 1;
        NativeMusicChannelState? previous = ReadMusicChannel(slot);
        bool? requested = previous is null ? null : previous.ChannelId != -1;
        byte? music = requested == true ? byte.MaxValue : previous?.MusicId;
        NativeMusicChannelState stopped = new(-1, music);
        if (slot == 0)
        {
            Scene.MusicChannel108 = stopped;
            Scene.MusicPlaying = false;
        }
        else
        {
            Scene.MusicChannel112 = stopped;
        }
        Scene.MusicStop = new NativeMusicStop(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, unchecked((uint)args[0]), slot, previous?.ChannelId, requested, GlobalSoundBackendStatus.Muted);
        AudioCommandExecuted?.Invoke(Scene.MusicStop);
        return args;
    }

    private void ValidateMusicStopCheckpoint(Checkpoint saved)
    {
        foreach (int slot in new[] { 0, 1 })
        {
            NativeMusicChannelState? channel = slot == 0 ? saved.Scene.MusicChannel108 : saved.Scene.MusicChannel112;
            if (channel is not null && saved.EventMemory.Cells.Any(pair => pair.Key < 112 + slot * 4 && (long)pair.Key + pair.Value.Width > 108 + slot * 4 || pair.Key <= 199 + slot && (long)pair.Key + pair.Value.Width > 199 + slot))
            {
                throw new InvalidDataException("Saved music stop channel authority disagrees with its exact memory alias.");
            }
        }
        if (saved.Scene.MusicStop is not NativeMusicStop stopped)
        {
            return;
        }
        if (stopped.Version != 1 || stopped.FieldId < 72 || stopped.ObjectId < 0 || stopped.ScriptId < 0 || stopped.Operation < 0 || stopped.Frame < 0 || stopped.Slot != (stopped.RawSlot & 1) || stopped.StopRequested != (stopped.PriorChannelId is int prior ? prior != -1 : (bool?)null) || stopped.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved music stop lost its original slot width or conditional request provenance.");
        }
        if (stopped.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == stopped.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == stopped.ScriptId);
            if (script is null || stopped.Operation < script.Start || stopped.Operation >= script.End || program.Operations[stopped.Operation].Opcode != Jsm.Opcode.MUSICSTOP)
            {
                throw new InvalidDataException("Saved music stop does not match its original instruction.");
            }
        }
    }
}
