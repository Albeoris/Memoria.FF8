using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163Frame Enter5F5B80(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleActionPacketsState packets, NativeBattleRoughDivide163Resources resources, NativeCrtRandomState random, NativeBattleActionMatrix actorMatrix)
    {
        if (effect.Binding is not { Effect: 163, Initializer: 0x5F5B60, Entry: 0x5F5B80 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native5F5B80 requires its original binding and actual action parameters.");
        }
        resources.Validate();
        NativeBattleRoughDivide163State retained = memory.RoughDivide163 ?? NativeBattleRoughDivide163State.FromColdProcess();
        if (!retained.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleRoughDivide163Resources.StaticAddress, resources.StaticData.Data.AsSpan());
            foreach (NativeBattleRoughDivide163ColdRegion region in resources.ColdGlobals)
            {
                staging = staging.Write(region.Address, region.Data.AsSpan());
            }
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native5F5B80 requires its physical first-contact pointer.");
        byte recipient = packets.ReadContact(first).Primary.ActorIndex;
        NativeBattleEffectResourcePointer initializer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23CE278) ?? throw new NotSupportedException("Native5F5B80 requires its actual initializer publication.");
        staging = WriteDword(staging, 0x23CE278, checked(0x20DFAB8u + (uint)initializer.ArenaOffset));
        staging = WriteDword(staging, 0x23CA1A0, recipient);
        staging = WriteDword(staging, 0x23CE274, parameters.Actor78);
        staging = WriteDword(staging, 0x23CE580, 0x20DFAB8);
        for (uint address = 0x23CA1D8; address < 0x23CAB38; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        for (uint address = 0x23CAB38; address < 0x23CD0B8; address += 32)
        {
            staging = WriteDword(staging, address, 0);
        }
        staging = StoreMatrix(staging, 0x23CD0D8, actorMatrix, true);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23CA1C8, 0x23CA1A8, 16, 2);
        (main, int mainSlot) = main.Append(0x5F9020);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23CD348, 0x23CD358, 36, 100);
        (children, int childSlot) = children.Append(0x5F5C80);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5F5B80 requires its actual texture owner.");
        textures = textures.Register5099A0(resources.StaticData, 0xE3C);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native5F5B80 requires its actual initializer texture.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { RoughDivide163 = new(true, parameters, recipient, resources), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23CA1C8, DrawArenaOffset = 0 }, staging, random);
    }

    public static NativeBattleRoughDivide163Frame Advance5F9020(NativeBattleRoughDivide163Frame frame, Func<NativeBattleRoughDivide163Frame, NativeBattleEffectTaskPool, int, NativeBattleRoughDivide163ChildStep> advanceChild, Func<NativeBattleRoughDivide163Frame, uint, NativeBattleRoughDivide163Frame> beginTextureLoad)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23CA1C8 || frame.Effect.Pools[1].DescriptorAddress != 0x23CD348)
        {
            throw new InvalidDataException("Native5F9020 requires its original task descriptors.");
        }
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((main, slot) =>
        {
            NativeBattleModulePoolTask task = main.Slots[slot];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
            int result;
            if (task.Callback == 0x5F9020)
            {
                ushort counter = task.ReadWord(12);
                int offset = (counter & 1) * 0x10000;
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = offset }, Staging = WriteDword(frame.Staging, 0x23CE584, checked(0x20DFAB8u + (uint)offset)) };
                NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
                {
                    NativeBattleRoughDivide163ChildStep child = advanceChild(frame, pool, index);
                    frame = child.Frame;
                    return new(child.Pool, child.Result);
                });
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
                main = frame.Effect.Pools[0];
                if (children.Survivors == 0)
                {
                    (main, int loader) = main.Append(0x5F9090);
                    main = main with { Slots = main.Slots.SetItem(loader, main.Slots[loader].WriteWord(12, 0).WriteWord(14, 0)) };
                }
                task = main.Slots[slot].WriteWord(12, unchecked((ushort)(counter + 1)));
                result = children.Survivors == 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5F9090)
            {
                if (task.ReadWord(12) == 1)
                {
                    uint completion = checked(main.BankAddress + (uint)(slot * main.Stride) + 14);
                    frame = beginTextureLoad(frame, completion);
                    main = frame.Effect.Pools[0];
                    task = main.Slots[slot];
                }
                result = task.ReadWord(14) != 0 ? 2 : 0;
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            }
            else
            {
                throw new NotSupportedException($"Native Rough Divide main callback0x{task.Callback:X} is unsupported.");
            }
            main = main with { Slots = main.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
            return new(main, result);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23CA1C8u } };
    }

    public static NativeBattleRoughDivide163Frame ComposeCapturedActor(NativeBattleRoughDivide163Frame frame, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, ReadMatrix(frame.Staging, 0x23CD0D8));
        return frame with { Staging = StoreMatrix(frame.Staging, 0x23CD2F8, matrix, false) };
    }

    private static NativeBattleFileStaging StoreMatrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix, bool copiedPadding)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = WriteWord(staging, address + (uint)index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        if (copiedPadding)
        {
            staging = Forget(staging, address + 18, 2);
        }
        return WriteDword(WriteDword(WriteDword(staging, address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
    }
}
