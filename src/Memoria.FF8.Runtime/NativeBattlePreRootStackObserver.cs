namespace Memoria.FF8.Runtime;

public enum NativeBattleUiStackContext
{
    NormalPreCore47CF60,
    Other
}

/// <summary>Instruction observations for one common47CF60 host frame; no value is carried from an earlier frame.</summary>
public static class NativeBattlePreRootStackObserver
{
    public const int CommonWordOffset = -0x8C;
    public const int HeroScaleWordOffset = -0x88;
    public const int CommonIntervalLength = 8;

    public static NativeBattleStackBytes BeginHostFrame(long callback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callback);
        return NativeBattleStackBytes.Unknown($"common47CF60:{callback}", CommonWordOffset, CommonIntervalLength);
    }

    public static NativeBattleStackBytes ObserveUiWindowUpdate(NativeBattleStackBytes stack, uint selectedCallback, NativeBattleUiStackContext context, string phase)
    {
        RequireInterval(stack);
        if (selectedCallback == 0)
        {
            return stack;
        }
        if (context == NativeBattleUiStackContext.NormalPreCore47CF60 && selectedCallback == 0x4B0500)
        {
            //The selected footer pushes the actual rectangle pointer before its mandatory CALL.
            return stack.StoreUInt32(HeroScaleWordOffset, 0x1A7904C, 0x4B04C6, phase).StoreUInt32(CommonWordOffset, 0x4B04E3, 0x4B04DE, phase);
        }
        return ObserveUnknownCall(stack, selectedCallback, phase);
    }

    public static NativeBattleStackBytes ObserveDescriptorPump500CC0(NativeBattleStackBytes stack, NativeBattleDescriptorState before, string phase)
    {
        RequireInterval(stack);
        before.Validate();
        byte priority = before.Priorities[before.Depth];
        foreach (int slot in before.Queue)
        {
            byte state = before.Packets[slot].PriorityState;
            if (state <= priority)
            {
                continue;
            }
            int commandState = state & 15;
            if (commandState is 0 or 15)
            {
                //Dispatch or completion can change the priority stack and the later selected command closure.
                return ObserveUnknownCall(stack, 0x500CC0, phase);
            }
        }
        //Nonempty list counting reaches the adjacent word, while rotation preserves both lanes.
        return before.Queue.IsEmpty ? stack : stack.StoreUInt32(HeroScaleWordOffset, 0x50CFB1, 0x50CFAC, phase);
    }

    public static NativeBattleStackBytes ObserveCasting50A9A0(NativeBattleStackBytes stack, byte phaseBefore, bool returnedDescriptorLive, string phase)
    {
        RequireInterval(stack);
        if (phaseBefore == 6 && returnedDescriptorLive)
        {
            //50AE80 returns before501190 when the retained main descriptor remains nonnull.
            return stack;
        }
        return ObserveUnknownCall(stack, 0x50A9A0, phase);
    }

    public static NativeBattleStackBytes ObserveTextureAnimation5057D0(NativeBattleStackBytes stack, NativeBattleAuxiliaryTask task, NativeBattleGraphicsModel model, string phase)
    {
        RequireInterval(stack);
        if (task.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || task.Payload.IsDefault || task.Payload.Length != 32)
        {
            throw new InvalidDataException("Native5057D0 observation requires its selected physical auxiliary callback.");
        }
        NativeBattleAuxiliaryProgram program = task.Program ?? throw new InvalidDataException("Native5057D0 observation requires its deadline owner.");
        program.Validate();
        if ((task.UnknownByteMask & 0x20Fu) != 0)
        {
            return ObserveUnknownCall(stack, 0x5057D0, phase);
        }
        uint modelAddress = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan());
        if (modelAddress != 0x1D972C0u + 156u * checked((uint)model.Index))
        {
            throw new InvalidDataException("Native5057D0 observation selected a different physical model.");
        }
        if (program.Source[program.Offset] == 255 && (model.Flags0 & 2) != 0 && model.ResourceFlags44 is ushort flags && (flags & 0x40) == 0 && model.Sequence == task.Payload[9])
        {
            return stack;
        }
        //Copy/release paths require their actual50C780 resource selection and RECT-pointer write observation.
        return ObserveUnknownCall(stack, 0x5057D0, phase);
    }

    public static NativeBattleStackBytes ObserveModuleCallback(NativeBattleStackBytes stack, NativeBattleModuleTask task, string phase)
    {
        RequireInterval(stack);
        if (task.Kind == NativeBattleModuleKind.CriticalFlash571300 && task.Callback is null)
        {
            //Its full rendering path reaches only R-30, above the tracked R-38/R-34 words.
            return stack;
        }
        return ObserveUnknownCall(stack, task.Callback ?? 0x508434, phase);
    }
    public static NativeBattleStackBytes ObserveUnknownCall(NativeBattleStackBytes stack, uint selectedCallback, string phase)
    {
        RequireInterval(stack);
        return stack.Invalidate(CommonWordOffset, CommonIntervalLength, selectedCallback, phase);
    }

    private static void RequireInterval(NativeBattleStackBytes stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        stack.Validate();
        if (stack.FirstOffset != CommonWordOffset || stack.Bytes.Length != CommonIntervalLength)
        {
            throw new InvalidDataException("The common-frame observer requires its actual adjacent C-8C and C-88 DWORD interval.");
        }
    }
}
