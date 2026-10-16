using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeFollowOffReceipt(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int PartySlot, byte NativeActorId, byte BaseAnimationId, [property: JsonRequired] int? AnimationFrames);

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
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.NativeBaseAnimationVersion != 1 || actor.NativeAnimationHeader?.BoundModelId is null)
        {
            return "FOLLOWOFF requires its original party mapping, allocated animation header and executed resource binding";
        }
        if (actor.NativeAnimationHeader.BoundModelId != -1)
        {
            try
            {
                _ = NativeAnimationTerminal(actor, unchecked((byte)actor.BaseAnimationId));
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or InvalidOperationException)
            {
                return exception.Message;
            }
        }
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
        byte idle = unchecked((byte)actor.BaseAnimationId);
        int? frames = RequireNativeAnimationHeader(actor).BoundModelId == -1 ? null : AnimationFrameCount!(actor.RenderModelId, idle);
        byte nativeActor = checked((byte)Scene.NativeActorObjectIds.IndexOf(actor.ObjectId));
        // 51EC30 clears only runtime follow mapping1CE4772; saved aliasesB194..196 remain intact.
        Scene.FieldPartyNativeActorIds[slot] = 255;
        actor.NativePartyFlags = new NativeActorPartyFlags(false, true);
        //51EC30 resets only the idle header and runtime mapping. Physical
        //mode572, completion542, targets, backup,575/576 and rotation survive.
        actor.BaseFirstFrame = 0;
        if (frames is int knownFrames)
        {
            actor.BaseLastFrame = knownFrames - 1;
        }
        SelectNativeBodyAnimation(actor, idle, AnimationRepeating);
        actor.NativeFollowOff = new NativeFollowOffReceipt(1, FieldId, actor.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, slot, nativeActor, idle, frames);
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
            if (receipt.Version != 1 || receipt.FieldId != saved.FieldId || receipt.ObjectId != actor.ObjectId || receipt.Frame is < 1 || receipt.Frame > saved.Frame || receipt.PartySlot is < 0 or >= 3 || receipt.AnimationFrames is null && actor.NativeAnimationHeader?.BoundModelId != -1 || receipt.AnimationFrames is < 1 || receipt.NativeActorId >= saved.Scene.NativeActorObjectIds.Count || saved.Scene.NativeActorObjectIds[receipt.NativeActorId] != actor.ObjectId || script is null || !script.ContainsOperation(receipt.Operation) || program.Operations[receipt.Operation].Opcode != Jsm.Opcode.FOLLOWOFF)
            {
                throw new InvalidDataException("Saved FOLLOWOFF receipt lost its original actor, animation or instruction provenance.");
            }
        }
    }
}
