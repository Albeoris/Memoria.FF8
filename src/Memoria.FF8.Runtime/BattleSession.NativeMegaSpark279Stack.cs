using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleStackBytes? nativeMegaSpark279Stack;

    private void BeginNativeMegaSpark279Stack(long callback)
    {
        nativeMegaSpark279Stack = NativeBattleStackBytes.Unknown($"mega-spark279:common47CF60:{callback}", -0xAC, 8);
    }

    private void EndNativeMegaSpark279Stack()
    {
        nativeMegaSpark279Stack = null;
    }

    private void InvalidateNativeMegaSpark279Stack(uint callback, string phase)
    {
        if (nativeMegaSpark279Stack is { } stack)
        {
            nativeMegaSpark279Stack = stack.Invalidate(-0xAC, 8, callback, phase);
        }
    }

    private short ReadNativeMegaSpark279RowPointW(int outerPhase)
    {
        NativeBattleStackBytes stack = nativeMegaSpark279Stack ?? throw new NotSupportedException("Mega Spark requires its current host-frame stack observations.");
        int offset = outerPhase switch
        {
            0 or 2 or 3 or 4 => -0xA6,
            1 => -0xAA,
            _ => throw new NotSupportedException("Mega Spark row sampling requires its actual outer callback phase.")
        };
        return unchecked((short)stack.ReadUInt16(offset));
    }

    private void ObserveNativeMegaSpark279UiStack(NativeBattleUiWindows windows, bool normalPreCore)
    {
        if (nativeMegaSpark279Stack is not { } stack || !normalPreCore)
        {
            return;
        }
        windows.Validate();
        foreach (NativeBattleUiWindow window in windows.Rows)
        {
            if (window.Update0 == 0)
            {
                continue;
            }
            if (window.Update0 != 0x4B0500 || nativePartyPresentation is not { } presentation)
            {
                stack = stack.Invalidate(-0xAC, 8, window.Update0, "UI window callback");
                continue;
            }
            presentation.Validate();
            bool[] guardian = new bool[3];
            bool supported = true;
            for (int index = 0; index < 3; index++)
            {
                if (presentation.Rows[index] is null)
                {
                    continue;
                }
                BattleCombatant? actor = combatants.SingleOrDefault(actor => actor.IsPlayer && actor.NativeIndex == index);
                if (actor is null)
                {
                    supported = false;
                    break;
                }
                guardian[index] = guardianForceCasts.ContainsKey(actor.Id);
            }
            stack = supported ? NativeBattleMegaSpark279Stack.ObserveVitals(stack, presentation, guardian, NativeEffectMemory?.ExecutionStack) : stack.Invalidate(-0xAC, 8, 0x4B0500, "Missing current party UI row");
        }
        nativeMegaSpark279Stack = stack;
    }

    private void ObserveNativeMegaSpark279DescriptorStack(NativeBattleGraphicsState graphics, NativeBattleDescriptorState state, int slot)
    {
        if (nativeMegaSpark279Stack is null)
        {
            return;
        }
        NativeBattleDescriptorTask task = state.Tasks[slot];
        if (task.Kind == NativeBattleDescriptorTaskKind.Pump500CC0)
        {
            state.Validate();
            byte priority = state.Priorities[state.Depth];
            foreach (int packet in state.Queue)
            {
                byte command = state.Packets[packet].PriorityState;
                if (command > priority && (command & 15) is 0 or 15)
                {
                    InvalidateNativeMegaSpark279Stack(0x500CC0, "Descriptor dispatch or completion");
                    return;
                }
            }
            return;
        }
        if (task.Kind == NativeBattleDescriptorTaskKind.Action104 && graphics.ActionGraphics?.Callback == 0x50A9A0 && task.Phase == 6 && NativeEffectMemory?.ReturnedTaskPointer1D96AAC > 0)
        {
            return;
        }
        InvalidateNativeMegaSpark279Stack(0x508434, $"Descriptor callback {task.Kind}");
    }

    private void ObserveNativeMegaSpark279AuxiliaryStack(NativeBattleGraphicsState graphics, NativeBattleAuxiliaryTask task)
    {
        if (nativeMegaSpark279Stack is null)
        {
            return;
        }
        if (task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && task.Payload.Length == 32 && (task.UnknownByteMask & 0x20F) == 0 && task.Program is { } program)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan());
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 == 0 && delta / 156 < (uint)graphics.Models.Length)
            {
                var model = graphics.Models[(int)(delta / 156)];
                program.Validate();
                if (program.Source[program.Offset] == 255 && (model.Flags0 & 2) != 0 && model.ResourceFlags44 is ushort flags && (flags & 0x40) == 0 && model.Sequence == task.Payload[9])
                {
                    return;
                }
            }
        }
        InvalidateNativeMegaSpark279Stack(0x508434, $"Auxiliary callback {task.Kind}");
    }

    private void ObserveNativeMegaSpark279ModuleStack(NativeBattleModuleTask task)
    {
        if (task.Kind == NativeBattleModuleKind.CriticalFlash571300 && task.Callback is null)
        {
            return;
        }
        InvalidateNativeMegaSpark279Stack(task.Callback ?? 0x508434, "Shared module callback");
    }
}


public static class NativeBattleMegaSpark279Stack
{
    public static NativeBattleStackBytes ObserveVitals(NativeBattleStackBytes stack, NativeBattlePartyPresentation presentation, IReadOnlyList<bool> guardianActive, NativeBattleExecutionStack? execution = null)
    {
        stack.Validate();
        presentation.Validate();
        if (stack.FirstOffset != -0xAC || stack.Bytes.Length != 8 || guardianActive.Count != 3)
        {
            throw new InvalidDataException("Mega Spark UI stack observation requires its actual eight-byte common-frame interval and three party flags.");
        }
        uint edi = 0;
        byte ediKnown = 12;
        for (int index = 0; index < 3; index++)
        {
            if (presentation.Rows[index] is not { } row)
            {
                if (guardianActive[index])
                {
                    throw new InvalidDataException("Mega Spark UI observation selected a GF panel without its current party owner.");
                }
                continue;
            }
            string phase = $"Vitals party row {index}";
            stack = stack.StoreRegister32(-0xA8, edi, ediKnown, 0x4B0833, phase);
            stack = stack.StoreUInt32(-0xAC, 0x60, 0x4B0853, phase);
            uint ebp = checked(0x1D76971u + (uint)(index * 0x6C));
            Track(row.DisplayedHitPointsQ12, row.HitPoints, ebp - 0x23, phase);
            if (guardianActive[index])
            {
                if (row.DisplayedGuardianForceHitPointsQ12 is not int displayed || row.GuardianForceHitPoints is not ushort hitPoints)
                {
                    stack = stack.Invalidate(-0xAC, 8, 0x4B0500, "Missing active GF UI cache");
                    ediKnown = 0;
                    continue;
                }
                Track(displayed, hitPoints, ebp - 0xF, phase);
            }
            edi = guardianActive[index] ? ebp + 5 : ebp - 0xF;
            ediKnown = 15;
        }
        return stack;

        void Track(int displayed, ushort hitPoints, uint record, string phase)
        {
            int target = unchecked((short)hitPoints) << 12;
            if (displayed < target)
            {
                stack = stack.StoreRegister32(-0xA8, record, 15, 0x4B884A, phase);
            }
            else if (displayed > target)
            {
                stack = stack.StoreUInt32(-0xA8, 3, 0x4B07E8, phase);
                if (execution is not null)
                {
                    stack = stack.StoreRegister32(-0xAC, execution.ReadCommonFrame().AddressAtOffset(-0x8F), 15, 0x4B07EA, phase);
                }
                else
                {
                    stack = stack.StoreRegister32(-0xAC, 0, 0, 0x4B07EA, phase);
                }
            }
        }
    }
}
