using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    public static NativeBattleMagmaBreath48Frame Initialize(NativeBattleMagmaBreath48Frame frame, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is null))
        {
            throw new InvalidDataException("Magma Breath activation requires its six inactive physical bank owners.");
        }
        NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x824E40, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), Parameters(frame), inputs.Packets));
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26481B4)?.ArenaOffset ?? throw new NotSupportedException("Magma Breath requires its executed texture initializer.");
        if (tim < 0 || tim > NativeBattleEffectArena.Length - 0xC000)
        {
            throw new InvalidDataException("Magma Breath addressed drawing banks exceed the actual COMMON arena.");
        }
        uint source = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim);
        NativeBattleMagmaBreath48State state = frame.State.WriteDword(0x26495E0, 0).WriteDword(0x26489AC, 0).WriteDword(0x26481B4, source).WriteDword(0x26495BC, source).WriteDword(0x26489B4, source + 0x6000).WriteDword(0x26495B8, source + 0x6000).WriteDword(0x26495E4, source + 0xC000).WriteDword(0x26489B0, source + 0xC000) with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() };
        NativeBattleEffectMemory memory = frame.Memory;
        if ((Parameters(frame).Flags79 & 0x01) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Magma Breath requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Magma Breath requires its loaded COMMON arena."), tim);
            memory = memory with { Textures = textures };
        }
        return frame with { Memory = memory, State = state, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x26482A8, DrawArenaOffset = null } };
    }

    public static NativeBattleMagmaBreath48Frame Advance(NativeBattleMagmaBreath48Frame frame, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 6 || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Magma Breath requires its exclusive six active hierarchy owners.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Magma Breath physical task bank does not match its selected descriptor.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : 0x26482A8u } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Survivors) AdvancePool(NativeBattleMagmaBreath48Frame frame, int pool, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        NativeBattleMagmaBreath48Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(pool, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            (current, int result) = AdvanceTask(current, address, resources, inputs);
            return new(new(current.Effect.Pools), result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceTask(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x824E40 => AdvanceRoot(frame, address, resources, inputs),
            0x825170 => AdvanceTarget(frame, address, resources, inputs),
            0x825220 => AdvanceColors(frame, address, resources, inputs),
            0x8253B0 => AdvanceEngine(frame, address, resources, inputs),
            0x82A540 => AdvanceMiscellaneous(frame, address),
            0x825050 => AdvanceOpeningStage(frame, address, inputs),
            0x82A6A0 => AdvanceClosingStage(frame, address, inputs),
            _ => throw new NotSupportedException("Magma Breath reached an unsupported physical hierarchy callback.")
        };
    }

    private static NativeBattleMagmaBreath48Frame Next(NativeBattleMagmaBreath48Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleMagmaBreath48Frame Done(NativeBattleMagmaBreath48Frame frame, uint address) => Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static int Phase(NativeBattleMagmaBreath48Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) Finish(NativeBattleMagmaBreath48Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        if ((task.ReadByte(0x26) & 1) == 0 || task.ReadByte(0x28) != 0)
        {
            return (frame, 0);
        }
        NativeBattleEffectHierarchy completed = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address);
        return (frame with { Effect = frame.Effect with { Pools = completed.Pools } }, 2);
    }
}
