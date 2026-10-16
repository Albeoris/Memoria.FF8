using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool IsKnownNativeByteAlias(int address, int value)
    {
        bool party = address is >= 194 and <= 196 && (Scene.NativeActorLayoutVersion == 1 && Scene.FieldPartyNativeActorIds.Count == 3 && Scene.FieldPartyNativeActorIds[address - 194] == value || Scene.PartyBindingMemory is { Version: 1 } memory && memory.At(address - 194) == value);
        return party || address == 86 && Scene.PreviousWorldEntityByte86 is byte entity && value == entity || TryReadSoundVolumeByte(address, out int volume) && volume == value || address is 199 or 200 && ReadMusicChannel(address - 199)?.MusicId == value || TryReadNativeMusicByte(address, out int music) && music == value || address == 204 && services.DiscProcess?.SavedDisc204 == value;
    }

    private static int NativeMemoryWidth(Jsm.Opcode opcode)
    {
        return opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
    }

    private string? UnsupportedNativeScalarAccess(Fiber fiber, JsmOperation operation)
    {
        if (UnsupportedNativeDisc(fiber, operation) is string disc)
        {
            return disc;
        }
        if (operation.Opcode == Jsm.Opcode.DISC)
        {
            return null;
        }
        if (operation.Opcode == Jsm.Opcode.SETDCAMERA)
        {
            return UnsupportedMovieMask(fiber, operation);
        }
        if (UnsupportedWorldEntryMemory(fiber, operation) is string worldEntry)
        {
            return worldEntry;
        }
        if (operation.Opcode == Jsm.Opcode.SETVIBRATE)
        {
            return UnsupportedVibrationRequest(fiber, operation);
        }
        if (UnsupportedNativeEventRandom(fiber, operation) is string random)
        {
            return random;
        }
        if (operation.Opcode is Jsm.Opcode.MENUPHS or Jsm.Opcode.MENUNORMAL or Jsm.Opcode.MENUSAVE)
        {
            // Native521D10/521D40 read no operand stack slots.
            return null;
        }
        if (operation.Opcode == Jsm.Opcode.MENUSHOP)
        {
            // Native521D60 pops one DWORD; the shop request preflight validates its exact value.
            return null;
        }
        if (operation.Opcode == Jsm.Opcode.HOLD)
        {
            return UnsupportedNativeHold(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.CAL)
        {
            try
            {
                _ = PrepareNativeCalculation(fiber, operation.Parameter);
                return null;
            }
            catch (Exception exception) when (exception is NotSupportedException or ArithmeticException or InvalidDataException)
            {
                return exception.Message;
            }
        }
        if (operation.Opcode is Jsm.Opcode.SHAKE or Jsm.Opcode.SHAKEOFF)
        {
            return UnsupportedShake(fiber, operation);
        }
        if (operation.Opcode is Jsm.Opcode.FACEDIRI or Jsm.Opcode.RFACEDIRI)
        {
            return UnsupportedNativeFaceImmediate(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.FACEDIR or Jsm.Opcode.RFACEDIR)
        {
            return UnsupportedNativeFacePoint(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.FACEDIRP or Jsm.Opcode.RFACEDIRP)
        {
            return UnsupportedNativePartyFace(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.FACEDIRA or Jsm.Opcode.RFACEDIRA)
        {
            return UnsupportedNativeActorFace(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.FACEDIROFF or Jsm.Opcode.RFACEDIROFF)
        {
            return UnsupportedNativeFaceReturn(fiber);
        }
        if (operation.Opcode is Jsm.Opcode.PARTICLEON or Jsm.Opcode.PARTICLEOFF or Jsm.Opcode.PARTICLESET)
        {
            return UnsupportedParticle(fiber, operation.Opcode);
        }
        if (operation.Opcode is Jsm.Opcode.SEPOS or Jsm.Opcode.SEPOSTRANS)
        {
            return UnsupportedSoundPosition(fiber, operation);
        }
        if (operation.Opcode == Jsm.Opcode.MESVAR)
        {
            return UnsupportedMessageVariable(fiber, operation);
        }
        int width = NativeMemoryWidth(operation.Opcode);
        bool writing = operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L;
        if (width > 0)
        {
            try
            {
                NativeEventMemoryOwnership.Validate(EventMemory, MemoryRegions());
                NativeEventMemoryOwner owner = ResolveMemoryOwner(operation.Parameter, width);
                if (writing && owner == NativeEventMemoryOwner.Generic && fiber.Stack.Count > 0)
                {
                    NativeScalarValue value = fiber.Stack.PeekValue();
                    if (value.ActorInfoOrigin is not null)
                    {
                        return "Native event memory cannot store an out-of-table actor-info result.";
                    }
                    _ = EventMemory.Write(operation.Parameter, width, value);
                }
                else if (!writing)
                {
                    _ = ReadEventMemory(operation);
                }
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArgumentOutOfRangeException)
            {
                return exception.Message;
            }
        }
        if (!fiber.Stack.HasUncertainty || operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.POPI_L or Jsm.Opcode.LBL or Jsm.Opcode.NOP or Jsm.Opcode.MOVEFLUSH or Jsm.Opcode.MUSICVOLSYNC or Jsm.Opcode.MUSICSTATUS or Jsm.Opcode.FACEDIRSYNC or Jsm.Opcode.FADEIN or Jsm.Opcode.FADEOUT or Jsm.Opcode.FADENONE or Jsm.Opcode.FADEBLACK or Jsm.Opcode.FADESYNC or Jsm.Opcode.COLSYNC or Jsm.Opcode.MAPFADEOFF or Jsm.Opcode.MAPFADEON or Jsm.Opcode.RND || writing)
        {
            return null;
        }
        return "This command requires an exact native integer; uncertain operands are preserved without choosing a representative";
    }

    private NativeScalarValue PrepareNativeCalculation(Fiber fiber, int kind)
    {
        int count = NativeScalarCalculation.OperandCount(kind);
        if (fiber.Stack.Count < count)
        {
            throw new NotSupportedException($"Native CAL requires {count} original operands; its stack is preserved.");
        }
        NativeScalarValue[] operands = fiber.Stack.Capture();
        return NativeScalarCalculation.Resolve(kind, count == 2 ? operands[1] : null, operands[0]);
    }

    private NativeScalarValue PopNativeScalar(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            throw Fault(fiber, "Operand stack underflow: need one native scalar.");
        }
        NativeScalarValue value = fiber.Stack.PopValue();
        fiber.LastExpressions = [fiber.StackExpressions.Pop()];
        return value;
    }

    private NativeScalarValue StoreNativeScalar(Fiber fiber, JsmOperation operation)
    {
        NativeScalarValue raw = fiber.Stack.PeekValue();
        WriteGenericEventMemory(operation.Parameter, NativeMemoryWidth(operation.Opcode), raw);
        PopNativeScalar(fiber);
        return raw;
    }

    private void ValidateNativeScalarSource(NativeScalarValue value, int fieldId, string sourceHash, long frame, NativeFieldInitializationProof? initialization, NativeExamTimerState? timer, SceneEventState scene)
    {
        value.Validate();
        if (value.ActorInfoOrigin is NativeActorInfoReadOrigin actorRead)
        {
            EventScript? actorScript = (uint)actorRead.ObjectId < program!.Objects.Count ? program.Objects[actorRead.ObjectId].Scripts.FirstOrDefault(script => script.Id == actorRead.ScriptId) : null;
            if (actorRead.FieldId != fieldId || actorRead.ProgramHash != sourceHash || actorRead.RetainedActorCount != scene.NativeActorObjectIds.Count || actorRead.Frame > frame || actorScript is null || actorRead.Operation < actorScript.Start || actorRead.Operation >= actorScript.End || program.Operations[actorRead.Operation].Opcode != Jsm.Opcode.PGETINFO)
            {
                throw new InvalidDataException("Unknown actor-info scalar source does not identify its executed PGETINFO operation.");
            }
            return;
        }
        if (value.Origin is not { } origin || origin.FieldId != fieldId)
        {
            return;
        }
        EventScript? script = (uint)origin.ObjectId < program!.Objects.Count ? program.Objects[origin.ObjectId].Scripts.FirstOrDefault(script => script.Id == origin.ScriptId) : null;
        // Existing timers advance before this load's first VM turn. A read at or
        // before the captured allocation clock belongs to an earlier load, whose
        // local frame must not be compared with the reset current field frame.
        bool earlierLoad = initialization?.TimerClockCallsAtAllocation is long allocationClock && origin.Timer.ClockCalls <= allocationClock;
        if (origin.ProgramHash != sourceHash || !earlierLoad && origin.Frame > frame || timer is not null && origin.Timer.ClockCalls > timer.ClockCalls || script is null || origin.Operation < script.Start || origin.Operation >= script.End || program.Operations[origin.Operation].Opcode != Jsm.Opcode.GETTIMER)
        {
            throw new InvalidDataException("Native scalar source does not identify its original executed GETTIMER operation.");
        }
    }
}
