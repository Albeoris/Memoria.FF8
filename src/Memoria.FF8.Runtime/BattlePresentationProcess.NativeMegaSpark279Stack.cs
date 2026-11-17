using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
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
        if (task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && task.Payload.Length == 32 && (task.UnknownByteMask & 0x200) == 0 && task.Program is { } program && task.ModelOwner is { } actor && graphics.ActorModels.TryGet(actor, out NativeBattleGraphicsModel? model))
        {
            program.Validate();
            if (program.Source[program.Offset] == 255 && (model.Flags0 & 2) != 0 && model.ResourceFlags44 is ushort flags && (flags & 0x40) == 0 && model.Sequence == task.Payload[9])
            {
                return;
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

    private void ObserveNativeMegaSpark279UiStack(NativeBattleUiWindows windows, bool normalPreCore)
    {
        if (nativeMegaSpark279Stack is not { } stack || !normalPreCore ||
            NativeGraphics?.OriginalEffect?.Binding.MegaSpark279Resources is null || services is not { } host)
        {
            return;
        }
        windows.Validate();
        var (presentation, guardian) = host.ReadStackVitals();
        foreach (NativeBattleUiWindow window in windows.Rows)
        {
            if (window.Update0 == 0)
            {
                continue;
            }
            stack = window.Update0 == 0x4B0500
                ? NativeBattleMegaSpark279Stack.ObserveVitals(stack, presentation, guardian, NativeEffectMemory?.ExecutionStack)
                : stack.Invalidate(-0xAC, 8, window.Update0, "UI window callback");
        }
        nativeMegaSpark279Stack = stack;
    }
}
