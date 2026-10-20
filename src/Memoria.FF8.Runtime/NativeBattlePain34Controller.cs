using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePain34LeafStep(NativeBattlePain34Frame Frame, bool Complete);
public sealed record NativeBattlePain34Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPose, Func<NativeBattlePain34Frame, uint, NativeBattlePain34LeafStep> AdvancePlayer, Func<NativeBattlePain34Frame, int, NativeBattlePain34Frame> Contact, Action<uint> PlaySound);

public static class NativeBattlePain34Controller
{
    public const uint Descriptor = 0x26BAAD0;
    public const uint Bank = 0x26BAA08;
    public static NativeBattlePain34Frame Initialize865600(NativeBattlePain34Frame frame, NativeBattlePain34Resources resources, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Pain entry requires current action parameters.");
        NativeBattlePain34State state = RequireState(frame).WriteDword(0x26C0378, 0).WriteDword(0x26BAAE0, 0);
        NativeBattleEffectResourcePointer pointer = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26BA9E0) ?? throw new NotSupportedException("Native Pain entry requires its published original TIM pointer.");
        uint tim = checked(0x20DFAB8u + (uint)pointer.ArenaOffset);
        state = state.WriteDword(0x26BA9E0, tim).WriteDword(0x26C028C, tim).WriteDword(0x26C0114, checked(tim + 0xB800)).WriteDword(0x26C0288, checked(tim + 0xB800)).WriteDword(0x26C037C, checked(tim + 0x17000)).WriteDword(0x26C0110, checked(tim + 0x17000));
        NativeBattleEffectMemory memory = frame.Memory with { Pain34 = state };
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = (memory.Textures ?? throw new NotSupportedException("Native Pain entry requires its actual texture queue.")).Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Pain entry requires its loaded TIM arena."), pointer.ArenaOffset);
            memory = memory with { Textures = textures };
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(Descriptor, Bank, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x26C0118, 0x26C0128, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x26C0100, 0x26BAAE8, 2204, 10)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x865720, 100);
        hierarchy = appended.Hierarchy;
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(12, 0x1D99A78);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, unchecked((sbyte)root.ReadByte(42)), unchecked((sbyte)root.ReadByte(43)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        root = root.WriteByte(44, actors.Actor).WriteByte(45, actors.Target).WriteByte(41, 0).WriteWord(90, header.ReadByte(16)).WriteWord(88, header.ReadByte(17));
        byte last = unchecked((byte)(root.ReadWord(90) - 1));
        root = root.WriteByte(47, last < unchecked((short)root.ReadWord(88)) ? root.ReadByte(88) : last);
        return frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Write(appended.Address, root).Pools, ReturnedDescriptor = Descriptor } };
    }

    public static NativeBattlePain34Frame Advance865720(NativeBattlePain34Frame frame, NativeBattlePain34Resources resources, NativeBattlePain34Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Pain requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        if (hierarchy.Pools.Length != 3 || hierarchy.Pools[0].DescriptorAddress != Descriptor || hierarchy.Pools[1].DescriptorAddress != 0x26C0118 || hierarchy.Pools[2].DescriptorAddress != 0x26C0100)
        {
            throw new InvalidDataException("Native Pain requires its original three task banks.");
        }
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (active, address) =>
        {
            hierarchy = active;
            NativeBattleModulePoolTask root = hierarchy.Read(address);
            NativeBattlePain34State state = RequireState(frame).WriteDword(0x26C0374, 0x2793E58).WriteDword(0x26BAA00, 0x2793E58);
            bool odd = (root.ReadByte(92) & 1) != 0;
            uint packet = state.Dword(odd ? 0x26C028Cu : 0x26C0288u);
            state = state.WriteDword(0x26BA9E4, packet).WriteDword(0x26BA9FC, state.Dword(odd ? 0x26C0114u : 0x26C0110u));
            frame = frame with { Memory = frame.Memory with { Pain34 = state, SharedEffectView2793E58 = inputs.Camera }, Effect = frame.Effect with { DrawArenaOffset = ArenaOffset(packet) } };
            root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(root, parameters, new ActorView(inputs.ReadModel), inputs.ReadPose);
            Store(address, root);
            switch (unchecked((sbyte)root.ReadByte(41)))
            {
                case 0:
                case 1:
                case 5:
                case 7:
                case 8:
                    Store(address, Next(root));
                    break;
                case 2:
                    if (root.ReadByte(40) == 0)
                    {
                        Store(address, Next(root));
                    }
                    break;
                case 3:
                    Store(address, root.WriteByte(99, 1));
                    Append(1, 0x8658A0, 88, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 4:
                    if (root.ReadByte(99) == 0)
                    {
                        if (unchecked((sbyte)root.ReadByte(42)) >= unchecked((short)root.ReadWord(88)))
                        {
                            Store(address, Next(root));
                        }
                        else
                        {
                            Store(address, root.WriteByte(42, unchecked((byte)(root.ReadByte(42) + 1))).WriteByte(46, unchecked((byte)(root.ReadByte(46) + 1))).WriteByte(41, unchecked((byte)(root.ReadByte(41) - 1))));
                        }
                    }
                    break;
                case 6:
                    if (root.ReadWord(94) == 0)
                    {
                        Store(address, Next(root));
                    }
                    break;
                case 9:
                    Store(address, Next(root.WriteByte(38, (byte)(root.ReadByte(38) | 1))));
                    break;
                case 10:
                    break;
                default:
                    throw new NotSupportedException("Native Pain master left its original phase table.");
            }
            Store(address, hierarchy.Read(address).WriteWord(94, 0));
            ushort survivors = 0;
            for (int pool = 1; pool <= 2; pool++)
            {
                if (pool == 2)
                {
                    frame = frame with { PlayerQueueStack = NativeBattleStackBytes.Unknown("pain-player-queue", -0x94, 4) };
                }
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (current, child) =>
                {
                    hierarchy = current;
                    if (pool == 1)
                    {
                        Emitter(child);
                        int childResult = Finish(child);
                        return new(hierarchy, childResult);
                    }
                    Sync();
                    NativeBattlePain34LeafStep step = inputs.AdvancePlayer(frame, child);
                    Accept(step.Frame);
                    if (step.Complete)
                    {
                        hierarchy = hierarchy.Complete8DC530(child);
                    }
                    return new(hierarchy, step.Complete ? 2 : 0);
                });
                hierarchy = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            root = hierarchy.Read(address);
            Store(address, root.WriteWord(94, survivors).WriteWord(92, unchecked((ushort)(root.ReadWord(92) + 1))));
            int rootResult = Finish(address);
            return new(hierarchy, rootResult);
        });
        hierarchy = roots.Hierarchy;
        Sync();
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : Descriptor } };

        void Emitter(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, new ActorView(inputs.ReadModel), inputs.ReadPose);
            int actor = task.ReadByte(45);
            NativeBattlePoseSource pose = inputs.ReadPose(actor);
            NativeBattleSequenceState sequence = inputs.ReadModel(actor).SequenceState ?? throw new NotSupportedException("Native Pain target bounds require its current pose sequence.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, pose, NativeBattlePoseMatrices.ReadDecodedPose(sequence, pose));
            Store(address, task);
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    Spawn(0x8659D0, 0x15E7040, 2056, 0, 1);
                    Spawn(0x865DB0, 0x15EA2E8, 640, 1, 0);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (task.ReadWord(36) == 20)
                    {
                        uint root = task.ReadDword(16);
                        Store(root, hierarchy.Read(root).WriteByte(99, 0));
                        Store(address, Next(task));
                    }
                    break;
                case 2:
                    if (unchecked((short)task.ReadWord(36)) >= 20)
                    {
                        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(42)));
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                        int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Native Pain requires its current contact pointer.")) + unchecked((sbyte)task.ReadByte(43)));
                        Sync();
                        Accept(inputs.Contact(frame, contact));
                        task = hierarchy.Read(address);
                        Store(address, Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1))));
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new NotSupportedException("Native Pain emitter left its original phase table.");
            }
            if (hierarchy.Read(address).ReadWord(36) == 0)
            {
                inputs.PlaySound(resources.Sound);
            }
            void Spawn(uint callback, uint source, int size, ushort mode, ushort glow)
            {
                uint child = Append(2, callback, 2204, address);
                Store(child, hierarchy.Read(child).WriteWord(128, mode).WriteDword(116, source).WriteDword(120, unchecked((uint)(int)(short)size)).WriteWord(130, glow));
            }
        }
        uint Append(int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Pain exhausted its required task bank.");
            }
            hierarchy = appended.Hierarchy;
            return appended.Address;
        }
        int Finish(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
            Store(address, task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1))));
            if (complete)
            {
                hierarchy = hierarchy.Complete8DC530(address);
            }
            return complete ? 2 : 0;
        }
        void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task);
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        void Accept(NativeBattlePain34Frame updated)
        {
            frame = updated;
            hierarchy = new(updated.Effect.Pools);
        }
    }
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
    private static NativeBattlePain34State RequireState(NativeBattlePain34Frame frame)
    {
        if (frame.Effect.Binding is not { Effect: 34, Entry: 0x865600, Initializer: 0x8655E0 })
        {
            throw new InvalidDataException("Native Pain requires its original entry and initializer binding.");
        }
        NativeBattlePain34State state = frame.Memory.Pain34 ?? throw new NotSupportedException("Native Pain requires its retained module owner.");
        state.Validate();
        return state;
    }
    private static int ArenaOffset(uint address)
    {
        if (address < 0x20DFAB8 || address > 0x21DFAB8)
        {
            throw new InvalidDataException("Native Pain packet cursor leaves its physical arena.");
        }
        return checked((int)(address - 0x20DFAB8));
    }
    private sealed class ActorView(Func<int, NativeBattleGraphicsModel> read) : IReadOnlyList<NativeBattleGraphicsModel>
    {
        public int Count => 7;
        public NativeBattleGraphicsModel this[int index] => read(index);
        public IEnumerator<NativeBattleGraphicsModel> GetEnumerator() => Enumerable.Range(0, Count).Select(read).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}