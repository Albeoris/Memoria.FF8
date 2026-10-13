using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeFootstepOffAll(int Version, int FieldId, string ProgramHash, string ExecutableHash, int ObjectId, int ScriptId, int Operation, long Frame, int ActorLayoutVersion, int[] Targets, uint ClearedMask);

public sealed partial class EventEngine
{
    private string? UnsupportedFootstepOffAll(Fiber fiber, JsmOperation operation)
    {
        return operation.Opcode == Jsm.Opcode.FOOTSTEPOFFALL && NativeRuntimeProfile is not { IsVerified: true } ? "FOOTSTEPOFFALL requires its authenticated native dense actor layout" : null;
    }

    private int[] ExecuteFootstepOffAll(Fiber fiber)
    {
        int[] targets = FootstepCopyTargets();
        foreach (int objectId in targets)
        {
            // Native520520 clears only actor DWORD352 mask80 for the current
            // dense native array;520260 exits before audio work when it is clear.
            actors[objectId].FootstepsEnabled = false;
        }
        Scene.FootstepOffAll = new NativeFootstepOffAll(1, FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, Scene.NativeActorLayoutVersion, targets, 0x80);
        return [];
    }

    private void ValidateFootstepOffAllCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.FootstepOffAll is not NativeFootstepOffAll request)
        {
            return;
        }
        int[] allocated = program!.NativeObjects.Take(program.NativeActorCount).ToArray();
        int[] targets = request.ActorLayoutVersion == 0 ? allocated : saved.Scene.NativeActorObjectIds.ToArray();
        if (request.Version != 1 || NativeRuntimeProfile is not { IsVerified: true } || request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.ExecutableHash != NativeRuntimeProfile.ExecutableHash || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Frame > saved.Frame || request.ActorLayoutVersion is < 0 or > 1 || request.ActorLayoutVersion > saved.Scene.NativeActorLayoutVersion || request.Targets is null || !request.Targets.SequenceEqual(targets) || request.ClearedMask != 0x80)
        {
            throw new InvalidDataException("Saved FOOTSTEPOFFALL lost its original runtime, dense actor targets, mask or issuing clock.");
        }
        EventScript? script = program.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
        if (script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.FOOTSTEPOFFALL)
        {
            throw new InvalidDataException("Saved FOOTSTEPOFFALL source does not match its original instruction.");
        }
    }
}
