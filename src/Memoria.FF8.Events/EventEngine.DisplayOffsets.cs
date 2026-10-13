using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeDisplayOffsetCommand(int Version, int ObjectId, int ScriptId, int Operation, long Frame, string Opcode, int InlineParameter, ImmutableArray<int> Operands, short PreviousTargetX, short PreviousTargetY, short PreviousTargetZ);
public sealed record NativeDisplayOffsetState(int Version, short X, short Y, short Z, short StartX, short StartY, short StartZ, short TargetX, short TargetY, short TargetZ, ushort Duration, ushort Counter, byte Mode, short DirectParameter = 0, short Auxiliary = 0, NativeDisplayOffsetCommand? Command = null)
{
    public static NativeDisplayOffsetState AllocatedZero { get; } = new(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed partial class EventEngine
{
    private string? UnsupportedDisplayOffset(Fiber fiber, Jsm.Opcode opcode)
    {
        int count = opcode == Jsm.Opcode.LOFFSETS ? 7 : opcode is Jsm.Opcode.COFFSET or Jsm.Opcode.LOFFSET ? 4 : opcode == Jsm.Opcode.DOFFSET ? 3 : 0;
        if (fiber.Stack.Count < count)
        {
            return "Native display offset requires its complete original operands";
        }
        ActorState actor = actors[fiber.ObjectId];
        if (program is null || !program.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId))
        {
            return "Native display offset requires an original allocated actor record";
        }
        if (opcode != Jsm.Opcode.DOFFSET && actor.NativeDisplayOffset is null)
        {
            return "Native display offset requires known allocated target words; migrate or replay a fresh native field load";
        }
        return null;
    }

    private int[] ExecuteDisplayOffset(Fiber fiber, ActorState actor, JsmOperation operation)
    {
        bool explicitStart = operation.Opcode == Jsm.Opcode.LOFFSETS;
        int[] operands = Pop(fiber, operation.Opcode == Jsm.Opcode.DOFFSET ? 3 : explicitStart ? 7 : 4);
        short x = unchecked((short)operands[0]);
        short y = unchecked((short)operands[1]);
        short z = unchecked((short)operands[2]);
        NativeDisplayOffsetState? state = actor.NativeDisplayOffset;
        if (state is null)
        {
            // A direct write establishes current XYZ, not previously unknown
            // target words. Legacy saves remain unable to request interpolation.
            actor.DisplayOffset = new Vector3(x, y, z);
            return operands;
        }
        NativeDisplayOffsetCommand command = new(1, actor.ObjectId, fiber.Script.Id, operation.Index, Frame, operation.Opcode.ToString(), operation.Parameter, operands.ToImmutableArray(), state.TargetX, state.TargetY, state.TargetZ);
        if (operation.Opcode == Jsm.Opcode.DOFFSET)
        {
            state = state with { X = x, Y = y, Z = z, StartX = x, StartY = y, StartZ = z, Mode = 0, Counter = 0, DirectParameter = unchecked((short)operation.Parameter), Auxiliary = 0, Command = command };
            actor.DisplayOffset = new Vector3(x, y, z);
        }
        else
        {
            state = state with { StartX = explicitStart ? x : state.TargetX, StartY = explicitStart ? y : state.TargetY, StartZ = explicitStart ? z : state.TargetZ, TargetX = explicitStart ? unchecked((short)operands[3]) : x, TargetY = explicitStart ? unchecked((short)operands[4]) : y, TargetZ = explicitStart ? unchecked((short)operands[5]) : z, Duration = unchecked((ushort)operands[explicitStart ? 6 : 3]), Counter = 0, Mode = operation.Opcode is Jsm.Opcode.LOFFSET or Jsm.Opcode.LOFFSETS ? (byte)1 : (byte)2, Command = command };
        }
        actor.NativeDisplayOffset = state;
        return operands;
    }

    private void ExecuteDisplayOffsetSync(Fiber fiber)
    {
        if (actors[fiber.ObjectId].NativeDisplayOffset!.Mode != 3)
        {
            fiber.Position--;
            fiber.WaitReason = WaitReason.Frames;
            fiber.WaitUntil = Frame + 1;
        }
    }

    private void StepNativeDisplayOffsets()
    {
        foreach (ActorState actor in actors)
        {
            if (actor.NativeDisplayOffset is not { Mode: 1 or 2 } state || Scene.NativeActorLayoutVersion == 1 && !Scene.NativeActorObjectIds.Contains(actor.ObjectId))
            {
                continue;
            }
            short x = InterpolateNativeOffset(state.StartX, state.TargetX, state.Duration, state.Counter, state.Mode == 2);
            short y = InterpolateNativeOffset(state.StartY, state.TargetY, state.Duration, state.Counter, state.Mode == 2);
            short z = InterpolateNativeOffset(state.StartZ, state.TargetZ, state.Duration, state.Counter, state.Mode == 2);
            bool complete = state.Counter == state.Duration;
            actor.NativeDisplayOffset = state with { X = x, Y = y, Z = z, Mode = complete ? (byte)3 : state.Mode, Counter = complete ? state.Counter : unchecked((ushort)(state.Counter + 1)) };
            actor.DisplayOffset = new Vector3(x, y, z);
        }
    }

    private static short InterpolateNativeOffset(short start, short target, ushort duration, int elapsed, bool cosine)
    {
        // Native475C60's small-delta branch always applies to signed-WORD
        // endpoints. Its elapsed*delta product remains wrapped int32.
        int value = cosine ? NativeCosineInterpolation.Interpolate(start, target, duration, elapsed) : duration == 0 ? start : unchecked(start + unchecked(elapsed * (target - start)) / duration);
        return unchecked((short)value);
    }

    private void ValidateDisplayOffsetCheckpoint(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeDisplayOffset is not NativeDisplayOffsetState state)
            {
                continue;
            }
            if (state.Version != 1 || !program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId) || state.Mode > 3 || state.Counter > state.Duration || state.Mode == 0 && state.Counter != 0 || state.Mode == 3 && state.Counter != state.Duration || actor.DisplayOffset != new Vector3(state.X, state.Y, state.Z) || saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) != true)
            {
                throw new InvalidDataException("Saved display offset has inconsistent native words, mode, mirror or allocation provenance.");
            }
            if (state.Command is not NativeDisplayOffsetCommand command)
            {
                if (state != NativeDisplayOffsetState.AllocatedZero)
                {
                    throw new InvalidDataException("Saved nonzero display offset has no executed command.");
                }
                continue;
            }
            bool direct = command.Opcode == nameof(Jsm.Opcode.DOFFSET);
            bool cosine = command.Opcode == nameof(Jsm.Opcode.COFFSET);
            bool explicitStart = command.Opcode == nameof(Jsm.Opcode.LOFFSETS);
            if (command.Version != 1 || command.ObjectId != actor.ObjectId || command.ScriptId < 0 || command.Operation < 0 || command.Frame < 0 || command.Frame > saved.Frame || command.Operands.IsDefault || command.Operands.Length != (direct ? 3 : explicitStart ? 7 : 4) || !direct && !cosine && !explicitStart && command.Opcode != nameof(Jsm.Opcode.LOFFSET) || !MatchesNativeMediaInstruction(command.ObjectId, command.ScriptId, command.Operation) || program!.Operations[command.Operation].Opcode.ToString() != command.Opcode || program.Operations[command.Operation].Parameter != command.InlineParameter)
            {
                throw new InvalidDataException("Saved display offset has invalid issuing command provenance.");
            }
            short x = unchecked((short)command.Operands[0]);
            short y = unchecked((short)command.Operands[1]);
            short z = unchecked((short)command.Operands[2]);
            if (direct)
            {
                if (state.Mode != 0 || state.X != x || state.Y != y || state.Z != z || state.StartX != x || state.StartY != y || state.StartZ != z || state.TargetX != command.PreviousTargetX || state.TargetY != command.PreviousTargetY || state.TargetZ != command.PreviousTargetZ || state.DirectParameter != unchecked((short)command.InlineParameter) || state.Auxiliary != 0)
                {
                    throw new InvalidDataException("Saved direct offset differs from its native current/start writes or retained targets.");
                }
            }
            else
            {
                short targetX = explicitStart ? unchecked((short)command.Operands[3]) : x;
                short targetY = explicitStart ? unchecked((short)command.Operands[4]) : y;
                short targetZ = explicitStart ? unchecked((short)command.Operands[5]) : z;
                if (state.Mode != (cosine ? 2 : 1) && state.Mode != 3 || state.StartX != (explicitStart ? x : command.PreviousTargetX) || state.StartY != (explicitStart ? y : command.PreviousTargetY) || state.StartZ != (explicitStart ? z : command.PreviousTargetZ) || state.TargetX != targetX || state.TargetY != targetY || state.TargetZ != targetZ || state.Duration != unchecked((ushort)command.Operands[explicitStart ? 6 : 3]))
                {
                    throw new InvalidDataException("Saved display offset differs from its native start/target and duration writes.");
                }
                if (state.Mode == 3 || state.Counter != 0)
                {
                    int elapsed = state.Mode == 3 ? state.Counter : state.Counter - 1;
                    if (state.X != InterpolateNativeOffset(state.StartX, state.TargetX, state.Duration, elapsed, cosine) || state.Y != InterpolateNativeOffset(state.StartY, state.TargetY, state.Duration, elapsed, cosine) || state.Z != InterpolateNativeOffset(state.StartZ, state.TargetZ, state.Duration, elapsed, cosine))
                    {
                        throw new InvalidDataException("Saved cosine offset current words differ from its retained native phase.");
                    }
                }
            }
        }
    }
}
