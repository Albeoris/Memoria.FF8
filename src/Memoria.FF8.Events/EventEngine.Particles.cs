using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeParticleRequest(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, int RawSlot, int Slot, byte Value, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? InstanceIndex = null);

public sealed partial class EventEngine
{
    private string? UnsupportedParticle(Fiber fiber, Jsm.Opcode opcode)
    {
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Particle commands require their exact original DWORD slot operand";
        }
        if (opcode == Jsm.Opcode.PARTICLESET)
        {
            try
            {
                _ = PrepareNativeParticleSetInstance(fiber);
            }
            catch (NotSupportedException exception)
            {
                return exception.Message;
            }
        }
        return null;
    }

    private int[] ExecuteParticle(Fiber fiber, Jsm.Opcode opcode)
    {
        int? instance = opcode == Jsm.Opcode.PARTICLESET ? PrepareNativeParticleSetInstance(fiber) : null;
        int[] operands = Pop(fiber, 1);
        int slot = operands[0] & 15;
        byte value = opcode switch
        {
            Jsm.Opcode.PARTICLEON => 1,
            Jsm.Opcode.PARTICLEOFF => 0,
            Jsm.Opcode.PARTICLESET => unchecked((byte)(instance!.Value | 0x80)),
            _ => throw new InvalidOperationException("Unknown native particle byte writer.")
        };
        EnsureNativeMediaHistoryBoundary();
        Scene.NativeParticleSlots[slot] = value;
        Scene.NativeParticleRequests.Add(new NativeParticleRequest(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, operands[0], slot, value, instance));
        if (Scene.NativeFieldParticles is { } particles)
        {
            Scene.NativeFieldParticles = particles.WithEmitter(slot, value);
        }
        return operands;
    }

    private void ValidateParticleCheckpoint(Checkpoint saved)
    {
        bool reconstructed = saved.Scene.NativeParticleSlotOrigin == NativeParticleControlOrigin.GameplayContinuation;
        if (!Enum.IsDefined(saved.Scene.NativeParticleSlotOrigin) || reconstructed && (NativeRuntimeProfile is not { IsVerified: true } || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed, Origin: NativeFieldInitializationOrigin.GameplayContinuation } proof || !proof.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash)))
        {
            throw new InvalidDataException("Saved semantic particle controls require their explicit current field reconstruction origin.");
        }
        if (saved.Scene.NativeParticleSlots.Any(pair => (uint)pair.Key >= 16 || pair.Value is > 1 and < 0x80))
        {
            throw new InvalidDataException("Saved known particle slots have invalid indices or unimplemented emitter values.");
        }
        if (reconstructed && saved.Scene.NativeParticleSlots.Values.Any(value => (value & 0x80) != 0 && (value & 0x7F) >= saved.Scene.NativeActorObjectIds.Count))
        {
            throw new InvalidDataException("Saved semantic particle attachment names an absent retained actor ordinal.");
        }
        saved.Scene.NativeFieldParticles?.Validate();
        if (saved.Scene.NativeFieldParticles is { } particleState)
        {
            byte[] definition = ReadNativeFieldParticleDefinition?.Invoke(saved.FieldId) ?? throw new InvalidDataException("Saved field particles require the current original PMD definition.");
            particleState.ValidateDefinition(definition);
            if (saved.Scene.NativeFieldParticleRandomPhase != particleState.RandomPhase || saved.Scene.NativeFieldParticleSprites is null)
            {
                throw new InvalidDataException("Saved field particle presentation omits its current random phase or drawn frame.");
            }
            particleState.ValidateDrawings(saved.Scene.NativeFieldParticleSprites);
            foreach ((int slot, byte value) in saved.Scene.NativeParticleSlots)
            {
                if (particleState.Emitters[slot] != value)
                {
                    throw new InvalidDataException("Saved field particle emitter differs from its current command byte.");
                }
            }
        }
        foreach ((NativeParticleRequest request, int index) in saved.Scene.NativeParticleRequests.Select((request, index) => (request, index)))
        {
            bool validValue = request.InstanceIndex is int instance ? instance is >= 0 and < byte.MaxValue && request.Value == unchecked((byte)(instance | 0x80)) : request.Value <= 1;
            if (request.Version != 1 || (uint)request.FieldId > ushort.MaxValue || !IsNativeHash(request.ProgramHash) || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Slot != (request.RawSlot & 15) || !validValue)
            {
                throw new InvalidDataException("Saved particle request has invalid native issuing provenance.");
            }
            if (index >= saved.Scene.NativeMediaHistoryBoundary!.ParticleRequests)
            {
                Jsm.Opcode expected = request.InstanceIndex is not null ? Jsm.Opcode.PARTICLESET : request.Value == 1 ? Jsm.Opcode.PARTICLEON : Jsm.Opcode.PARTICLEOFF;
                if (request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.Frame > saved.Frame || !MatchesNativeMediaInstruction(request.ObjectId, request.ScriptId, request.Operation) || program!.Operations[request.Operation].Opcode != expected)
                {
                    throw new InvalidDataException($"Saved particle request differs from its original instruction: object{request.ObjectId}, script{request.ScriptId}, op{request.Operation}, value{request.Value}, frame{request.Frame}/{saved.Frame}.");
                }
                if (request.InstanceIndex is int currentInstance && (NativeRuntimeProfile is not { IsVerified: true } || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.NativeActorObjectIds.IndexOf(request.ObjectId) != currentInstance || saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) != true || saved.Scene.NativeFieldInitialization.Stage != Memoria.FF8.Runtime.NativeFieldInitializationStage.Completed))
                {
                    throw new InvalidDataException("Saved PARTICLESET binding differs from the executed finalized native BYTE598 layout.");
                }
            }
        }
        foreach ((int slot, byte value) in saved.Scene.NativeParticleSlots)
        {
            NativeParticleRequest? latest = saved.Scene.NativeParticleRequests.Skip(saved.Scene.NativeMediaHistoryBoundary!.ParticleRequests).LastOrDefault(request => request.Slot == slot);
            if (latest is null ? !reconstructed : latest.Value != value)
            {
                throw new InvalidDataException("Saved known particle flag has no matching executed slot write.");
            }
        }
        foreach (NativeParticleRequest latest in saved.Scene.NativeParticleRequests.Skip(saved.Scene.NativeMediaHistoryBoundary!.ParticleRequests).GroupBy(request => request.Slot).Select(group => group.Last()))
        {
            if (!saved.Scene.NativeParticleSlots.TryGetValue(latest.Slot, out byte value) || value != latest.Value)
            {
                throw new InvalidDataException("Saved particle state omits or changes a current-field executed slot write.");
            }
        }
    }
}
