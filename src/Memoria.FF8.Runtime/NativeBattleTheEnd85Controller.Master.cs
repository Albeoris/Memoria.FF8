namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTheEnd85Controller
{
    public static NativeBattleTheEnd85Frame Advance784630(NativeBattleTheEnd85Frame frame, NativeBattleTheEnd85Inputs inputs)
    {
        RequireBinding(frame.Effect);
        RequireState(frame);
        inputs.CameraMatrix.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native The End master requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        if (hierarchy.Pools.Length != PoolLayout.Length || PoolLayout.Where((layout, index) => hierarchy.Pools[index].DescriptorAddress != layout.Descriptor || hierarchy.Pools[index].BankAddress != layout.Bank || hierarchy.Pools[index].Stride != layout.Stride || hierarchy.Pools[index].Slots.Length != layout.Count).Any())
        {
            throw new InvalidDataException("Native The End master requires its original nine physical task banks.");
        }
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x784630)
            {
                throw new NotSupportedException($"Native The End master callback 0x{task.Callback:X8} is unsupported.");
            }
            NativeBattleTheEnd85State state = State().WriteDword(0x25C21CC, 0x2793E58).WriteDword(0x25BC4A0, 0x2793E58);
            bool odd = (task.ReadByte(0x5C) & 1) != 0;
            uint packet = state.Dword(odd ? 0x25C1B5Cu : 0x25C1B58u);
            uint work = state.Dword(odd ? 0x25C0F54u : 0x25C0F50u);
            state = state.WriteDword(0x25BC394, packet).WriteDword(0x25BC49C, work);
            frame = frame with { Memory = frame.Memory with { TheEnd85 = state, SharedEffectView2793E58 = inputs.CameraMatrix }, Effect = frame.Effect with { DrawArenaOffset = ArenaOffset(packet, 0) } };
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
                        NativeBattleTheEnd85State currentState = State();
                        uint first = currentState.Dword(0x25C21D8);
                        uint second = unchecked(first + 1944);
                        currentState = currentState.WriteDword(0x25C1CB8, first).WriteDword(0x25C21D0, second).WriteDword(0x25C21D8, unchecked(second + 40320));
                        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native The End initialization requires its current work arena.");
                        arena = arena.Write(ArenaOffset(first, 1944), new byte[1944]).Write(ArenaOffset(second, 40320), new byte[40320]);
                        foreach (uint word in new uint[] { 0x25BC890, 0x25C1CC4, 0x25C1C54, 0x25BC398 })
                        {
                            currentState = currentState.Write(word, new byte[2]);
                        }
                        frame = frame with { Memory = frame.Memory with { TheEnd85 = currentState, Arena = arena } };
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, task.WriteByte(0x63, 1));
                    Append(1, 0x784890, 0x58, address);
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
                    Accept(inputs.Execute(frame, new NativeBattleChoco97CommonTextureLoad(ArenaOffset(State().Dword(0x25BC390), 8), unchecked(address + 0x62))));
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
                    throw new NotSupportedException($"Native The End signed master phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
            Store(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            frame = frame with { Memory = frame.Memory with { TheEnd85 = State().Write(0x25C21B0, new byte[2]).Write(0x25C1CB4, new byte[2]) } };
            ushort survivors = 0;
            foreach (int pool in new[] { 1, 2, 8, 3, 5, 6, 4, 7 })
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
            if (task.Callback == 0x784890)
            {
                Target(address);
                return Finish(address, true);
            }
            Sync();
            NativeBattleTheEnd85LeafStep step = inputs.AdvanceLeaf(frame, address);
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
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native The End target bounds require its actual current pose state.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(sequence, source));
            Store(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Sync();
                    Accept(inputs.InitializeTarget(frame, address));
                    Append(8, 0x784A60, 0x30, address);
                    Append(2, 0x784E30, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (State().Word(unchecked(Director() + 0x48)) == 1)
                    {
                        int contactOrdinal = 0;
                        while (true)
                        {
                            task = hierarchy.Read(address);
                            int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                            if ((uint)headerIndex >= inputs.Packets.Headers.Length)
                            {
                                throw new NotSupportedException("Native The End contact header leaves its current addressed owner.");
                            }
                            NativeBattleActionHeaderSlot header = inputs.Packets.Headers[headerIndex];
                            if (contactOrdinal >= header.ReadByte(16))
                            {
                                break;
                            }
                            int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Native The End target contact pointer is unknown.")) + contactOrdinal);
                            _ = inputs.Packets.ReadContact(contact);
                            Sync();
                            Accept(inputs.ApplyContact(frame, contact));
                            contactOrdinal++;
                        }
                        Store(address, Next(hierarchy.Read(address)));
                    }
                    break;
                case 2:
                    if (task.ReadByte(0x28) == 0 || State().Word(unchecked(Director() + 0x4C)) == 1)
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
                    throw new NotSupportedException($"Native The End signed target phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
        }

        uint Director() => inputs.DirectorAddress;
        NativeBattleTheEnd85State State() => RequireState(frame);
        void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task);
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        void Accept(NativeBattleTheEnd85Frame updated)
        {
            frame = updated;
            hierarchy = new(frame.Effect.Pools);
        }
        void Append(int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native The End exhausted a required task bank.");
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
