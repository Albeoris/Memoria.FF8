using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    public static NativeBattleResonance49Frame Initialize(NativeBattleResonance49Frame frame, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is null))
        {
            throw new InvalidDataException("Resonance activation requires its six inactive physical bank owners.");
        }
        NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x821690, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), Parameters(frame), inputs.Packets));
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26464D8)?.ArenaOffset ?? throw new NotSupportedException("Resonance requires its executed texture initializer.");
        if (tim < 0 || tim > NativeBattleEffectArena.Length - 0x18000)
        {
            throw new InvalidDataException("Resonance addressed drawing banks exceed the actual COMMON arena.");
        }
        uint source = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim);
        NativeBattleResonance49State state = frame.State.WriteDword(0x26480E8, 0).WriteDword(0x2646BE8, 0).WriteDword(0x26464D8, source).WriteDword(0x264781C, source).WriteDword(0x26472D4, source + 0xC000).WriteDword(0x2647818, source + 0xC000).WriteDword(0x26480EC, source + 0x18000).WriteDword(0x26472D0, source + 0x18000) with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() };
        NativeBattleEffectMemory memory = frame.Memory;
        if ((Parameters(frame).Flags79 & 0x01) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Resonance requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Resonance requires its loaded COMMON arena."), tim);
            memory = memory with { Textures = textures };
        }
        return frame with { Memory = memory, State = state, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x26466C0, DrawArenaOffset = null } };
    }

    public static NativeBattleResonance49Frame Advance(NativeBattleResonance49Frame frame, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != 6 || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Resonance requires its exclusive six active hierarchy owners.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Resonance physical task bank does not match its selected descriptor.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : 0x26466C0u } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleResonance49Frame Frame, int Survivors) AdvancePool(NativeBattleResonance49Frame frame, int pool, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        NativeBattleResonance49Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(pool, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            (current, int result) = AdvanceTask(current, address, resources, inputs);
            return new(new(current.Effect.Pools), result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static (NativeBattleResonance49Frame Frame, int Result) AdvanceTask(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x821690 => AdvanceRoot(frame, address, resources, inputs),
            0x821840 => AdvanceTarget(frame, address, resources, inputs),
            0x821970 => AdvanceEffect(frame, address, resources, inputs),
            0x821C50 or 0x824140 => AdvanceProgramme(frame, address, resources, inputs),
            0x824260 => AdvanceScreen(frame, address, resources, inputs),
            0x824970 => AdvanceReaction(frame, address, resources, inputs),
            _ => throw new NotSupportedException("Resonance reached an unsupported physical hierarchy callback.")
        };
    }

    private static NativeBattleResonance49Frame Next(NativeBattleResonance49Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleResonance49Frame Done(NativeBattleResonance49Frame frame, uint address) => Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static int Phase(NativeBattleResonance49Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));

    private static (NativeBattleResonance49Frame Frame, int Result) Finish(NativeBattleResonance49Frame frame, uint address)
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
