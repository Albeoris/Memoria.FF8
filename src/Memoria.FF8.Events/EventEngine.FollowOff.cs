using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeFollowOffReceipt(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int PartySlot, byte NativeActorId, byte BaseAnimationId, int AnimationFrames);

public sealed partial class EventEngine
{
    private string? UnsupportedFollowOff(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.FOLLOWOFF)
        {
            return null;
        }
        ActorState actor = actors[fiber.ObjectId];
        int slot = Scene.Party.IndexOf((byte)actor.CharacterId);
        if (slot < 0)
        {
            return null;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.NativeBaseAnimationVersion != 1 || AnimationFrameCount is null || actor.ModelId < 0 || actor.NativeScriptMovement is { Active: true } || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null)
        {
            return "FOLLOWOFF requires the original party mapping and known idle animation without unproved active motion";
        }
        _ = AnimationFrameCount(actor.ModelId, actor.BaseAnimationId);
        return null;
    }

    private void ExecuteFollowOff(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        int slot = Scene.Party.IndexOf((byte)actor.CharacterId);
        if (slot < 0)
        {
            return;
        }
        int frames = AnimationFrameCount!(actor.ModelId, actor.BaseAnimationId);
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(actor.ObjectId));
        // 51EC30 clears only runtime follow mapping1CE4772; saved aliasesB194..196 remain intact.
        Scene.FieldPartyNativeActorIds[slot] = 255;
        actor.InvalidateNativeMotionAuthority();
        actor.BaseFirstFrame = 0;
        actor.BaseLastFrame = frames - 1;
        actor.SetBaseAnimation();
        actor.NativeFollowOff = new NativeFollowOffReceipt(1, FieldId, actor.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, slot, nativeActor, (byte)actor.BaseAnimationId, frames);
    }

    private void ValidateFollowOffCheckpoint(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeFollowOff is not NativeFollowOffReceipt receipt)
            {
                continue;
            }
            EventScript? script = program!.Objects[actor.ObjectId].Scripts.FirstOrDefault(item => item.Id == receipt.ScriptId);
            if (receipt.Version != 1 || receipt.FieldId != saved.FieldId || receipt.ObjectId != actor.ObjectId || receipt.Frame is < 1 || receipt.Frame > saved.Frame || receipt.PartySlot is < 0 or >= 3 || receipt.AnimationFrames < 1 || receipt.NativeActorId >= saved.Scene.NativeActorObjectIds.Count || saved.Scene.NativeActorObjectIds[receipt.NativeActorId] != actor.ObjectId || script is null || receipt.Operation < script.Start || receipt.Operation >= script.End || program.Operations[receipt.Operation].Opcode != Jsm.Opcode.FOLLOWOFF)
            {
                throw new InvalidDataException("Saved FOLLOWOFF receipt lost its original actor, animation or instruction provenance.");
            }
        }
    }
}
