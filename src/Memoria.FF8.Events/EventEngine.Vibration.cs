using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Executed opcode receipt; physical queue/last return remain solely the process owner.</summary>
public sealed record NativeVibrationRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawPattern, int RawPriority, int EffectivePriority, string ExecutableHash, string BankSourceHash, int? PatternOffset, int NativeReturn, [property: JsonRequired] long LoadEpoch);

public sealed partial class EventEngine
{
    public Func<NativeVibrationBank>? ReadNativeVibrationBank { get; set; }

    private NativeVibrationEnqueue PrepareVibrationRequest(Fiber fiber)
    {
        NativeScalarValue[] args = fiber.Stack.Capture();
        return services.VibrationProcess!.Enqueue(ReadNativeVibrationBank!(), args[1].RequireExact(), args[0].RequireExact() | 1);
    }

    private string? UnsupportedVibrationRequest(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SETVIBRATE)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Stack.Count < 2 || services.VibrationProcess is null || !services.CanStoreVibrationProcess || ReadNativeVibrationBank is null)
        {
            return "SETVIBRATE requires its native process queue, original bank, writable owner and two DWORD operands";
        }
        try
        {
            _ = PrepareVibrationRequest(fiber);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteVibrationRequest(Fiber fiber)
    {
        NativeVibrationEnqueue result = PrepareVibrationRequest(fiber);
        NativeVibrationBank bank = ReadNativeVibrationBank!();
        NativeScalarValue[] pending = fiber.Stack.Capture();
        int pattern = pending[1].RequireExact();
        int priority = pending[0].RequireExact();
        //51F620 writes global1D9CDE8, not actor R0. Publish before consuming operands.
        try
        {
            services.SetVibrationProcess(result.State with { LastSetReturn = result.Handle });
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: vibration service rejected SETVIBRATE; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, nameof(Jsm.Opcode.SETVIBRATE), message);
            throw new EventScriptException(message, exception);
        }
        int[] args = Pop(fiber, 2);
        Scene.VibrationRequest = new NativeVibrationRequest(2, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, pattern, priority, priority | 1, NativeRuntimeProfile!.ExecutableHash, bank.SourceHash, (uint)pattern < bank.Offsets.Length ? bank.Offsets[pattern] : null, result.Handle, Scene.AudioLoadEpoch);
        return args;
    }

    private void ValidateVibrationCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.VibrationRequest is not NativeVibrationRequest request)
        {
            return;
        }
        NativeVibrationBank? bank = ReadNativeVibrationBank?.Invoke();
        if (bank is null || request.Version != 2 || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 1 || request.EffectivePriority != (request.RawPriority | 1) || request.ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || request.BankSourceHash != bank.SourceHash || request.PatternOffset != ((uint)request.RawPattern < bank.Offsets.Length ? bank.Offsets[request.RawPattern] : (int?)null) || request.NativeReturn < -1 || request.NativeReturn > 16371)
        {
            throw new InvalidDataException("Saved SETVIBRATE receipt lost its original operands, bank or exact native result.");
        }
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == request.ObjectId)?.Scripts.FirstOrDefault(candidate => candidate.Id == request.ScriptId);
            if (script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.SETVIBRATE)
            {
                throw new InvalidDataException("Saved SETVIBRATE receipt does not identify its executed original instruction.");
            }
        }
    }
}
