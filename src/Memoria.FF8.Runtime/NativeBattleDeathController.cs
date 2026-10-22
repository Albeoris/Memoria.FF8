using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    public const uint RootDescriptor = 0x273B0B8;
    public const uint RootBank = 0x273AFF0;

    public static NativeBattleDeathFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleDeathResources resources, NativeCrtRandomState random)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect.Binding.Effect);
        if (effect.Binding.Entry != profile.Entry || resources.Effect != profile.Effect || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Death requires its selected original entry and action context.");
        }
        NativeBattleDeathState state = profile.RequireState(memory);
        if (state.PendingColdSources)
        {
            if (state.AuraActors is not null || state.DarkActors is not null)
            {
                throw new InvalidDataException("Native Death pending cold sources already contain mutable bytes.");
            }
            state = state with { AuraActors = resources.AuraActors, DarkActors = resources.DarkActors, PendingColdSources = false };
        }
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.ModuleBase)?.ArenaOffset ?? throw new NotSupportedException("Native Death requires its executed initializer allocation.");
        if (source < 0 || source > NativeBattleEffectArena.Length - 0x265F0)
        {
            throw new InvalidDataException("Native Death auxiliary allocations exceed the selected file arena.");
        }
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, profile.Callback(0x8B6A40), 0x64);
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
        state = state with { Module = state.Module.Write(profile.ModuleOffset(0x274286C), new byte[4]).Write(profile.ModuleOffset(0x273B53C), new byte[4]) };
        return new(profile.WithState(memory, state), effect with { Pools = hierarchy.Pools, ReturnedDescriptor = profile.RootDescriptor }, random);
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
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect.Binding.Effect);
        if (root.Callback != profile.Callback(0x8B6A40))
        {
            throw new InvalidDataException("Native Death completion is not its physical root BYTE62.");
        }
        return effect with { Pools = hierarchy.Write(address, root.WriteByte(0x62, value)).Pools };
    }

    internal static NativeBattleDeathFrame InitializeDirector(NativeBattleDeathFrame frame, uint address, NativeBattleDeathResources resources, Func<NativeBattleDeathFrame, NativeBattleDeathOperation, NativeBattleDeathFrame> execute)
    {
        NativeBattleDeathFamilyProfile profile = resources.Profile;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (frame.Effect.Binding.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry || task.Callback != profile.Callback(0x8B6E30))
        {
            throw new InvalidDataException("Native Death director setup requires its actual module and callback.");
        }
        ushort delay = 1;
        if (task.ReadByte(0x2E) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native Death requires its reaper TIM queue.");
            (textures, _) = textures.EnqueueTim505E30(resources.FirstTim);
            (textures, _) = textures.EnqueueTim505E30(resources.SecondTim);
            frame = frame with { Memory = frame.Memory with { Textures = textures } };
            if (profile.SingleSelectedTarget)
            {
                NativeBattleModuleResource caster = resources.CasterResource ?? throw new NotSupportedException("LV?Death requires its original caster resource.");
                if (caster.Address != profile.CasterDirectory)
                {
                    throw new InvalidDataException("LV?Death caster binding selected another original resource.");
                }
                byte actor = task.ReadByte(0x2C);
                frame = execute(frame, new NativeBattleDeathBindCaster(actor, profile.SavedCasterRow!.Value, caster.Address));
                frame = execute(frame, new NativeBattleDeathRingSequence(actor, 1));
                delay = 12;
            }
        }
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address);
        task = task.WriteWord(0x44, delay).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
    }

    internal const uint ModuleBase = 0x273AE80;

    private sealed partial class Execution
    {
        private NativeBattleDeathFrame current;
        private readonly NativeBattleDeathResources resources;
        private readonly NativeBattleDeathFamilyProfile profile;
        private readonly NativeBattleDeathInputs inputs;
        private NativeBattleEffectHierarchy hierarchy;

        internal Execution(NativeBattleDeathFrame frame, NativeBattleDeathResources resources, NativeBattleDeathInputs inputs)
        {
            current = frame;
            this.resources = resources;
            profile = NativeBattleDeathFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
            if (resources.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry)
            {
                throw new InvalidDataException("Native Death family resources do not match the selected original module.");
            }
            this.inputs = inputs;
            hierarchy = new(frame.Effect.Pools);
        }

        internal NativeBattleDeathFrame Advance()
        {
            NativeBattleEffectHierarchyStep step = hierarchy.Advance(0, (active, address) =>
            {
                hierarchy = active;
                NativeBattleModulePoolTask root = Read(address);
                NativeBattlePackedMatrix32 camera = inputs.ReadPackedCamera?.Invoke() ?? throw new NotSupportedException("Native Death requires its actual packed camera producer.");
                current = NativeBattleDeathNodeDrawing.PublishCamera(current, camera);
                int source = State.Source ?? throw new NotSupportedException("Native Death lost its initialized file pointer.");
                SetState(State with { Cursor = source + ((root.ReadWord(0x5C) & 1) != 0 ? 0 : 0xE000), PassEmitters = 0, PassGeometry = 0 });
                root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(root, current.Effect.Parameters!, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource, producer => Emit(new NativeBattleDeathObserveAnchor(producer)));
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
                            Store(address, profile.AdvanceTargetLoop(root));
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
            return current with { Effect = current.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = step.Survivors == 0 ? 0u : profile.RootDescriptor } };
        }

        private NativeBattleDeathState State => profile.RequireState(current.Memory);
        private void SetState(NativeBattleDeathState value) => current = current with { Memory = profile.WithState(current.Memory, value) };
        private NativeBattleModulePoolTask Read(uint address) => hierarchy.Read(address);
        private void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task.WriteByte(0x28, hierarchy.Read(address).ReadByte(0x28)) with { Next = hierarchy.Read(address).Next });
        private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
        private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        private uint Append(int pool, uint callback, ushort size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, profile.Callback(callback), size, parent, Select);
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
        private ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(State.Module.Read(profile.ModuleOffset(address), 2));
        private void Word(uint address, int value) => SetState(State with { Module = State.Module.Write(profile.ModuleOffset(address), BitConverter.GetBytes(unchecked((ushort)value))) });
        private uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(State.Module.Read(profile.ModuleOffset(address), 4));
        private void Dword(uint address, uint value) => SetState(State with { Module = State.Module.Write(profile.ModuleOffset(address), BitConverter.GetBytes(value)) });
        private ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
    }
}


