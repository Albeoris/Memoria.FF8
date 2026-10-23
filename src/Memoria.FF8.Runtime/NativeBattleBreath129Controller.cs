using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Controller
{
    public const uint RootDescriptor = 0x25211D0;
    public const uint RootBank = 0x2521168;
    public const uint WorkingGlobal = 0x252115C;
    public const uint CursorGlobal = 0x252117C;
    public const uint CasterGlobal = 0x2521134;
    public const uint StageGlobal = 0x2521130;
    public const uint CompositeMatrix = 0x2521110;
    public const uint YawMatrix = 0x2521138;

    public static NativeBattleBreath129Frame Initialize(NativeBattleBreath129Frame frame, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Breath activation requires its inactive physical root owner.");
        }
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2521158)?.ArenaOffset ?? throw new NotSupportedException("Breath requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Breath requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Breath requires its current arena."), tim);
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (roots, int slot) = roots.Append(0x6B4CB0);
        roots = roots with { Slots = roots.Slots.SetItem(slot, roots.Slots[slot].WriteBytes(12, new byte[8])) };
        NativeBattleBreath129State state = frame.State.RequireSeeds(resources).WriteDword(0x2521158, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress).WriteDword(0x2521160, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state with { RootBankStorage = null }, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [roots], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleBreath129Frame Advance(NativeBattleBreath129Frame frame, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 6) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Breath requires its actual root and five working pool owners.");
        }
        NativeBattleBreath129Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != 0x6B4CB0)
            {
                throw new NotSupportedException("Breath reached an unsupported root callback.");
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
            throw new InvalidDataException("Breath completion requires all six queues to drain.");
        }
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(current.Memory, current.Effect);
        return current with { Memory = transfer.Memory, Effect = transfer.Effect with { Pools = [] }, State = current.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(current.Effect.Pools[0]) } };
    }

    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceRoot(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(Working(frame) + (first ? 0x3B70u : 0x1BB70u))).WriteDword(StageGlobal, 0) };
        frame = Write(frame, address, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            uint working = Working(frame);
            NativeBattleEffectTaskPool[] children = [NativeBattleEffectTaskPool.Initialize(0x2521180, working, 0x18, 9), NativeBattleEffectTaskPool.Initialize(0x2521190, working + 0xD8, 0x1E8, 3), NativeBattleEffectTaskPool.Initialize(0x25211A0, working + 0x690, 0x80, 16), NativeBattleEffectTaskPool.Initialize(0x25211B0, working + 0xE90, 0x20, 64), NativeBattleEffectTaskPool.Initialize(0x25211C0, working + 0x1690, 0x330, 1)];
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], .. children] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, children.ToImmutableArray()) };
            frame = WriteBytes(frame, working + 0x19C0, NativeBattleEffectArena.Clear(), 0x1C00);
            for (int index = 0; index < 256; index++)
            {
                frame = StoreWord(frame, working + 0x19C0u + (uint)(index * 28), 0xFFFF);
            }
            for (int index = 0; index < 16; index++)
            {
                frame = StoreWord(frame, working + 0x35C0u + (uint)(index * 2), unchecked((ushort)NativeBattleCameraMath.Sine(index * 2048 / 15)));
            }
            (frame, uint mainAddress) = Append(frame, 5, 0x6B5140);
            if (mainAddress == 0)
            {
                throw new InvalidDataException("Breath failed its required main allocation.");
            }
            NativeBattleModulePoolTask task = Read(frame, mainAddress).WriteBytes(12, new byte[0x324]);
            foreach ((int offset, int size, uint source) in new (int, int, uint)[] { (0x38, 0x108, 0x12239B8), (0x140, 0x68, 0x1226098), (0x1A8, 0x188, 0x122688C) })
            {
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(offset, size).ToImmutableArray()).Initialize7016B0(resources.Act(source), size);
                task = task.WriteBytes(offset, workspace.Data.AsSpan());
            }
            int count = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(16);
            if (count is < 1 or > 3)
            {
                throw new NotSupportedException("Breath targets must fit its three actual pointer fields.");
            }
            int contact = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Breath requires its captured contacts.");
            task = task.WriteWord(14, checked((ushort)count));
            for (int index = 0; index < count; index++)
            {
                task = task.WriteByte(0x1C + index, checked((byte)index)).WriteDword(16 + index * 4, ModelAddress(inputs.Packets.ReadContact(contact + index).Primary.ActorIndex));
            }
            for (int firstIndex = 0; firstIndex < count - 1; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < count; secondIndex++)
                {
                    uint a = task.ReadDword(16 + firstIndex * 4);
                    uint b = task.ReadDword(16 + secondIndex * 4);
                    if ((inputs.ReadModel(frame, a).Transform ?? throw new NotSupportedException("Breath requires its current first target X.")).Position28.X > (inputs.ReadModel(frame, b).Transform ?? throw new NotSupportedException("Breath requires its current second target X.")).Position28.X)
                    {
                        byte mapping = task.ReadByte(0x1C + firstIndex);
                        task = task.WriteDword(16 + firstIndex * 4, b).WriteDword(16 + secondIndex * 4, a).WriteByte(0x1C + firstIndex, task.ReadByte(0x1C + secondIndex)).WriteByte(0x1C + secondIndex, mapping);
                    }
                }
            }
            frame = Write(frame, mainAddress, task.WriteWord(0x22, count > 1 ? (ushort)1 : (ushort)0).WriteWord(0x26, unchecked((ushort)(count * 4096 / 33))));
            frame = NativeBattleBreath129Preprocessing.Initialize(frame, resources);
            frame = WriteBytes(frame, working + 0x35E0, NativeBattleEffectArena.Clear(), 16);
        }
        int main = 1;
        int fibre = 1;
        int target = 1;
        int sprite = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, main) = Scan(frame, 5, resources, inputs);
            foreach (uint actor in CurrentBodyActors(frame))
            {
                frame = inputs.Execute(frame, new NativeBattleBreath129ComposeBody5095B0(actor));
            }
            (frame, fibre) = Scan(frame, 3, resources, inputs);
            foreach (uint actor in CurrentBodyActors(frame))
            {
                uint row = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, actor));
                frame = inputs.PublishBody508C90(frame, row).Frame;
            }
            (frame, target) = Scan(frame, 2, resources, inputs);
            (frame, sprite) = Scan(frame, 1, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleBreath129Stage5713E0(unchecked((int)frame.State.ReadDword(StageGlobal)), 0));
        if (Read(frame, address).ReadByte(15) != 0 && main == 0 && fibre == 0 && target == 0 && sprite == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    private static IEnumerable<uint> CurrentBodyActors(NativeBattleBreath129Frame frame)
    {
        for (int index = 0; index < 3; index++)
        {
            uint actor = ReadDword(frame, Working(frame) + 0x35E4u + (uint)(index * 4));
            if (actor != 0)
            {
                yield return actor;
            }
        }
        yield return frame.State.ReadDword(CasterGlobal);
    }

    private static (NativeBattleBreath129Frame Frame, int Survivors) Scan(NativeBattleBreath129Frame frame, int index, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleBreath129Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            (current, int result) = pool.Slots[slot].Callback switch
            {
                0x6B5140 => AdvanceMain(current, address, resources, inputs),
                0x6B5FC0 => AdvanceTarget(current, address, resources, inputs),
                0x6B6130 => AdvanceSprite(current, address, resources, inputs),
                0x6B62D0 => AdvanceCurve(current, address, resources, inputs),
                0x6B68C0 => AdvanceFibre(current, address, resources, inputs),
                _ => throw new NotSupportedException("Breath reached an unsupported working callback.")
            };
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    private static uint Working(NativeBattleBreath129Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ReadDword(NativeBattleBreath129Frame frame, uint address) => BitConverter.ToUInt32(ReadBytes(frame, address, 4).Read(0, 4));
    private static short ReadSigned(NativeBattleBreath129Frame frame, uint address) => BitConverter.ToInt16(ReadBytes(frame, address, 2).Read(0, 2));
    private static NativeBattleBreath129Frame StoreWord(NativeBattleBreath129Frame frame, uint address, ushort value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    private static NativeBattleBreath129Frame StoreDword(NativeBattleBreath129Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    private static void RequireBinding(NativeBattleBreath129Frame frame)
    {
        if (frame.Effect.RequestedEffect != 129 || frame.Effect.Binding.Entry != 0x6B4C00 || frame.Effect.Binding.Initializer != 0x6B4C10)
        {
            throw new InvalidDataException("Breath requires its original selected entry and initializer.");
        }
    }
}
