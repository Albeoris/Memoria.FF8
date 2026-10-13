using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeFootstepPairSource(int Version, string Kind, int FieldId, string ProgramHash, string ExecutableHash, int ObjectId, int ScriptId, int Operation, long Frame, int? RawValue = null, int? RawParameter = null);
public sealed record NativeFootstepPairCopy(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int ActorLayoutVersion, int[] Targets, byte First392, byte Second393);
public sealed record NativeFootstepPairState(int Version, byte First392, byte Second393, NativeFootstepPairSource Source, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeFootstepPairCopy? Copy = null);

public sealed partial class EventEngine
{
    private int[] FootstepCopyTargets() => Scene.NativeActorLayoutVersion == 1 ? Scene.NativeActorObjectIds.ToArray() : program!.NativeObjects.Take(program.NativeActorCount).ToArray();

    private string? UnsupportedFootstepPair(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.FOOTSTEP or Jsm.Opcode.FOOTSTEPCOPY or Jsm.Opcode.FOOTSTEPCUT))
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native footstep pair requires its authenticated original actor BYTE392/393 layout";
        }
        if (operation.Opcode == Jsm.Opcode.FOOTSTEPCOPY && FootstepCopyTargets().Length == 0)
        {
            return null;
        }
        if (!program!.NativeObjects.Take(program.NativeActorCount).Contains(fiber.ObjectId))
        {
            return "Native footstep pair requires its original allocated actor BYTE392/393 layout";
        }
        if (operation.Opcode == Jsm.Opcode.FOOTSTEP)
        {
            return fiber.Stack.Count < 1 ? "FOOTSTEP requires its original DWORD operand before byte narrowing" : null;
        }
        if (operation.Opcode == Jsm.Opcode.FOOTSTEPCUT)
        {
            return null;
        }
        return FootstepCopyTargets().Length > 0 && actors[fiber.ObjectId].NativeFootstepPair is null ? "FOOTSTEPCOPY requires executed or authenticated allocated source BYTE392/393 authority" : null;
    }

    private int[] ExecuteFootstepPair(Fiber fiber, JsmOperation operation)
    {
        int[] args = Pop(fiber, 1);
        actors[fiber.ObjectId].NativeFootstepPair = new NativeFootstepPairState(1, unchecked((byte)args[0]), unchecked((byte)operation.Parameter), new NativeFootstepPairSource(1, nameof(Jsm.Opcode.FOOTSTEP), FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], operation.Parameter));
        return args;
    }

    private void ExecuteFootstepCopy(Fiber fiber)
    {
        int[] targets = FootstepCopyTargets();
        if (targets.Length == 0)
        {
            return;
        }
        NativeFootstepPairState source = actors[fiber.ObjectId].NativeFootstepPair!;
        NativeFootstepPairCopy copy = new(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, Scene.NativeActorLayoutVersion, targets, source.First392, source.Second393);
        foreach (int objectId in targets)
        {
            // 520490 reads the source pair once, then copies both BYTEs to the
            // dense native actor array. Visibility/activity and mask80 do not gate it.
            actors[objectId].NativeFootstepPair = new NativeFootstepPairState(1, source.First392, source.Second393, source.Source, copy);
        }
    }

    private void ExecuteFootstepCut(Fiber fiber)
    {
        // 520550 writes both selection BYTEs directly; mask80 and audio remain independent.
        actors[fiber.ObjectId].NativeFootstepPair = new NativeFootstepPairState(1, 255, 255, new NativeFootstepPairSource(1, nameof(Jsm.Opcode.FOOTSTEPCUT), FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame));
    }

    private void ValidateFootstepPairCheckpoint(Checkpoint saved)
    {
        int[] allocated = program!.NativeObjects.Take(program.NativeActorCount).ToArray();
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeFootstepPair is not NativeFootstepPairState pair)
            {
                continue;
            }
            NativeFootstepPairSource source = pair.Source;
            if (pair.Version != 1 || source is null || source.Version != 1 || source.FieldId != saved.FieldId || source.ProgramHash != saved.ProgramHash || source.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || !allocated.Contains(actor.ObjectId) || !allocated.Contains(source.ObjectId) || source.Frame < 0 || source.Frame > saved.Frame)
            {
                throw new InvalidDataException("Saved footstep pair lacks its original actor/runtime source.");
            }
            if (source.Kind == "Allocation")
            {
                if (pair.First392 != 255 || pair.Second393 != 255 || source.ScriptId != -1 || source.Operation != -1 || source.Frame != 0 || source.RawValue is not null || source.RawParameter is not null || saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) != true)
                {
                    throw new InvalidDataException("Saved footstep allocation disagrees with original52BC00 FF/FF.");
                }
            }
            else if (source.Kind == nameof(Jsm.Opcode.FOOTSTEP))
            {
                if (source.RawValue is not int raw || source.RawParameter is not int parameter || pair.First392 != unchecked((byte)raw) || pair.Second393 != unchecked((byte)parameter))
                {
                    throw new InvalidDataException("Saved FOOTSTEP lost its operand/immediate BYTE narrowing.");
                }
                ValidateFootstepSite(source.ObjectId, source.ScriptId, source.Operation, Jsm.Opcode.FOOTSTEP, source.RawParameter);
            }
            else if (source.Kind == nameof(Jsm.Opcode.FOOTSTEPCUT))
            {
                if (pair.First392 != 255 || pair.Second393 != 255 || source.RawValue is not null || source.RawParameter is not null)
                {
                    throw new InvalidDataException("Saved FOOTSTEPCUT lost its original FF/FF constant writes.");
                }
                ValidateFootstepSite(source.ObjectId, source.ScriptId, source.Operation, Jsm.Opcode.FOOTSTEPCUT);
            }
            else
            {
                throw new InvalidDataException("Unknown saved native footstep byte source.");
            }
            if (pair.Copy is NativeFootstepPairCopy copy)
            {
                int[] targets = copy.ActorLayoutVersion == 0 ? allocated : saved.Scene.NativeActorObjectIds.ToArray();
                if (copy.Version != 1 || copy.FieldId != saved.FieldId || !allocated.Contains(copy.ObjectId) || copy.Frame < source.Frame || copy.Frame > saved.Frame || copy.ActorLayoutVersion is < 0 or > 1 || copy.Targets is null || !copy.Targets.SequenceEqual(targets) || !copy.Targets.Contains(actor.ObjectId) || copy.First392 != pair.First392 || copy.Second393 != pair.Second393)
                {
                    throw new InvalidDataException("Saved FOOTSTEPCOPY lost its captured pair, source or dense target order.");
                }
                ValidateFootstepSite(copy.ObjectId, copy.ScriptId, copy.Operation, Jsm.Opcode.FOOTSTEPCOPY);
            }
            else if (source.ObjectId != actor.ObjectId)
            {
                throw new InvalidDataException("Saved footstep bytes refer to another actor without executed copy provenance.");
            }
        }
    }

    private void ValidateFootstepSite(int objectId, int scriptId, int operation, Jsm.Opcode opcode, int? parameter = null)
    {
        EventScript? script = program!.Objects[objectId].Scripts.FirstOrDefault(item => item.Id == scriptId);
        if (script is null || operation < script.Start || operation >= script.End || program.Operations[operation].Opcode != opcode || parameter is int expected && program.Operations[operation].Parameter != expected)
        {
            throw new InvalidDataException("Saved footstep source does not match its original instruction.");
        }
    }
}
