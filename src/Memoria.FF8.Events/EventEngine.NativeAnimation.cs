using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeActorAnimationHeader([property: JsonRequired] int Version, [property: JsonRequired] byte AnimationId, [property: JsonRequired] ushort? Frame, [property: JsonRequired] ushort Increment, [property: JsonRequired] ushort First, [property: JsonRequired] ushort? Terminal, [property: JsonRequired] ushort? Flags, [property: JsonRequired] int? BoundModelId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ushort? EmptyModelTerminal = null);
public sealed record NativeActorAnimationBackup([property: JsonRequired] int Version, [property: JsonRequired] byte AnimationId, [property: JsonRequired] ushort Frame, [property: JsonRequired] ushort Increment, [property: JsonRequired] ushort First, [property: JsonRequired] ushort Terminal, [property: JsonRequired] ushort Flags);

public sealed partial class EventEngine
{
    public Func<IReadOnlyList<int>, IReadOnlyList<ushort?>>? ReadNativeEmptyAnimationTerminals { get; set; }

    private const ushort AnimationComplete = 0x0800;
    private const ushort AnimationFrozen = 0x1000;
    private const ushort AnimationRepeating = 0x2000;
    private const ushort AnimationReturnIdle = 0x4000;
    private const ushort AnimationHold = 0x8000;

    private NativeActorAnimationHeader RequireNativeAnimationHeader(ActorState actor)
    {
        return actor.NativeAnimationHeader ?? throw new NotSupportedException("Native animation requires its executed physical header allocation.");
    }

    private ushort NativeAnimationTerminal(ActorState actor, int animation)
    {
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        if (header.BoundModelId == -1 && header.EmptyModelTerminal is ushort empty)
        {
            return empty;
        }
        if (header.BoundModelId is not int model || model < 0)
        {
            throw new NotSupportedException("Native animation selection requires its actually bound model resource; an empty instance has opaque model WORD12.");
        }
        int frames = AnimationFrameCount?.Invoke(model, animation) ?? throw new NotSupportedException("Native animation selection requires actual bound-resource frame metadata.");
        if (frames <= 0)
        {
            throw new NotSupportedException("Native animation frame count lies outside the represented model WORD12 contract.");
        }
        return unchecked((ushort)(frames * 16));
    }

    private void PublishNativeAnimation(ActorState actor, bool select)
    {
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        actor.AnimationSpeed = unchecked((short)header.Increment);
        if (header.BoundModelId is null or < 0 || header.Frame is null || header.Terminal is null || header.Flags is null)
        {
            return;
        }
        if (select)
        {
            _ = NativeAnimationTerminal(actor, header.AnimationId);
            actor.AnimationId = header.AnimationId;
        }
        actor.AnimationFrame = header.Frame.Value / 16f;
        actor.AnimationFirst = header.First / 16;
        actor.AnimationLast = header.Terminal.Value == 0 ? 0 : (header.Terminal.Value - 1) / 16;
        actor.AnimationLoop = (header.Flags.Value & AnimationRepeating) != 0;
        actor.AnimationKeep = (header.Flags.Value & (AnimationHold | AnimationFrozen)) != 0;
        actor.AnimationPlaying = (header.Flags.Value & AnimationFrozen) == 0;
    }

    private void SetNativeAnimationIncrement(ActorState actor, ushort increment)
    {
        actor.NativeAnimationHeader = RequireNativeAnimationHeader(actor) with { Increment = increment };
        actor.AnimationSpeed = unchecked((short)increment);
    }

    private void SelectNativeBodyAnimation(ActorState actor, byte animation, ushort flags, ushort first = 0, ushort? terminal = null)
    {
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        ushort? end = terminal ?? (header.BoundModelId == -1 ? header.EmptyModelTerminal : NativeAnimationTerminal(actor, animation));
        actor.NativeAnimationHeader = header with { AnimationId = animation, Frame = first, First = first, Terminal = end, Flags = flags };
        PublishNativeAnimation(actor, true);
    }

    private void BindNativeAnimationResources()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        int[] boundModels = Scene.NativeActorObjectIds.Select(objectId => actors[objectId].ModelId is -1 or ushort.MaxValue ? -1 : actors[objectId].ModelId).ToArray();
        IReadOnlyList<ushort?>? emptyTerminals = boundModels.Contains(-1) ? ReadNativeEmptyAnimationTerminals?.Invoke(boundModels) : null;
        if (emptyTerminals is not null && emptyTerminals.Count != boundModels.Length)
        {
            throw new InvalidDataException("Native animation allocation returned a different physical actor order.");
        }
        int ordinal = 0;
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            if (actor.ModelId is < -1 or > 255 && actor.ModelId != ushort.MaxValue)
            {
                throw new NotSupportedException("Native model binding requires the represented original model mapping.");
            }
            int bound = actor.ModelId is -1 or ushort.MaxValue ? -1 : actor.ModelId;
            if (actor.NativeEyes is { Mode: not 0 } retainedEyes && retainedEyes.BoundModelId != bound)
            {
                throw new NotSupportedException("Native model binding cannot replace a retained eye resource without a proved backend rebind contract.");
            }
            actor.NativeAnimationHeader = RequireNativeAnimationHeader(actor) with { BoundModelId = bound, EmptyModelTerminal = bound == -1 ? emptyTerminals?[ordinal] : null };
            ordinal++;
            SelectNativeBodyAnimation(actor, unchecked((byte)actor.BaseAnimationId), AnimationRepeating);
        }
    }

    private void StepNativeAnimation(ActorState actor)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            actor.StepAnimation();
            return;
        }
        if (!defaultsStarted || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.NativePartyFlags is { AutoAnimation4: true })
        {
            return;
        }
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        if (header.BoundModelId == -1 && header.EmptyModelTerminal is null)
        {
            // An unrepresented backing WORD12 leaves the empty-body phase opaque.
            actor.NativeAnimationHeader = header with { Frame = null, Flags = null };
            return;
        }
        if (header.BoundModelId is null || header.Frame is not ushort frame || header.Terminal is not ushort terminal || header.Flags is not ushort flags)
        {
            throw new NotSupportedException("Native animation callback has no executed bound header phase.");
        }
        if ((flags & AnimationFrozen) == 0)
        {
            ushort previous = frame;
            frame = unchecked((ushort)(frame + header.Increment));
            if (HasNativeMode1(actor))
            {
                StepNativeFootstepCrossings(actor, previous, frame, terminal, header.Increment, false);
            }
        }
        if (unchecked((short)frame) < unchecked((short)terminal))
        {
            flags = (ushort)(flags & ~AnimationComplete);
        }
        else if ((flags & AnimationRepeating) != 0)
        {
            flags |= AnimationComplete;
            frame = header.First;
        }
        else if ((flags & AnimationHold) != 0)
        {
            flags = AnimationFrozen | AnimationComplete;
            terminal = frame = unchecked((ushort)(terminal - header.Increment));
        }
        else if ((flags & AnimationReturnIdle) != 0)
        {
            SelectNativeBodyAnimation(actor, unchecked((byte)actor.BaseAnimationId), AnimationRepeating | AnimationComplete);
            return;
        }
        else if ((flags & AnimationFrozen) != 0)
        {
            flags |= AnimationComplete;
        }
        else
        {
            throw new NotSupportedException("Native animation reached its original fatal unowned terminal phase.");
        }
        actor.NativeAnimationHeader = header with { Frame = frame, Terminal = terminal, Flags = flags };
        PublishNativeAnimation(actor, false);
    }

    private string? UnsupportedNativeAnimation(Fiber fiber, JsmOperation operation)
    {
        bool backupCommand = operation.Opcode is Jsm.Opcode.PUSHANIME or Jsm.Opcode.POPANIME;
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return backupCommand ? "Native animation backup requires a verified physical actor allocation" : null;
        }
        bool selection = operation.Opcode is Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RANIME or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RANIMELOOP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.RCANIMELOOP;
        if (!backupCommand && !selection && operation.Opcode is not (Jsm.Opcode.ANIMESYNC or Jsm.Opcode.ANIMESPEED or Jsm.Opcode.ANIMESTOP))
        {
            return null;
        }
        ActorState actor = actors[fiber.ObjectId];
        NativeActorAnimationHeader? header = actor.NativeAnimationHeader;
        bool opaqueWriter = header?.BoundModelId == -1 && operation.Opcode is Jsm.Opcode.RANIME or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RANIMELOOP or Jsm.Opcode.ANIMESTOP;
        if (header is null || actor.NativeAnimationBackup is null || operation.Opcode != Jsm.Opcode.ANIMESPEED && !opaqueWriter && (header.Frame is null || header.Terminal is null || header.Flags is null))
        {
            return "Native animation observation requires its actual allocated header and single backup slot, with known physical words";
        }
        try
        {
            if (operation.Opcode == Jsm.Opcode.POPANIME)
            {
                _ = NativeAnimationTerminal(actor, actor.NativeAnimationBackup.AnimationId);
            }
            else if (operation.Opcode == Jsm.Opcode.ANIMESTOP)
            {
                if (!opaqueWriter)
                {
                    _ = NativeAnimationTerminal(actor, unchecked((byte)actor.BaseAnimationId));
                }
            }
            else if (selection)
            {
                if (opaqueWriter)
                {
                    //532890 skips resource lookup for an executed531D40 empty
                    //binding; only the body BYTE selector survives.526890/526910/
                    //526A30 still write body words and yield.
                    return null;
                }
                if (unchecked((short)operation.Parameter) is < 0 or > byte.MaxValue)
                {
                    return "Native animation WORD selector outside the represented BYTE resource/current-header domain is unsupported";
                }
                ushort full = NativeAnimationTerminal(actor, unchecked((short)operation.Parameter));
                if (operation.Opcode is Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.RCANIMELOOP)
                {
                    if (fiber.Stack.Count < 2)
                    {
                        return "Native ranged animation requires both original frame operands";
                    }
                    int[] stack = fiber.Stack.ToArray();
                    short first = unchecked((short)(16 * unchecked((short)stack[0]) - 16));
                    short end = unchecked((short)(16 * unchecked((short)stack[1]) - 16));
                    if (first < 0 || end > full)
                    {
                        return "Native ranged animation reached its original invalid-range/fatal branch";
                    }
                }
            }
            else if (operation.Opcode == Jsm.Opcode.ANIMESPEED && (fiber.Stack.Count < 1 || fiber.Stack.HasUncertainty))
            {
                return "Native animation speed requires its exact original DWORD operand";
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or InvalidOperationException)
        {
            return exception.Message;
        }
        return null;
    }

    private void ExecuteNativeAnimationBackup(ActorState actor, bool restore)
    {
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        if (!restore)
        {
            actor.NativeAnimationBackup = new NativeActorAnimationBackup(1, header.AnimationId, header.Frame!.Value, header.Increment, header.First, header.Terminal!.Value, header.Flags!.Value);
            return;
        }
        NativeActorAnimationBackup saved = actor.NativeAnimationBackup!;
        _ = NativeAnimationTerminal(actor, saved.AnimationId);
        actor.NativeAnimationHeader = header with { AnimationId = saved.AnimationId, Frame = saved.Frame, Increment = saved.Increment, First = saved.First, Terminal = saved.Terminal, Flags = saved.Flags };
        PublishNativeAnimation(actor, true);
    }

    private void PlayNativeAnimation(Fiber fiber, ActorState actor, Jsm.Opcode opcode, int animation, out int[] args)
    {
        animation = unchecked((short)animation);
        NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
        ushort? full = header.BoundModelId == -1 ? header.EmptyModelTerminal : NativeAnimationTerminal(actor, animation);
        bool ranged = opcode is Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RCANIME or Jsm.Opcode.RCANIMEKEEP or Jsm.Opcode.RCANIMELOOP;
        ushort first = 0;
        ushort? terminal = full;
        args = [];
        if (ranged)
        {
            if (fiber.Stack.Count < 2)
            {
                throw new NotSupportedException("Native ranged animation requires both original frame operands.");
            }
            int[] stack = fiber.Stack.ToArray();
            first = unchecked((ushort)(16 * unchecked((short)stack[0]) - 16));
            terminal = unchecked((ushort)(16 * unchecked((short)stack[1]) - 16));
            if (full is null || unchecked((short)first) < 0 || unchecked((short)terminal!.Value) > full.Value)
            {
                throw new NotSupportedException("Native ranged animation reached its original invalid-range/fatal branch.");
            }
            args = Pop(fiber, 2);
        }
        ushort flags = opcode is Jsm.Opcode.RANIMELOOP or Jsm.Opcode.RCANIMELOOP ? AnimationRepeating : opcode is Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIMEKEEP or Jsm.Opcode.RANIMEKEEP or Jsm.Opcode.RCANIMEKEEP ? AnimationHold : AnimationReturnIdle;
        SelectNativeBodyAnimation(actor, unchecked((byte)animation), flags, first, terminal);
        if (opcode is Jsm.Opcode.ANIME or Jsm.Opcode.ANIMEKEEP or Jsm.Opcode.CANIME or Jsm.Opcode.CANIMEKEEP)
        {
            fiber.WaitReason = WaitReason.NativeAnimationBlocking;
        }
        else
        {
            WaitFrames(fiber, 1);
        }
    }

    private void ValidateNativeAnimationCheckpoint(Checkpoint saved)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        foreach (int objectId in program!.NativeObjects.Take(program.NativeActorCount))
        {
            ActorState actor = saved.Actors[objectId];
            NativeActorAnimationHeader header = actor.NativeAnimationHeader ?? throw new InvalidDataException("Saved physical actor lost its allocated native animation header.");
            NativeActorAnimationBackup backup = actor.NativeAnimationBackup ?? throw new InvalidDataException("Saved physical actor lost its allocated single animation backup slot.");
            bool empty = header.BoundModelId == -1;
            NativeFieldModelInstance? registered = saved.Scene.ModelParts?.Instances.SingleOrDefault(instance => instance.ObjectId == objectId);
            if (header.Version != 1 || backup.Version != 1 || header.BoundModelId is < -1 or > 255 || !empty && header.EmptyModelTerminal is not null || header.Flags is ushort flags && (flags & 0x07FF) != 0 || (backup.Flags & 0x07FF) != 0 || !empty && (header.Frame is null || header.Terminal is null || header.Flags is null) || saved.DefaultsStarted && saved.Scene.NativeActorObjectIds.Contains(objectId) && header.BoundModelId is null || actor.AnimationSpeed != unchecked((short)header.Increment))
            {
                throw new InvalidDataException("Saved native animation header/backup or speed mirror has no represented physical owner.");
            }
            if (saved.Scene.ModelParts is not null && saved.DefaultsStarted && saved.Scene.NativeActorObjectIds.Contains(objectId) && (registered?.ModelId ?? -1) != header.BoundModelId)
            {
                throw new InvalidDataException("Saved native animation resource differs from its independently retained renderer-instance registration.");
            }
        }
    }
}
