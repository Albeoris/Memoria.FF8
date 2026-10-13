using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeActorShadowSet(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawSize, byte Radius601To608);
public sealed record NativeActorShadowForm(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int[] RawSizes, IReadOnlyList<byte> Radii601To608);
public sealed record NativeActorShadowLevel(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawLevel, byte Level609);
public sealed record NativeActorShadowAllocation(int Version, int FieldId, int ObjectId, string ProgramHash, string ExecutableHash, byte Level609);

public sealed partial class EventEngine
{
    private string? UnsupportedActorShadow(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.SHADESET or Jsm.Opcode.SHADEFORM))
        {
            return null;
        }
        int count = operation.Opcode == Jsm.Opcode.SHADEFORM ? 8 : 1;
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Stack.Count < count)
        {
            return $"Native {operation.Opcode} requires its authenticated actor-radius contract and {count} original operands";
        }
        ActorState actor = actors[fiber.ObjectId];
        return fiber.Stack.Take(count).Any(value => unchecked((byte)(value / 4)) != 0) && actor.NativeShadowLevel is null && actor.NativeShadowAllocation is null ? $"Nonzero native {operation.Opcode} requires executed or authenticated allocated BYTE609 authority; original PC and operands preserved" : null;
    }

    private int[] ExecuteActorShadowSet(Fiber fiber, ActorState actor)
    {
        int[] args = Pop(fiber, 1);
        // 526CD0 divides the signed DWORD with truncation toward zero, then
        // stores its low BYTE independently to all eight actor bytes601..608.
        actor.NativeShadowSet = new NativeActorShadowSet(1, FieldId, actor.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], unchecked((byte)(args[0] / 4)));
        actor.NativeShadowForm = null;
        return args;
    }

    private int[] ExecuteActorShadowForm(Fiber fiber, ActorState actor)
    {
        int[] args = Pop(fiber, 8);
        // 526D30 pops in byte order604,603,602,601,608,607,606,605.
        // Pop returns chronological pushes, hence byte601 reads args[4].
        byte[] radii = new byte[8];
        for (int index = 0; index < radii.Length; index++)
        {
            radii[index] = unchecked((byte)(args[(index + 4) & 7] / 4));
        }
        actor.NativeShadowForm = new NativeActorShadowForm(1, FieldId, actor.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args, radii);
        actor.NativeShadowSet = null;
        return args;
    }

    private int[] ExecuteActorShadowLevel(Fiber fiber, ActorState actor)
    {
        int[] args = Pop(fiber, 1);
        byte level = unchecked((byte)args[0]);
        actor.ShadeLevel = level;
        actor.NativeShadowLevel = new NativeActorShadowLevel(1, FieldId, actor.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], level);
        return args;
    }

    private void ValidateActorShadowCheckpoint(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeShadowAllocation is NativeActorShadowAllocation allocation && (allocation.Version != 1 || allocation.FieldId != saved.FieldId || allocation.ObjectId != actor.ObjectId || allocation.Level609 != 96 || allocation.ProgramHash != saved.ProgramHash || allocation.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) != true || !program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId) || actor.NativeShadowLevel is null && actor.ShadeLevel != 96))
            {
                throw new InvalidDataException("Saved actor shadow level lacks its authenticated original52BC00 allocation.");
            }
            if (actor.NativeShadowSet is NativeActorShadowSet set)
            {
                if (set.Version != 1 || set.ObjectId != actor.ObjectId || set.Radius601To608 != unchecked((byte)(set.RawSize / 4)) || set.Radius601To608 != 0 && actor.NativeShadowLevel is null && actor.NativeShadowAllocation is null || set.FieldId != saved.FieldId || set.Frame < 0 || set.Frame > saved.Frame)
                {
                    throw new InvalidDataException("Saved native actor shadow lacks its executed radius-byte or shade-level authority.");
                }
                ValidateActorShadowSite(actor, set.ScriptId, set.Operation, Jsm.Opcode.SHADESET);
            }
            if (actor.NativeShadowForm is NativeActorShadowForm form)
            {
                if (NativeRuntimeProfile is not { IsVerified: true } || form.Version != 1 || form.ObjectId != actor.ObjectId || actor.NativeShadowSet is not null || form.RawSizes is not { Length: 8 } || form.Radii601To608 is not { Count: 8 } || form.FieldId != saved.FieldId || form.Frame < 0 || form.Frame > saved.Frame)
                {
                    throw new InvalidDataException("Saved native actor shadow form lacks its authenticated eight-byte contract.");
                }
                for (int index = 0; index < form.Radii601To608.Count; index++)
                {
                    if (form.Radii601To608[index] != unchecked((byte)(form.RawSizes[(index + 4) & 7] / 4)))
                    {
                        throw new InvalidDataException("Saved native actor shadow form disagrees with its original signed division and BYTE order.");
                    }
                }
                if (form.Radii601To608.Any(radius => radius != 0) && actor.NativeShadowLevel is null && actor.NativeShadowAllocation is null)
                {
                    throw new InvalidDataException("Saved nonzero native actor shadow form lacks shade-level authority.");
                }
                ValidateActorShadowSite(actor, form.ScriptId, form.Operation, Jsm.Opcode.SHADEFORM);
            }
            if (actor.NativeShadowLevel is NativeActorShadowLevel level)
            {
                if (level.Version != 1 || level.ObjectId != actor.ObjectId || level.Level609 != unchecked((byte)level.RawLevel) || actor.ShadeLevel != level.Level609 || level.FieldId != saved.FieldId || level.Frame < 0 || level.Frame > saved.Frame)
                {
                    throw new InvalidDataException("Saved native actor shadow level disagrees with its executed BYTE609 write.");
                }
                ValidateActorShadowSite(actor, level.ScriptId, level.Operation, Jsm.Opcode.SHADELEVEL);
            }
        }
    }

    private void ValidateActorShadowSite(ActorState actor, int scriptId, int operation, Jsm.Opcode opcode)
    {
        EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == actor.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == scriptId);
        if (script is null || operation < script.Start || operation >= script.End || program.Operations[operation].Opcode != opcode)
        {
            throw new InvalidDataException("Saved actor-shadow request does not match its original instruction.");
        }
    }
}
