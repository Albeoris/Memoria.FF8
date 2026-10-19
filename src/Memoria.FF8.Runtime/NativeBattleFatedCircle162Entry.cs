using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162Frame Enter5F90F0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleActionPacketsState packets, NativeBattleFatedCircle162Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 162, Initializer: 0x5F90D0, Entry: 0x5F90F0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native5F90F0 requires its original binding and actual action parameters.");
        }
        resources.Validate();
        NativeBattleFatedCircle162State retained = memory.FatedCircle162 ?? NativeBattleFatedCircle162State.FromColdProcess();
        if (!retained.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleFatedCircle162Resources.StaticAddress, resources.StaticData.Data.AsSpan());
            foreach (NativeBattleFatedCircle162ColdRegion region in resources.ColdGlobals)
            {
                staging = staging.Write(region.Address, region.Data.AsSpan());
            }
        }
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native5F90F0 requires its physical first-contact pointer.");
        NativeBattleEffectResourcePointer initializer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x23D0CE0) ?? throw new NotSupportedException("Native5F90F0 requires its actual initializer publication.");
        staging = WriteDword(staging, 0x23D0CE0, checked(0x20DFAB8u + (uint)initializer.ArenaOffset));
        staging = WriteDword(staging, 0x23CE608, packets.ReadContact(first).Primary.ActorIndex);
        staging = WriteDword(staging, 0x23D0798, parameters.Actor78);
        staging = WriteDword(staging, 0x23D0D74, 170);
        staging = WriteDword(staging, 0x23D0D7C, 0);
        staging = WriteDword(staging, 0x23CE61C, 0);
        staging = WriteDword(staging, 0x23D0D84, 0x20DFAB8u);
        for (uint address = 0x23CEA40; address < 0x23CF1C0; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        for (uint address = 0x23CE650; address < 0x23CEA10; address += 24)
        {
            staging = WriteDword(staging, address, 0);
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23CE640, 0x23CE620, 16, 2);
        (main, int mainSlot) = main.Append(0x5FD090);
        main = main with { Slots = main.Slots.SetItem(mainSlot, main.Slots[mainSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23CF430, 0x23CF440, 36, 100);
        (children, int childSlot) = children.Append(0x5F91D0);
        children = children with { Slots = children.Slots.SetItem(childSlot, children.Slots[childSlot].WriteWord(12, 0)) };
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5F90F0 requires its actual texture owner.");
        textures = textures.Register5099A0(resources.StaticData, 0x1688);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native5F90F0 requires its actual initializer texture.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { FatedCircle162 = new(true, parameters, resources), Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23CE640, DrawArenaOffset = 0 }, staging, random);
    }

    public static NativeBattleFatedCircle162Frame Advance5FD090(NativeBattleFatedCircle162Frame frame, Func<NativeBattleFatedCircle162Frame, NativeBattleEffectTaskPool, int, NativeBattleFatedCircle162ChildStep> advanceChild, Func<NativeBattleFatedCircle162Frame, uint, NativeBattleFatedCircle162Frame> beginTextureLoad)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23CE640 || frame.Effect.Pools[1].DescriptorAddress != 0x23CF430)
        {
            throw new InvalidDataException("Native5FD090 requires its two original task descriptors.");
        }
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((main, slot) =>
        {
            NativeBattleModulePoolTask task = main.Slots[slot];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
            int result;
            if (task.Callback == 0x5FD090)
            {
                ushort counter = task.ReadWord(12);
                int offset = (counter & 1) * 0x8000;
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = offset }, Staging = WriteDword(frame.Staging, 0x23D0D88, unchecked(0x20DFAB8u + (uint)offset)) };
                NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
                {
                    NativeBattleFatedCircle162ChildStep child = advanceChild(frame, pool, index);
                    frame = child.Frame;
                    return new(child.Pool, child.Result);
                });
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
                main = frame.Effect.Pools[0];
                task = main.Slots[slot].WriteWord(12, unchecked((ushort)(counter + 1)));
                result = children.Survivors == 0 ? 2 : 0;
            }
            else if (task.Callback == 0x5FCFF0)
            {
                if (task.ReadWord(12) == 2)
                {
                    uint completion = checked(main.BankAddress + (uint)(slot * main.Stride) + 14);
                    frame = beginTextureLoad(frame, completion);
                    main = frame.Effect.Pools[0];
                    task = main.Slots[slot].WriteWord(14, 0);
                }
                result = task.ReadWord(14) != 0 ? 2 : 0;
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            }
            else
            {
                throw new NotSupportedException($"Native Fated Circle main callback0x{task.Callback:X} is unsupported.");
            }
            main = main with { Slots = main.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, main) } };
            return new(main, result);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : 0x23CE640u } };
    }

    private static NativeBattleFileStaging WriteDword(NativeBattleFileStaging staging, uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging WriteWord(NativeBattleFileStaging staging, uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static int ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));
    private static short ReadWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
}
