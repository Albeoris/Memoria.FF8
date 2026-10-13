using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeParticleRequest(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, int RawSlot, int Slot, byte Value);

public sealed partial class EventEngine
{
    private string? UnsupportedParticle(Fiber fiber)
    {
        return fiber.Stack.Count == 0 ? "Particle commands require their original slot operand" : null;
    }

    private int[] ExecuteParticle(Fiber fiber, bool enabled)
    {
        int[] operands = Pop(fiber, 1);
        int slot = operands[0] & 15;
        byte value = enabled ? (byte)1 : (byte)0;
        EnsureNativeMediaHistoryBoundary();
        Scene.NativeParticleSlots[slot] = value;
        Scene.NativeParticleRequests.Add(new NativeParticleRequest(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, operands[0], slot, value));
        // TODO(TD-008):. Native473DE0 emission,
        // its PMD instance pool and RNG phase are not rendered by this prototype.
        // OFF changes the emitter flag; it does not claim to kill live particles.
        return operands;
    }

    private void ValidateParticleCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeParticleSlots.Any(pair => (uint)pair.Key >= 16 || pair.Value > 1))
        {
            throw new InvalidDataException("Saved known particle slots have invalid indices or unimplemented emitter values.");
        }
        foreach ((NativeParticleRequest request, int index) in saved.Scene.NativeParticleRequests.Select((request, index) => (request, index)))
        {
            if (request.Version != 1 || (uint)request.FieldId > ushort.MaxValue || !IsNativeHash(request.ProgramHash) || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Slot != (request.RawSlot & 15) || request.Value > 1)
            {
                throw new InvalidDataException("Saved particle request has invalid native issuing provenance.");
            }
            if (index >= saved.Scene.NativeMediaHistoryBoundary!.ParticleRequests)
            {
                Jsm.Opcode expected = request.Value == 1 ? Jsm.Opcode.PARTICLEON : Jsm.Opcode.PARTICLEOFF;
                if (request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.Frame > saved.Frame || !MatchesNativeMediaInstruction(request.ObjectId, request.ScriptId, request.Operation) || program!.Operations[request.Operation].Opcode != expected)
                {
                    throw new InvalidDataException($"Saved particle request differs from its original instruction: object{request.ObjectId}, script{request.ScriptId}, op{request.Operation}, value{request.Value}, frame{request.Frame}/{saved.Frame}.");
                }
            }
        }
        foreach ((int slot, byte value) in saved.Scene.NativeParticleSlots)
        {
            NativeParticleRequest? latest = saved.Scene.NativeParticleRequests.Skip(saved.Scene.NativeMediaHistoryBoundary!.ParticleRequests).LastOrDefault(request => request.Slot == slot);
            if (latest is null || latest.Value != value)
            {
                throw new InvalidDataException("Saved known particle flag has no matching executed slot write.");
            }
        }
    }
}
