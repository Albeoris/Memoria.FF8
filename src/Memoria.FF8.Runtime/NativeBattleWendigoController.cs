using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    public static uint ModuleBase(bool shoot) => shoot ? 0x2659108u : 0x267B858u;
    public static int ModuleLength(bool shoot) => shoot ? 0x21F1C : 0x1D260;
    public static uint BallAddress(bool shoot) => shoot ? 0x2659118u : 0x267B868u;

    public static NativeBattleWendigoFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleWendigoResources resources, NativeCrtRandomState random)
    {
        bool shoot = resources.Effect == 42;
        if (resources.Effect is not (41 or 42) || effect.RequestedEffect != resources.Effect || effect.Binding.Entry != (shoot ? 0x83ECC0u : 0x8465C0u) || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Wendigo ball effects require their original selected entry and resources.");
        }
        NativeBattleWendigoState state = (shoot ? memory.Shoot42 : memory.Dribble41) ?? throw new NotSupportedException("Native Wendigo requires its explicit retained executable module owner.");
        if (state.PendingColdSources)
        {
            if (state.ActorBank is not null)
            {
                throw new InvalidDataException("Native Wendigo pending cold source already contains mutable bytes.");
            }
            state = state with { ActorBank = resources.ActorBank, PendingColdSources = false };
        }
        NativeBattleEffectTaskPool[] pools = [
            NativeBattleEffectTaskPool.Initialize(shoot ? 0x266CB60u : 0x268F288u, shoot ? 0x266CA98u : 0x268F1C0u, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(shoot ? 0x267AAF0u : 0x26988E8u, shoot ? 0x267AB00u : 0x26988F8u, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(shoot ? 0x2672DE0u : 0x2690BD8u, shoot ? 0x26725F0u : 0x26903E8u, 0x2A4, 3),
            NativeBattleEffectTaskPool.Initialize(shoot ? 0x266CB70u : 0x268F298u, shoot ? 0x266CB80u : 0x268F2A8u, shoot ? (ushort)0x1FC : (ushort)0xDC, shoot ? (ushort)40 : (ushort)20)
        ];
        NativeBattleEffectHierarchy hierarchy = new(pools.ToImmutableArray());
        if (shoot)
        {
            hierarchy = hierarchy with { Pools = hierarchy.Pools.Add(NativeBattleEffectTaskPool.Initialize(0x26725D8, 0x2671AE8, 0x118, 10)) };
        }
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, shoot ? 0x83EE00u : 0x8466F0u, 0x64);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Wendigo requires its actual first contact.");
        byte maximum = unchecked((byte)(header.ReadByte(16) - 1));
        if (maximum < header.ReadByte(17))
        {
            maximum = header.ReadByte(17);
        }
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, 0, 0);
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(12, 0x1D99A78).WriteByte(0x2C, actors.Actor).WriteByte(0x2D, actors.Target).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17)).WriteByte(0x2F, maximum);
        hierarchy = hierarchy.Write(root.Address, task);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == ModuleBase(shoot))?.ArenaOffset ?? throw new NotSupportedException("Native Wendigo requires its executed initializer file pointer.");
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Wendigo requires its retained texture queue.");
            textures = textures.Register5099A0(resources.Camera);
            (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Wendigo requires its loaded file arena."), source);
            memory = memory with { Textures = textures };
        }
        state = state with { Source = source };
        memory = shoot ? memory with { Shoot42 = state } : memory with { Dribble41 = state };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = pools[0].DescriptorAddress }, random);
    }

    public static NativeBattleWendigoFrame Advance(NativeBattleWendigoFrame frame, NativeBattleWendigoResources resources, NativeBattleWendigoInputs inputs) => frame.Effect.ReturnedDescriptor == 0 ? frame : new Execution(frame, resources, inputs).Advance();

    private sealed partial class Execution
    {
        private NativeBattleWendigoFrame current;
        private readonly NativeBattleWendigoResources resources;
        private readonly NativeBattleWendigoInputs inputs;
        private readonly bool shoot;
        private NativeBattleEffectHierarchy hierarchy;
        private uint Ball => BallAddress(shoot);
        private uint Base => ModuleBase(shoot);
        private int TaskSize => shoot ? 0x1FC : 0xDC;
        private int Caster => current.Effect.Parameters!.Actor78;
        private NativeBattleWendigoState State => (shoot ? current.Memory.Shoot42 : current.Memory.Dribble41) ?? throw new NotSupportedException("Native Wendigo lost its executable owner.");

        internal Execution(NativeBattleWendigoFrame frame, NativeBattleWendigoResources resources, NativeBattleWendigoInputs inputs)
        {
            current = frame;
            this.resources = resources;
            this.inputs = inputs;
            shoot = resources.Effect == 42;
            hierarchy = new(frame.Effect.Pools);
        }

        internal NativeBattleWendigoFrame Advance()
        {
            NativeBattleEffectHierarchyStep advanced = hierarchy.Advance(0, (bank, address) =>
            {
                hierarchy = bank;
                NativeBattleModulePoolTask root = Read(address);
                int source = State.Source ?? throw new NotSupportedException("Native Wendigo lost its loaded file base.");
                int half = shoot ? 0x6000 : 0x5000;
                bool odd = (root.ReadWord(0x5C) & 1) != 0;
                SetState(State with { PrimaryCursor = source + (odd ? 0 : half), SecondaryCursor = source + (odd ? half : 2 * half), PassEmitters = 0, PassGeometry = 0 });
                root = NativeBattleEffectModelHelpers.UpdateCaster8DC740(root, current.Effect.Parameters!, Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
                Store(address, root);
                switch (Stage(root))
                {
                    case 0:
                    case 1:
                    case 5:
                    case 7:
                    case 8:
                        Store(address, Next(root));
                        break;
                    case 2:
                        if (root.ReadByte(0x28) == 0)
                        {
                            int emitterBytes = shoot ? 0x438 : 0x870;
                            int geometryBytes = shoot ? 0x4EC0 : 0x8340;
                            int emitters = source + 2 * half;
                            int geometry = emitters + emitterBytes;
                            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Wendigo requires its cleared actor allocations.");
                            current = current with { Memory = current.Memory with { Arena = arena.Write(emitters, new byte[emitterBytes]).Write(geometry, new byte[geometryBytes]) } };
                            SetState(State with { Emitters = emitters, Geometry = geometry, EmitterScan = 0, GeometryScan = 0 });
                            Store(address, Next(root));
                        }
                        break;
                    case 3:
                        Store(address, Next(root.WriteByte(0x63, 1)));
                        Append(1, shoot ? 0x83F020u : 0x846900u, 0x58, address);
                        break;
                    case 4:
                        if (root.ReadByte(0x63) == 0)
                        {
                            Store(address, unchecked((sbyte)root.ReadByte(0x2A)) < Signed(root, 0x58) ? root.WriteByte(0x2A, unchecked((byte)(root.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(root.ReadByte(0x2E) + 1))).WriteByte(0x29, 3) : Next(root));
                        }
                        break;
                    case 6:
                        if (root.ReadWord(0x5E) == 0)
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
                        throw new NotSupportedException("Native Wendigo master reached an unproved state.");
                }
                int survivors = 0;
                for (int pool = 1; pool < hierarchy.Pools.Length; pool++)
                {
                    NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, (active, child) =>
                    {
                        hierarchy = active;
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
            hierarchy = advanced.Hierarchy;
            return current with { Effect = current.Effect with { Pools = hierarchy.Pools, ReturnedDescriptor = advanced.Survivors == 0 ? 0u : hierarchy.Pools[0].DescriptorAddress } };
        }

        private NativeBattleModulePoolTask Read(uint address) => hierarchy.Read(address);
        private void Store(uint address, NativeBattleModulePoolTask task) => hierarchy = hierarchy.Write(address, task.WriteByte(0x28, hierarchy.Read(address).ReadByte(0x28)).SetNext(hierarchy.Read(address).Next));
        private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
        private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        private uint Append(int pool, uint callback, int size, uint parent)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(current.Effect.Parameters!, inputs.Packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            return appended.Address;
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
        private void Emit(NativeBattleWendigoOperation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(current.Effect.Pools);
        }
        private void SetState(NativeBattleWendigoState value) => current = current with { Memory = shoot ? current.Memory with { Shoot42 = value } : current.Memory with { Dribble41 = value } };
        private short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(State.Module.Read(checked((int)(address - Base)), 2));
        private uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(State.Module.Read(checked((int)(address - Base)), 4));
        private void Word(uint address, int value) => SetState(State with { Module = State.Module.Write(checked((int)(address - Base)), BitConverter.GetBytes(unchecked((ushort)value))) });
        private void Dword(uint address, uint value) => SetState(State with { Module = State.Module.Write(checked((int)(address - Base)), BitConverter.GetBytes(value)) });
        private ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
        private NativeBattleVector Bone(int actor, byte bone, short fraction = 0) => NativeBattlePoseMatrices.ReadBoneLazy(inputs.ReadModel(actor), () => inputs.ReadPoseSource(actor), bone, fraction);
        private NativeBattleVector Position(uint address) => new(Word(address), Word(address + 2), Word(address + 4));
        private void Position(uint address, NativeBattleVector position)
        {
            Word(address, position.X);
            Word(address + 2, position.Y);
            Word(address + 4, position.Z);
        }
        private NativeBattleActionMatrix Matrix(uint address)
        {
            short[] rotation = new short[9];
            for (int index = 0; index < 9; index++)
            {
                rotation[index] = Word(address + (uint)(index * 2));
            }
            return new(rotation.ToImmutableArray(), unchecked((int)Dword(address + 20)), unchecked((int)Dword(address + 24)), unchecked((int)Dword(address + 28)), 4096);
        }
        private void Matrix(uint address, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                Word(address + (uint)(index * 2), matrix.Rotation[index]);
            }
            Dword(address + 20, unchecked((uint)matrix.X));
            Dword(address + 24, unchecked((uint)matrix.Y));
            Dword(address + 28, unchecked((uint)matrix.Z));
        }
    }
}
