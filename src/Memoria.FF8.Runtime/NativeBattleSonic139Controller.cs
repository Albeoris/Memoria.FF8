using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSonic139Controller
{
    public static NativeBattleSonic139Frame Initialize(NativeBattleSonic139Frame frame, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Sonic Wave activation requires its inactive physical root owner.");
        }
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Sonic Wave requires its executed initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Sonic Wave requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Sonic Wave requires its current arena."), tim);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (roots, int slot) = roots.Append(RootCallback);
        roots = roots.WriteSlot(slot, roots.Slots[slot].WriteBytes(12, new byte[8]));
        NativeBattleSonic139State state = frame.State.CopyEntrySnapshot6A8860(resources).WriteDword(InitializerGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state with { RootBankStorage = null }, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [roots], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleSonic139Frame Advance(NativeBattleSonic139Frame frame, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Sonic Wave requires its exclusive original root and working pools.");
        }
        NativeBattleSonic139Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Sonic Wave reached an unsupported root callback.");
            }
            (current, int result) = AdvanceRoot(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, step.Pool);
        current = current with { Effect = current.Effect with { ReturnedDescriptor = step.Survivors == 0 ? 0u : RootDescriptor } };
        if (step.Survivors != 0)
        {
            return current;
        }
        if (current.Effect.Pools.Any(pool => pool.Head is not null || pool.Slots.Any(task => (task.Flags & 1) != 0)))
        {
            throw new InvalidDataException("Sonic Wave completion requires all physical queues to drain.");
        }
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(current.Memory, current.Effect);
        return current with { Memory = transfer.Memory, Effect = transfer.Effect with { Pools = [] }, State = current.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(current.Effect.Pools[0]) } };
    }

    public static (NativeBattleSonic139Frame Frame, int Result) AdvanceRoot(NativeBattleSonic139Frame frame, uint address, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(frame.State.ReadDword(WorkingGlobal) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = NativeBattleSonic139Preprocessing.Initialize(frame, resources);
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            uint working = frame.State.ReadDword(WorkingGlobal);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, 0x4DC, 1);
            NativeBattleEffectTaskPool lower = NativeBattleEffectTaskPool.Initialize(LowerDescriptor, working, 0x298, 4);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, lower] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, lower]) };
            (frame, uint mainAddress) = Append(frame, 1, MainCallback);
            if (mainAddress == 0)
            {
                throw new InvalidDataException("Sonic Wave failed its required initial main allocation.");
            }
            NativeBattleModulePoolTask task = Read(frame, mainAddress).WriteBytes(12, new byte[0x4D0]);
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(0x30, 0x4AC).ToImmutableArray()).Initialize7016B0(resources.MainAct, 0x4AC);
            task = workspace.CopyTo(task, 0x30, retainUnknown: false);
            int header = Parameters(frame).HeaderIndex7C;
            int count = inputs.Packets.Headers[header].ReadByte(16);
            if (count is < 1 or > 3)
            {
                throw new NotSupportedException("Sonic Wave target list must fit its three actual actor fields and nonzero native divisor.");
            }
            int firstContact = inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Sonic Wave requires its captured contacts.");
            task = task.WriteWord(14, checked((ushort)count));
            frame = Write(frame, mainAddress, task);
            short[] sum = new short[3];
            for (int index = 0; index < count; index++)
            {
                uint actor = ModelAddress(inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex);
                frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteByte(16 + index, checked((byte)index)).WriteDword(20 + index * 4, actor));
                NativeBattleSonic139PointStep anchor = inputs.ReadAnchor571400(frame, actor);
                frame = anchor.Frame;
                NativeBattleVector point = Point(anchor.Point8);
                sum[0] = unchecked((short)(sum[0] + point.X));
                sum[1] = unchecked((short)(sum[1] + point.Y));
                sum[2] = unchecked((short)(sum[2] + point.Z));
                task = Read(frame, mainAddress).WriteWord(0x28, unchecked((ushort)sum[0])).WriteWord(0x2A, unchecked((ushort)sum[1])).WriteWord(0x2C, unchecked((ushort)sum[2]));
                frame = Write(frame, mainAddress, task);
            }
            task = Read(frame, mainAddress).WriteWord(0x28, unchecked((ushort)(sum[0] / count))).WriteWord(0x2A, unchecked((ushort)(sum[1] / count))).WriteWord(0x2C, unchecked((ushort)(sum[2] / count)));
            frame = Write(frame, mainAddress, task);
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, survivors) = Scan(frame, 1, resources, inputs);
            (frame, _) = Scan(frame, 2, resources, inputs);
        }
        if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleSonic139Frame Frame, int Survivors) Scan(NativeBattleSonic139Frame frame, int index, NativeBattleSonic139Resources resources, NativeBattleSonic139Inputs inputs)
    {
        NativeBattleSonic139Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            (current, int result) = pool.Slots[slot].Callback switch
            {
                MainCallback => AdvanceMain(current, address, resources, inputs),
                TargetCallback => AdvanceTarget(current, address, resources, inputs),
                _ => throw new NotSupportedException("Sonic Wave reached an unsupported working callback.")
            };
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        int index = -1;
        for (int candidate = 0; candidate < effect.Pools.Length; candidate++)
        {
            if (effect.Pools[candidate].DescriptorAddress == MainDescriptor)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0 || address != effect.Pools[index].BankAddress + 0x13)
        {
            throw new InvalidDataException("Sonic Wave completion byte leaves its actual main task.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[index];
        return effect with { Pools = effect.Pools.SetItem(index, pool.WriteSlot(0, pool.Slots[0].WriteByte(0x13, value))) };
    }

    private static void RequireBinding(NativeBattleSonic139Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Sonic Wave requires its original selected entry and initializer.");
        }
    }
}
