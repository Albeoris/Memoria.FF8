using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleClash26Resources(ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<short> Fade, ImmutableArray<uint> FlashScript, uint Sound);
public sealed record NativeBattleClash26State([property: JsonRequired] int ArenaOffset, [property: JsonRequired] int? DrawCursor);
public sealed record NativeBattleClash26Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleClash26State State);
public sealed record NativeBattleClash26Step(NativeBattleClash26Frame Frame, NativeCrtRandomState Random);
public abstract record NativeBattleClash26Operation;
public sealed record NativeBattleClash26Packets(NativeBattleGfGeometryBatch Batch) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Sprite(NativeBattleModulePacketBatch Batch) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Sound(uint Sound) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Contact(int Index) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Sequence(byte Actor, int Sequence) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Model(NativeBattleGraphicsModel Model) : NativeBattleClash26Operation;
public sealed record NativeBattleClash26Inputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleClash26Frame, NativeBattleClash26Operation, NativeBattleClash26Frame> Execute);

public static partial class NativeBattleClash26Controller
{
    public static NativeBattleClash26Frame Initialize8885D0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x8885D0)
        {
            throw new InvalidDataException("Native Clash requires its original8885D0 entry.");
        }
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native8885D0 requires its50AFC0 parameters.");
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x26DBC00, 0x26DBB38, 0x64, 2),
            NativeBattleEffectTaskPool.Initialize(0x26E3F68, 0x26E3F78, 0x58, 4),
            NativeBattleEffectTaskPool.Initialize(0x26E40E0, 0x26E40F0, 0x60, 0x40),
            NativeBattleEffectTaskPool.Initialize(0x26E3F50, 0x26DBC10, 0x70, 0x12C),
            NativeBattleEffectTaskPool.Initialize(0x26DBB10, 0x26D90E0, 0x6C, 0x64),
            NativeBattleEffectTaskPool.Initialize(0x26D9050, 0x26D9060, 0x3C, 2)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8886C0, 0x64);
        hierarchy = root.Hierarchy;
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, 0x1D99A78, 0, 0);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78).WriteByte(0x2D, actors.Target).WriteWord(0x5A, header.ReadByte(16)).WriteWord(0x58, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        int offset = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x26D90D8)?.ArenaOffset ?? throw new NotSupportedException("Native8885D0 requires its executed26D90D8 resource pointer.");
        int packetOffset = memory.Cursor;
        if (packetOffset > NativeBattleEffectArena.Length - 0x18000)
        {
            throw new NotSupportedException("Native8885D0 packet arenas leave their original magic allocation.");
        }
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native8885D0 requires its505E30 texture owner.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native8885D0 requires its loaded magic arena."), offset);
        memory = memory with { Textures = textures };
        effect = effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x26DBC00 };
        return new(memory, effect, new(packetOffset, null));
    }

    public static NativeBattleClash26Step Advance8886C0(NativeBattleClash26Frame frame, NativeBattleClash26Resources resources, NativeBattleClash26Inputs inputs, NativeCrtRandomState random)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return new(frame, random);
        }
        Execution execution = new(frame, resources, inputs, random);
        NativeBattleEffectHierarchyStep main = execution.Hierarchy.Advance(0, (hierarchy, address) =>
        {
            execution.SetHierarchy(hierarchy);
            NativeBattleModulePoolTask task = execution.Read(address);
            if (task.Callback != 0x8886C0)
            {
                throw new NotSupportedException("Native Clash main task requires8886C0.");
            }
            execution.Frame = execution.Frame with { State = execution.Frame.State with { DrawCursor = checked(execution.Frame.State.ArenaOffset + ((task.ReadByte(0x5C) & 1) == 0 ? 0xC000 : 0)) } };
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, execution.Models(), inputs.ReadPoseSource);
            execution.Write(address, task);
            int phase = unchecked((sbyte)task.ReadByte(0x29));
            switch (phase)
            {
                case 0:
                case 2:
                    execution.Write(address, Next(task));
                    break;
                case 1:
                    for (int target = 0; target < Word(task, 0x5A); target++)
                    {
                        execution.Append(address, 0x8887F0, 0x58, 1);
                        task = execution.Read(address);
                        task = task.WriteByte(0x2B, unchecked((byte)(task.ReadByte(0x2B) + 1)));
                        execution.Write(address, task);
                    }
                    execution.Write(address, Next(execution.Read(address)));
                    break;
                case 3:
                    if (task.ReadWord(0x5E) == 0)
                    {
                        execution.Write(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new InvalidDataException("Native8886C0 signed phase exceeds its original five callbacks.");
            }
            int survivors = 0;
            for (int pool = 1; pool < 6; pool++)
            {
                NativeBattleEffectHierarchyStep children = execution.Hierarchy.Advance(pool, (childHierarchy, childAddress) =>
                {
                    execution.SetHierarchy(childHierarchy);
                    int result = execution.Advance(childAddress);
                    return new(execution.Hierarchy, result);
                });
                execution.SetHierarchy(children.Hierarchy);
                survivors = unchecked((ushort)(survivors + children.Survivors));
            }
            task = execution.Read(address).WriteWord(0x5E, (ushort)survivors);
            task = Add(task, 0x5C, 1);
            execution.Write(address, task);
            int complete = execution.Finish(address);
            return new(execution.Hierarchy, complete);
        });
        execution.SetHierarchy(main.Hierarchy);
        if (main.Survivors == 0)
        {
            execution.Frame = execution.Frame with { Effect = execution.Frame.Effect with { ReturnedDescriptor = 0 } };
        }
        return new(execution.Frame, execution.Random);
    }

    private sealed partial class Execution(NativeBattleClash26Frame frame, NativeBattleClash26Resources resources, NativeBattleClash26Inputs inputs, NativeCrtRandomState random)
    {
        public NativeBattleClash26Frame Frame { get; set; } = frame;
        public NativeCrtRandomState Random { get; private set; } = random;
        public NativeBattleEffectHierarchy Hierarchy => new(Frame.Effect.Pools);
        public void SetHierarchy(NativeBattleEffectHierarchy hierarchy) => Frame = Frame with { Effect = Frame.Effect with { Pools = hierarchy.Pools } };
        public NativeBattleModulePoolTask Read(uint address) => Hierarchy.Read(address);
        public void Write(uint address, NativeBattleModulePoolTask task) => SetHierarchy(Hierarchy.Write(address, task));
        public NativeBattleGraphicsModel[] Models() => Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray();
        private void Execute(NativeBattleClash26Operation operation) => Frame = inputs.Execute(Frame, operation);
        private int Rand()
        {
            Random = Random.Draw(out ushort value);
            return value;
        }
        public uint Append(uint parent, uint callback, int bytes, int pool)
        {
            NativeBattleEffectParameters parameters = Frame.Effect.Parameters ?? throw new InvalidDataException("Native Clash append lost its original parameters.");
            NativeBattleEffectHierarchyAppend appended = Hierarchy.Append8DC540(pool, callback, bytes, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.Packets, pointer, group, target));
            SetHierarchy(appended.Hierarchy);
            if (appended.Address == 0)
            {
                throw new NotSupportedException("Native Clash dereferenced its failed original task allocation.");
            }
            return appended.Address;
        }
        private NativeBattleModulePoolTask CopyAnchor(NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask first = Read(task.ReadDword(0x14));
            return task.WriteDword(0x1C, first.ReadDword(0x30)).WriteDword(0x20, first.ReadDword(0x34));
        }
        public int Finish(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            byte status = task.ReadByte(0x26);
            task = Add(task, 0x24, 1);
            Write(address, task);
            if ((status & 1) != 0 && task.ReadByte(0x28) == 0)
            {
                SetHierarchy(Hierarchy.Complete8DC530(address));
                return 2;
            }
            return 0;
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
