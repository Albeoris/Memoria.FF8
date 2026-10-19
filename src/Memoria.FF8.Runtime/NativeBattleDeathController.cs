using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    public const uint RootDescriptor = 0x273B0B8;
    public const uint RootBank = 0x273AFF0;

    public static NativeBattleDeathFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleDeathResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 14, Entry: 0x8B68C0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Death requires its selected original entry and action context.");
        }
        NativeBattleDeathState state = memory.Death14 ?? throw new NotSupportedException("Native Death requires its explicit retained process owner.");
        if (state.PendingColdSources)
        {
            if (state.AuraActors is not null || state.DarkActors is not null)
            {
                throw new InvalidDataException("Native Death pending cold sources already contain mutable bytes.");
            }
            state = state with { AuraActors = resources.AuraActors, DarkActors = resources.DarkActors, PendingColdSources = false };
        }
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x273AE80)?.ArenaOffset ?? throw new NotSupportedException("Native Death requires its executed initializer allocation.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 0x265F0)
        {
            throw new InvalidDataException("Native Death auxiliary allocations exceed the selected file arena.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x2741878, 0x2741888, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x273AFC0, 0x273AEE8, 0x48, 3),
            NativeBattleEffectTaskPool.Initialize(0x2742810, 0x2742450, 0x140, 3),
            NativeBattleEffectTaskPool.Initialize(0x27415D8, 0x2740B48, 0x2A4, 4),
            NativeBattleEffectTaskPool.Initialize(0x273B0C8, 0x273B0D8, 0x70, 10),
            NativeBattleEffectTaskPool.Initialize(0x273B540, 0x273D490, 0x118, 50),
            NativeBattleEffectTaskPool.Initialize(0x2741868, 0x27415E8, 0x40, 10)
        ]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x8B6A40, 0x64);
        hierarchy = appended.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Death requires its actual first contact.");
        byte maximum = Math.Max(unchecked((byte)(header.ReadByte(16) - 1)), header.ReadByte(17));
        NativeBattleModulePoolTask root = hierarchy.Read(appended.Address).WriteDword(12, 0x1D99A78).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17)).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(appended.Address, root);
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Death requires its current texture queue.");
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Death requires its loaded file arena."), source);
            memory = memory with { Textures = textures.Register5099A0(resources.Animation) };
        }
        state = state with { Source = source };
        state = state with { Module = state.Module.Write(checked((int)(0x274286Cu - ModuleBase)), new byte[4]).Write(checked((int)(0x273B53Cu - ModuleBase)), new byte[4]) };
        return new(memory with { Death14 = state }, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = RootDescriptor }, random);
    }

    public static NativeBattleDeathFrame Advance(NativeBattleDeathFrame frame, NativeBattleDeathResources resources, NativeBattleDeathInputs inputs)
    {
        return frame.Effect.ReturnedDescriptor == 0 ? frame : new Execution(frame, resources, inputs).Advance();
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint pointer, byte value)
    {
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        uint address = checked(pointer - 0x62);
        NativeBattleModulePoolTask root = hierarchy.Read(address);
        if (root.Callback != 0x8B6A40)
        {
            throw new InvalidDataException("Native Death completion is not its physical root BYTE62.");
        }
        return effect with { Pools = hierarchy.Write(address, root.WriteByte(0x62, value)).Pools };
    }

    internal const uint ModuleBase = 0x273AE80;

    private sealed partial class Execution
    {
        private NativeBattleDeathFrame current;
        private readonly NativeBattleDeathResources resources;
        private readonly NativeBattleDeathInputs inputs;
        private NativeBattleEffectHierarchy hierarchy;

        internal Execution(NativeBattleDeathFrame frame, NativeBattleDeathResources resources, NativeBattleDeathInputs inputs)
        {
            current = frame;
            this.resources = resources;
            this.inputs = inputs;
            hierarchy = new(frame.Effect.Pools);
        }

        internal NativeBattleDeathFrame Advance()
        {
            NativeBattleEffectHierarchyStep step = hierarchy.Advance(0, (active, address) =>
            {
                hierarchy = active;
                NativeBattleModulePoolTask root = Read(address);
                int source = State.Source ?? throw new NotSupportedException("Native Death lost its initialized file pointer.");
                SetState(State with { Cursor = source + ((root.ReadWord(0x5C) & 1) != 0 ? 0 : 0xE000), PassEmitters = 0, PassGeometry = 0 });
                root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(root, current.Effect.Parameters!, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
                Store(address, root);
                switch (Stage(root))
                {
                    case 0:
                    case 1:
                    case 5:
                        Store(address, Next(root));
                        break;
                    case 2:
                        if (root.ReadByte(0x28) == 0)
                        {
                            int emitters = source + 0x1C000;
                            int geometry = emitters + 0x870;
                            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Death requires its actor arena.");
                            SetState(State with { Emitters = emitters, Geometry = geometry, EmitterScan = 0, GeometryScan = 0 });
                            current = current with { Memory = current.Memory with { Arena = arena.Write(emitters, new byte[0x870]).Write(geometry, new byte[0x9D80]) } };
                            Store(address, Next(root));
                        }
                        break;
                    case 3:
                        Store(address, Next(root.WriteByte(0x63, 1)));
                        Append(1, 0x8B6C90, 0x58, address);
                        break;
                    case 4:
                        if (root.ReadByte(0x63) == 0)
                        {
                            Store(address, unchecked((sbyte)root.ReadByte(0x2A)) < S(root, 0x58) ? root.WriteByte(0x2A, unchecked((byte)(root.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(root.ReadByte(0x2E) + 1))).WriteByte(0x29, 3) : Next(root));
                        }
                        break;
                    case 6:
                        if (root.ReadWord(0x5E) == 0)
                        {
                            Store(address, Next(root));
                        }
                        break;
                    case 7:
                        Store(address, Next(root));
                        Emit(new NativeBattleDeathTextureLoad(source, address + 0x62));
                        break;
                    case 8:
                        if (root.ReadByte(0x62) != 0)
                        {
                            Store(address, Next(root));
                        }
                        break;
                    case 9:
                        Store(address, Next(root.WriteByte(0x26, (byte)(root.ReadByte(0x26) | 1))));
                        break;
                    case 10:
                        break;
                    default:
                        throw new NotSupportedException("Native Death master reached an unproved state.");
                }
                Store(address, Read(address).WriteWord(0x5E, 0));
                int survivors = 0;
                for (int pool = 1; pool < 8; pool++)
                {
                    NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (bank, child) =>
                    {
                        hierarchy = bank;
                        Run(child);
                        int result = Finish(child);
                        return new(hierarchy, result);
                    });
                    hierarchy = children.Hierarchy;
                    survivors = unchecked(survivors + children.Survivors);
                }
                root = Read(address).WriteWord(0x5E, unchecked((ushort)survivors)).WriteWord(0x5C, unchecked((ushort)(Read(address).ReadWord(0x5C) + 1)));
                Store(address, root);
                int result = Finish(address);
                return new(hierarchy, result);
            });
            hierarchy = step.Hierarchy;
            return current with { Effect = current.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = step.Survivors == 0 ? 0u : RootDescriptor } };
        }

        private NativeBattleDeathState State => current.Memory.Death14 ?? throw new NotSupportedException("Native Death lost its retained process owner.");
        private void SetState(NativeBattleDeathState value) => current = current with { Memory = current.Memory with { Death14 = value } };
        private NativeBattleModulePoolTask Read(uint address) => hierarchy.Read(address);
        private void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task.WriteByte(0x28, hierarchy.Read(address).ReadByte(0x28)) with { Next = hierarchy.Read(address).Next });
        private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
        private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        private uint Append(int pool, uint callback, ushort size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, Select);
            hierarchy = appended.Hierarchy;
            return appended.Address;
        }
        private NativeBattleEffectTaskActors Select(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(current.Effect.Parameters!, inputs.Packets, pointer, group, target);
        private void Emit(NativeBattleDeathOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(current.Effect.Pools);
        }
        private int Finish(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            Store(address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
            if ((task.ReadByte(0x26) & 1) == 0 || task.ReadByte(0x28) != 0)
            {
                return 0;
            }
            hierarchy = hierarchy.Complete8DC530(address);
            return 2;
        }
        private ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(State.Module.Read(checked((int)(address - ModuleBase)), 2));
        private void Word(uint address, int value) => SetState(State with { Module = State.Module.Write(checked((int)(address - ModuleBase)), BitConverter.GetBytes(unchecked((ushort)value))) });
        private uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(State.Module.Read(checked((int)(address - ModuleBase)), 4));
        private void Dword(uint address, uint value) => SetState(State with { Module = State.Module.Write(checked((int)(address - ModuleBase)), BitConverter.GetBytes(value)) });
        private ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
    }
}


