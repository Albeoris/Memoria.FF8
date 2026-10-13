using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public enum NativeVibrationBackendStatus
{
    Muted
}

public enum NativeVibrationReturnDomain
{
    MissingPatternOrNoQueue
}

public static class NativeVibrationPatternTable
{
    public const int Address = 0xB8DCB0;
    public const int EntryCount = 64;
    public const string Hash = "1A01EBCDDE88CA7FE6A1511AD6F77C540B7ADDA57313CBD2A1A4A7E318E1CAC1";
    private static readonly int[] DefinedOffsets = [256, 270, 308, 332, 350, 370, 394];

    public static int Offset(int index)
    {
        if ((uint)index >= EntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return index < DefinedOffsets.Length ? DefinedOffsets[index] : 0;
    }
}

public sealed record NativeVibrationRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawPattern, int RawPriority, int EffectivePriority, string ExecutableHash, string TableHash, int PatternOffset, NativeVibrationReturnDomain ReturnDomain, NativeVibrationBackendStatus BackendStatus)
{
    [JsonIgnore]
    public IReadOnlyList<int> PossibleNativeReturns => [-1, 0];
}

public sealed partial class EventEngine
{
    private string? UnsupportedVibrationRequest(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SETVIBRATE)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Stack.Count < 2)
        {
            return "SETVIBRATE requires its verified native executable and two original DWORD operands";
        }
        int pattern = fiber.Stack.ToArray()[1];
        if ((uint)pattern >= NativeVibrationPatternTable.EntryCount)
        {
            return "SETVIBRATE pattern DWORD lies outside the original embedded pointer table";
        }
        if (NativeVibrationPatternTable.Offset(pattern) != 0)
        {
            return "SETVIBRATE defined pattern requires the original vibration queue, counters and callback authority";
        }
        return null;
    }

    private int[] ExecuteVibrationRequest(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        // 51F620 passes the full DWORD priority|1 to4A29A0. Its zero
        // pointer entry returns-1, or an unavailable queue returns0 first.
        // Both branches leave the original queue and hardware unchanged.
        Scene.VibrationRequest = new NativeVibrationRequest(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], args[1], args[1] | 1, NativeRuntimeProfile!.ExecutableHash, NativeVibrationPatternTable.Hash, NativeVibrationPatternTable.Offset(args[0]), NativeVibrationReturnDomain.MissingPatternOrNoQueue, NativeVibrationBackendStatus.Muted);
        return args;
    }

    private void ValidateVibrationCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.VibrationRequest is not NativeVibrationRequest request)
        {
            return;
        }
        if (request.Version != 1 || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 1 || (uint)request.RawPattern >= NativeVibrationPatternTable.EntryCount || request.EffectivePriority != (request.RawPriority | 1) || request.ExecutableHash != Memoria.FF8.Runtime.NativeFieldRuntimeProfile.FF8EnExecutableHash || request.TableHash != NativeVibrationPatternTable.Hash || NativeVibrationPatternTable.Offset(request.RawPattern) != 0 || request.PatternOffset != 0 || request.ReturnDomain != NativeVibrationReturnDomain.MissingPatternOrNoQueue || request.BackendStatus != NativeVibrationBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved SETVIBRATE request lost its original pattern, DWORD operands, native return domain or muted backend.");
        }
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == request.ObjectId)?.Scripts.FirstOrDefault(candidate => candidate.Id == request.ScriptId);
            if (request.Frame > saved.Frame || script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.SETVIBRATE)
            {
                throw new InvalidDataException("Saved SETVIBRATE request does not identify its executed original instruction.");
            }
        }
    }
}
