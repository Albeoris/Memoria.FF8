using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeAllSoundPosition(int Version, string Opcode, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int? RawDuration, int RawPan, byte DurationByte, int DurationMilliseconds, byte Pan, [property: JsonRequired] long LoadEpoch);

public sealed partial class EventEngine
{
    private string? UnsupportedRemainingSoundCode(Fiber fiber, JsmOperation operation)
    {
        int count = operation.Opcode switch
        {
            Jsm.Opcode.ALLSEPOS or Jsm.Opcode.Unknown14 or Jsm.Opcode.KEYSIGHNCHANGE => 1,
            Jsm.Opcode.ALLSEPOSTRANS => 2,
            _ => 0
        };
        if (count == 0)
        {
            return null;
        }
        if (operation.Opcode is Jsm.Opcode.ALLSEPOS or Jsm.Opcode.ALLSEPOSTRANS && FieldId < 72)
        {
            return "Native all-voice pan requires an ordinary field allocation";
        }
        return fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact) ? $"{operation.Opcode} requires its original exact DWORD operands" : null;
    }

    private int[] ExecuteAllSoundPosition(Fiber fiber, bool transition)
    {
        int[] args = Pop(fiber, transition ? 2 : 1);
        //520040 doubles the pan before the46BF30 BYTE call; duration is the preceding BYTE.
        byte duration = transition ? unchecked((byte)args[0]) : (byte)0;
        byte pan = transition ? (byte)(unchecked((byte)(2 * args[1])) >> 1) : (byte)(unchecked((byte)args[0]) >> 1);
        Scene.AllSoundPosition = new NativeAllSoundPosition(1, transition ? "ALLSEPOSTRANS" : "ALLSEPOS", FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, transition ? args[0] : null, args[^1], duration, SoundPositionDuration(duration), pan, Scene.AudioLoadEpoch);
        AudioCommandExecuted?.Invoke(Scene.AllSoundPosition);
        return args;
    }

    private void ValidateAllSoundPositionCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.AllSoundPosition is not { } request)
        {
            return;
        }
        bool transition = request.Opcode == "ALLSEPOSTRANS";
        byte duration = transition ? unchecked((byte)(request.RawDuration ?? 0)) : (byte)0;
        byte pan = transition ? (byte)(unchecked((byte)(2 * request.RawPan)) >> 1) : (byte)(unchecked((byte)request.RawPan) >> 1);
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        if (request.Version != 1 || request.FieldId is < 72 or > ushort.MaxValue || request.Frame < 0 || request.Opcode is not ("ALLSEPOS" or "ALLSEPOSTRANS") || transition != request.RawDuration.HasValue || request.DurationByte != duration || request.DurationMilliseconds != SoundPositionDuration(duration) || request.Pan != pan || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0)
        {
            throw new InvalidDataException("Saved all-voice pan lost its original operands or native BYTE conversion.");
        }
        if (request.FieldId == saved.FieldId && (program is null || request.Operation >= program.Operations.Count || program.Operations[request.Operation].Opcode.ToString() != request.Opcode || !program.Objects.Any(item => item.Index == request.ObjectId && item.Scripts.Any(script => script.Id == request.ScriptId && script.ContainsOperation(request.Operation)))))
        {
            throw new InvalidDataException("Saved all-voice pan does not identify its executed instruction.");
        }
    }
}
