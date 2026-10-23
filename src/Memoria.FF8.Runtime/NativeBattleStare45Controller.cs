using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleStare45Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleStare45Controller
{
    public static NativeBattleStare45Frame Initialize(NativeBattleStare45Frame frame, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is null))
        {
            throw new InvalidDataException("Stare activation requires its six inactive physical bank owners.");
        }
        NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8366E0, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), Parameters(frame), inputs.ReadPackets(frame)));
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26543B0)?.ArenaOffset ?? throw new NotSupportedException("Stare requires its executed texture initializer.");
        if (tim < 0 || tim > NativeBattleEffectArena.Length - 0x18000)
        {
            throw new InvalidDataException("Stare addressed drawing banks exceed the actual COMMON arena.");
        }
        uint source = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim);
        NativeBattleStare45State state = frame.State.WriteDword(0x26543B0, source).WriteDword(0x2655750, 0).WriteDword(0x2654728, 0).WriteDword(0x26553C4, source).WriteDword(0x2654FB4, source + 0xC000).WriteDword(0x26553C0, source + 0xC000).WriteDword(0x2655754, source + 0x18000).WriteDword(0x2654FB0, source + 0x18000) with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() };
        NativeBattleEffectMemory memory = frame.Memory;
        if ((Parameters(frame).Flags79 & 0x01) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Stare requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Stare requires its loaded COMMON arena."), tim);
            memory = memory with { Textures = textures };
        }
        return frame with { Memory = memory, State = state, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x2654588, DrawArenaOffset = null } };
    }

    public static NativeBattleStare45Frame Advance(NativeBattleStare45Frame frame, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 6 || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Stare requires its exclusive six active hierarchy owners.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Stare physical task bank does not match its selected descriptor.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : 0x2654588u } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleStare45Frame Frame, int Survivors) AdvancePool(NativeBattleStare45Frame frame, int pool, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        NativeBattleStare45Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(pool, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            (current, int result) = AdvanceTask(current, address, resources, inputs);
            return new(new(current.Effect.Pools), result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static (NativeBattleStare45Frame Frame, int Result) AdvanceTask(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x8366E0 => AdvanceRoot(frame, address, resources, inputs),
            0x836990 => AdvanceTarget(frame, address, resources, inputs),
            0x836AC0 => AdvanceDirector(frame, address, resources, inputs),
            0x836D60 => AdvanceCasterProgramme(frame, address, resources, inputs),
            0x839590 => AdvanceTargetProgramme(frame, address, resources, inputs),
            0x839690 => AdvanceStage(frame, address, inputs),
            _ => throw new NotSupportedException("Stare reached an unsupported physical hierarchy callback.")
        };
    }

    private static NativeBattleStare45Frame Next(NativeBattleStare45Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleStare45Frame Done(NativeBattleStare45Frame frame, uint address) => Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static int Phase(NativeBattleStare45Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));

    private static (NativeBattleStare45Frame Frame, int Result) Finish(NativeBattleStare45Frame frame, uint address)
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
