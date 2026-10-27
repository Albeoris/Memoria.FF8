using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSigh46Controller
{
    public static NativeBattleSigh46Frame Initialize(NativeBattleSigh46Frame frame, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        AdmitNodeStorage(frame.State);
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is null))
        {
            throw new InvalidDataException("Sigh activation requires its six inactive physical bank owners.");
        }
        NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x831CB0, 100);
        hierarchy = root.Hierarchy.Write(root.Address, NativeBattleEffectRootTask.Initialize(root.Hierarchy.Read(root.Address), Parameters(frame), inputs.ReadPackets(frame)));
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x264EC60)?.ArenaOffset ?? throw new NotSupportedException("Sigh requires its executed texture initializer.");
        if (tim < 0 || tim > NativeBattleEffectArena.Length - 0xC000)
        {
            throw new InvalidDataException("Sigh addressed drawing banks exceed the actual COMMON arena.");
        }
        uint source = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim);
        NativeBattleSigh46State state = frame.State.WriteDword(0x26527A8, 0).WriteDword(0x264EFA4, 0).WriteDword(0x264EC60, source).WriteDword(0x2652784, source).WriteDword(0x2651B7C, source + 0x6000).WriteDword(0x2652780, source + 0x6000).WriteDword(0x26527AC, source + 0xC000).WriteDword(0x2651B78, source + 0xC000) with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() };
        NativeBattleEffectMemory memory = frame.Memory;
        if ((Parameters(frame).Flags79 & 0x01) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Sigh requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Sigh requires its loaded COMMON arena."), tim);
            memory = memory with { Textures = textures };
        }
        return NativeBattleTaskOperation.CompleteStandalone(operation, frame with { Memory = memory, State = state, Effect = frame.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x264ED50, DrawArenaOffset = null } });
    }

    public static NativeBattleSigh46Frame Advance(NativeBattleSigh46Frame frame, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        AdmitNodeStorage(frame.State);
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return NativeBattleTaskOperation.CompleteStandalone(operation, frame);
        }
        if (frame.Effect.Pools.Length != 6 || frame.State.InactiveBanks.Length != 6 || frame.State.InactiveBanks.Any(bank => bank is not null))
        {
            throw new InvalidDataException("Sigh requires its exclusive six active hierarchy owners.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Sigh physical task bank does not match its selected descriptor.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : 0x264ED50u } };
        return NativeBattleTaskOperation.CompleteStandalone(operation, survivors == 0 ? DetachBanks(frame) : frame);
    }

    private static (NativeBattleSigh46Frame Frame, int Survivors) AdvancePool(NativeBattleSigh46Frame frame, int pool, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        NativeBattleSigh46Frame current = frame;
        NativeBattleEffectHierarchyStep step = new NativeBattleEffectHierarchy(frame.Effect.Pools).Advance(pool, (hierarchy, address) =>
        {
            current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
            (current, int result) = AdvanceTask(current, address, resources, inputs);
            return new(new(current.Effect.Pools), result);
        });
        return (current with { Effect = current.Effect with { Pools = step.Hierarchy.Pools } }, step.Survivors);
    }

    public static (NativeBattleSigh46Frame Frame, int Result) AdvanceTask(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x831CB0 => AdvanceRoot(frame, address, resources, inputs),
            0x831FE0 => AdvanceTarget(frame, address, resources, inputs),
            0x832100 => AdvanceEngine(frame, address, resources, inputs),
            0x8359C0 => AdvanceCustom8359C0(frame, address, resources, inputs),
            0x835E90 => AdvanceParticles835E90(frame, address, resources, inputs),
            0x836090 => AdvanceSprite836090(frame, address, resources, inputs),
            0x831EC0 => AdvanceOpeningStage(frame, address, inputs),
            0x836420 => AdvanceClosingStage(frame, address, inputs),
            _ => throw new NotSupportedException("Sigh reached an unsupported physical hierarchy callback.")
        };
    }

    private static NativeBattleSigh46Frame Next(NativeBattleSigh46Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleSigh46Frame Done(NativeBattleSigh46Frame frame, uint address) => Next(Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1))), address);
    private static int Phase(NativeBattleSigh46Frame frame, uint address) => unchecked((sbyte)Read(frame, address).ReadByte(0x29));

    private static (NativeBattleSigh46Frame Frame, int Result) Finish(NativeBattleSigh46Frame frame, uint address)
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
