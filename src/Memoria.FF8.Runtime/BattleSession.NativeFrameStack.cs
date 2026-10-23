using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleStackBytes? nativePreRootStack;
    private bool nativePreCoreUiStack;

    private void BeginNativeFrameStack()
    {
        if (nativePreRootStack is not null)
        {
            throw new InvalidOperationException("Native execution stack observations require one nonnested host frame.");
        }
        nativePreRootStack = NativeBattlePreRootStackObserver.BeginHostFrame(NativeHostCallbacks ?? NativeGraphics!.Callback);
        BeginNativeMegaSpark279Stack(NativeHostCallbacks ?? NativeGraphics!.Callback);
        nativePreCoreUiStack = true;
    }

    private void EndNativeFrameStack()
    {
        EndNativeMegaSpark279Stack();
        nativePreRootStack = null;
        nativePreCoreUiStack = false;
    }

    private (NativeBattleStackBytes Bytes, int Offset) ReadNativeCurrentStackOperand()
    {
        if (NativeGraphics is not { EntryLifecycle: 4 })
        {
            throw new NotSupportedException("The current stack operand requires the proved normal outer core frame.");
        }
        return (nativePreRootStack ?? throw new NotSupportedException("The current native callback has no host-frame stack observation."), NativeBattlePreRootStackObserver.CommonWordOffset);
    }

    private void PublishNativeCurrentStackOperand(NativeBattleStackBytes stack)
    {
        (NativeBattleStackBytes current, int offset) = ReadNativeCurrentStackOperand();
        stack.Validate();
        if (stack.FrameOwner != current.FrameOwner || stack.FirstOffset != offset || stack.Bytes.Length != NativeBattlePreRootStackObserver.CommonIntervalLength)
        {
            throw new InvalidDataException("Native callback attempted to replace a different physical execution-frame owner.");
        }
        nativePreRootStack = stack;
    }

    private void ObserveNativeUnknownStackCallback(uint callback, string phase)
    {
        InvalidateNativeMegaSpark279Stack(callback, phase);
        if (nativePreRootStack is { } stack)
        {
            nativePreRootStack = NativeBattlePreRootStackObserver.ObserveUnknownCall(stack, callback, phase);
        }
    }

    private void ObserveNativeUiWindowStack(NativeBattleUiWindows before, NativeBattleUiWindows after)
    {
        if (nativePreRootStack is not { } stack)
        {
            return;
        }
        before.Validate();
        after.Validate();
        if (!before.Rows.Select(row => row.Update0).SequenceEqual(after.Rows.Select(row => row.Update0)))
        {
            ObserveNativeUnknownStackCallback(0x4B9C80, "UI callback selection changed during the callback pass");
            return;
        }
        NativeBattleUiStackContext context = nativePreCoreUiStack ? NativeBattleUiStackContext.NormalPreCore47CF60 : NativeBattleUiStackContext.Other;
        //The retained physical bank supplies native slot order, independently of high-level helper call order.
        for (int index = 0; index < before.Rows.Length; index++)
        {
            stack = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, before.Rows[index].Update0, context, $"UI window {index}");
        }
        nativePreRootStack = stack;
    }

    private void ObserveNativeDescriptorStack(NativeBattleGraphicsState graphics, NativeBattleDescriptorState state, int slot)
    {
        ObserveNativeMegaSpark279DescriptorStack(graphics, state, slot);
        if (nativePreRootStack is not { } stack)
        {
            return;
        }
        NativeBattleDescriptorTask task = state.Tasks[slot];
        if (task.Kind == NativeBattleDescriptorTaskKind.Pump500CC0)
        {
            nativePreRootStack = NativeBattlePreRootStackObserver.ObserveDescriptorPump500CC0(stack, state, "descriptor pump");
        }
        else if (task.Kind == NativeBattleDescriptorTaskKind.Action104 && graphics.ActionGraphics?.Callback == 0x50A9A0)
        {
            nativePreRootStack = NativeBattlePreRootStackObserver.ObserveCasting50A9A0(stack, task.Phase, NativeEffectMemory?.ReturnedTaskPointer1D96AAC > 0, "casting descriptor");
        }
        else
        {
            ObserveNativeUnknownStackCallback(0x508434, $"descriptor callback {task.Kind}");
        }
    }

    private void ObserveNativeAuxiliaryStack(NativeBattleGraphicsState graphics, NativeBattleAuxiliaryTask task)
    {
        ObserveNativeMegaSpark279AuxiliaryStack(graphics, task);
        if (nativePreRootStack is not { } stack)
        {
            return;
        }
        if (task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && (task.UnknownByteMask & 15) == 0)
        {
            uint relative = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()) - 0x1D972C0u);
            if (relative % 156 == 0 && relative / 156 < 7)
            {
                nativePreRootStack = NativeBattlePreRootStackObserver.ObserveTextureAnimation5057D0(stack, task, graphics.Models[(int)(relative / 156)], "texture-animation callback");
                return;
            }
        }
        ObserveNativeUnknownStackCallback(0x508434, $"auxiliary callback {task.Kind}");
    }

    private void ObserveNativeModuleStack(NativeBattleModuleTask task)
    {
        ObserveNativeMegaSpark279ModuleStack(task);
        if (nativePreRootStack is not { } stack)
        {
            return;
        }
        nativePreRootStack = NativeBattlePreRootStackObserver.ObserveModuleCallback(stack, task, "shared module callback");
    }
}
