namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTonberry90Controller
{
    public static NativeBattleTonberry90Frame Advance7624D0(NativeBattleTonberry90Frame frame, NativeBattleTonberry90Inputs inputs)
    {
        RequireBinding(frame.Effect);
        RequireState(frame);
        inputs.CameraMatrix.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Tonberry master requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy.Validate();
        if (hierarchy.Pools.Length != PoolLayout.Length || PoolLayout.Where((layout, index) => hierarchy.Pools[index].DescriptorAddress != layout.Descriptor || hierarchy.Pools[index].BankAddress != layout.Bank || hierarchy.Pools[index].Stride != layout.Stride || hierarchy.Pools[index].Slots.Length != layout.Count).Any())
        {
            throw new InvalidDataException("Native Tonberry master requires its original seven physical task banks.");
        }
        NativeBattleEffectHierarchyStep roots = hierarchy.Advance(0, (current, address) =>
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            if (task.Callback != 0x7624D0)
            {
                throw new NotSupportedException($"Native Tonberry master callback 0x{task.Callback:X8} is unsupported.");
            }
            NativeBattleTonberry90State state = State().WriteDword(0x25A4C08, 0x2793E58).WriteDword(0x259EFB8, 0x2793E58);
            bool odd = (task.ReadByte(0x5C) & 1) != 0;
            uint packet = state.ReadDword(odd ? 0x25A2CF4u : 0x25A2CF0u);
            uint work = state.ReadDword(odd ? 0x25A2B74u : 0x25A2B70u);
            state = state.WriteDword(0x259EEA8, packet).WriteDword(0x259EFB4, work);
            frame = frame with { Memory = frame.Memory with { Tonberry90 = state, SharedEffectView2793E58 = inputs.CameraMatrix }, Effect = frame.Effect with { DrawArenaOffset = ArenaOffset(packet, 0) } };
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
                        Store(address, Next(task));
                    }
                    break;
                case 3:
                    Store(address, task.WriteByte(0x63, 1));
                    Append(1, 0x762690, 0x58, address);
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
                    Accept(inputs.Execute(frame, new NativeBattleChoco97CommonTextureLoad(ArenaOffset(State().ReadDword(0x25A4C20), 8), unchecked(address + 0x62))));
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
                    throw new NotSupportedException($"Native Tonberry signed master phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
            Store(address, hierarchy.Read(address).WriteWord(0x5E, 0));
            ushort survivors = 0;
            foreach (int pool in new[] { 1, 2, 6, 5, 4, 3 })
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
            if (task.Callback == 0x762690)
            {
                Target(address);
                return Finish(address, true);
            }
            Sync();
            NativeBattleTonberry90TaskStep step = inputs.AdvanceLeaf(frame, address);
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
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Tonberry target bounds require its actual current pose state.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(sequence, source));
            Store(address, task);
            switch (unchecked((sbyte)task.ReadByte(0x29)))
            {
                case 0:
                    Sync();
                    Accept(inputs.InitializeTarget(frame, address));
                    Append(6, 0x762850, 0x30, address);
                    Append(2, 0x762C60, 0x48, address);
                    Store(address, Next(hierarchy.Read(address)));
                    break;
                case 1:
                    if (State().ReadWord(unchecked(Director() + 0x48)) == 1)
                    {
                        int header = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                        if ((uint)header >= inputs.Packets.Headers.Length)
                        {
                            throw new NotSupportedException("Native Tonberry contact header leaves its current addressed owner.");
                        }
                        int contact = checked((inputs.Packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Native Tonberry target contact pointer is unknown.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                        _ = inputs.Packets.ReadContact(contact);
                        Sync();
                        Accept(inputs.ApplyContact(frame, contact));
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
                    throw new NotSupportedException($"Native Tonberry signed target phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
            }
        }

        uint Director() => State().DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry director pointer is unknown.");
        NativeBattleTonberry90State State() => RequireState(frame);
        void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task);
        void Sync() => frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        void Accept(NativeBattleTonberry90Frame updated)
        {
            frame = updated;
            hierarchy = new(frame.Effect.Pools);
        }
        void Append(int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, header, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, header, target));
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Tonberry exhausted a required task bank.");
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
