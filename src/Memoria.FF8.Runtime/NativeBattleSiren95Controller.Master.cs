namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSiren95Controller
{
    public static NativeBattleSiren95Frame Advance739F40(NativeBattleSiren95Frame frame, NativeBattleSiren95Inputs inputs)
    {
        RequireBinding(frame.Effect);
        RequireState(frame);
        inputs.CameraMatrix.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Siren master requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        if (hierarchy.Pools.Length != PoolLayout.Length || PoolLayout.Where((layout, index) => hierarchy.Pools[index].DescriptorAddress != layout.Descriptor || hierarchy.Pools[index].BankAddress != layout.Bank || hierarchy.Pools[index].Stride != layout.Stride || hierarchy.Pools[index].Slots.Length != layout.Count).Any())
        {
            throw new InvalidDataException("Native Siren master requires its original ten physical task banks.");
        }
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x739F40)
            {
                throw new NotSupportedException($"Native Siren master callback 0x{task.Callback:X8} is unsupported.");
            }
            NativeBattleSiren95State state = State().WriteDword(0x258FB68, 0x2793E58).WriteDword(0x257F9B0, 0x2793E58);
            bool odd = (task.ReadByte(0x5C) & 1) != 0;
            uint packet = state.ReadDword(odd ? 0x258BFA4u : 0x258BFA0u);
            uint work = state.ReadDword(odd ? 0x258A024u : 0x258A020u);
            state = state.WriteDword(0x257F8A4, packet).WriteDword(0x257F9AC, work);
            frame = frame with { Memory = frame.Memory with { Siren95 = state, SharedEffectView2793E58 = inputs.CameraMatrix }, Effect = frame.Effect with { DrawArenaOffset = ArenaOffset(packet, 0) } };
            task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, parameters, new ActorView(inputs.ReadModel), inputs.ReadPoseSource);
            Store(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                case 1:
                case 5:
                    Store(address, Next(task));
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0)
                    {
                        NativeBattleSiren95State currentState = State();
                        uint first = currentState.ReadDword(0x258FB74);
                        uint second = unchecked(first + 0x10E0);
                        currentState = currentState.WriteDword(0x258EC54, first).WriteDword(0x258FB6C, second).WriteDword(0x258FB74, unchecked(second + 0xC240));
                        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Siren initialization requires its current work arena.");
                        arena = arena.Write(ArenaOffset(first, 0x10E0), new byte[0x10E0]).Write(ArenaOffset(second, 0xC240), new byte[0xC240]);
                        foreach (uint word in new uint[] { 0x2585AE0, 0x258EDD0, 0x258E89C, 0x257F8A8 })
                        {
                            currentState = currentState.Write(word, new byte[2]);
                        }
                        frame = frame with { Memory = frame.Memory with { Siren95 = currentState, Arena = arena } };
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, task.WriteByte(0x63, 1));
                    Append(1, 0x73A1A0, 0x58, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 4:
                    if (task.ReadByte(0x63) == 0)
                    {
                        if (unchecked((sbyte)task.ReadByte(0x2A)) < task.ReadWord(0x58))
                        {
                            task = task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                            Store(address, task);
                        }
                        else
                        {
                            Store(address, Next(task));
                        }
                    }
                    break;
                case 6:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 7:
                    Sync();
                    Accept(inputs.Execute(frame, new NativeBattleChoco97CommonTextureLoad(ArenaOffset(State().ReadDword(0x257F8A0), 8), unchecked(address + 0x62))));
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 8:
                    if (task.ReadByte(0x62) != 0)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 9:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    break;
                case 10:
                    break;
                default:
                    throw new NotSupportedException($"Native Siren signed master phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
            Store(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            frame = frame with { Memory = frame.Memory with { Siren95 = State().Write(0x258FB40, new byte[2]).Write(0x258EC50, new byte[2]) } };
            ushort survivors = 0;
            foreach (int pool in new[] { 1, 2, 9, 8, 3, 4, 5, 6, 7 })
            {
                NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (active, child) =>
                {
                    hierarchy = active;
                    int result = Run(child);
                    return new(hierarchy, result);
                });
                hierarchy = children.Hierarchy;
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = hierarchy.Read(address);
            Store(address, task.WriteWord(0x5E, survivors).WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
            int completion = Finish(address, true);
            return new(hierarchy, completion);
        });
        hierarchy = roots.Hierarchy;
        Sync();
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : MainDescriptor } };

        int Run(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback == 0x73A1A0)
            {
                Target(address);
                return Finish(address, true);
            }
            Sync();
            NativeBattleSiren95TaskStep step = inputs.AdvanceLeaf(frame, address);
            Accept(step.Frame);
            if (step.Complete)
            {
                hierarchy = hierarchy.Complete8DC530(address);
                return 2;
            }
            return 0;
        }

        void Target(uint address)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, new ActorView(inputs.ReadModel), inputs.ReadPoseSource);
            NativeBattlePoseSource source = inputs.ReadPoseSource(task.ReadByte(0x2D));
            NativeBattleGraphicsModel model = inputs.ReadModel(task.ReadByte(0x2D));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Siren target bounds require its actual current pose state.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(sequence, source));
            Store(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Sync();
                    Accept(inputs.InitializeTarget(frame, address));
                    Append(9, 0x73A380, 0x30, address);
                    Append(2, 0x73A7B0, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (State().ReadWord(unchecked(Director() + 0x48)) == 1)
                    {
                        int contactOrdinal = 0;
                        while (true)
                        {
                            task = hierarchy.Read(address);
                            int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                            if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                            {
                                throw new NotSupportedException("Native Siren contact header leaves its current addressed owner.");
                            }
                            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                            if (contactOrdinal >= header.ReadByte(16))
                            {
                                break;
                            }
                            int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Native Siren target contact pointer is unknown.")) + contactOrdinal);
                            _ = inputs.Packets.ReadContact(contact);
                            Sync();
                            Accept(inputs.ApplyContact(frame, contact));
                            contactOrdinal++;
                        }
                        Store(address, Next(hierarchy.Read(address)));
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || State().ReadWord(unchecked(Director() + 0x4C)) == 1)
                    {
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    uint root = task.ReadDword(0x10);
                    Store(root, hierarchy.Read(root).WriteByte(0x63, 0));
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException($"Native Siren signed target phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
        }

        uint Director() => State().DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren director pointer is unknown.");
        NativeBattleSiren95State State() => RequireState(frame);
        void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task);
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        void Accept(NativeBattleSiren95Frame updated)
        {
            frame = updated;
            hierarchy = new(frame.Effect.Pools);
        }
        void Append(int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Siren exhausted a required task bank.");
            }
            hierarchy = appended.Hierarchy;
        }
        int Finish(uint address, bool increment)
        {
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (increment)
            {
                task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
                Store(address, task);
            }
            if ((task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0)
            {
                hierarchy = hierarchy.Complete8DC530(address);
                return 2;
            }
            return 0;
        }
    }

    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

    private sealed class ActorView(Func<int, NativeBattleGraphicsModel> read) : IReadOnlyList<NativeBattleGraphicsModel>
    {
        public int Count => 7;
        public NativeBattleGraphicsModel this[int index] => (uint)index < Count ? read(index) : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<NativeBattleGraphicsModel> GetEnumerator() => Enumerable.Range(0, Count).Select(read).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
