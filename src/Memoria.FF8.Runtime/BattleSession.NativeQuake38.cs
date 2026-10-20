using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeQuake38(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleQuake38Frame frame = NativeBattleQuake38Controller.Enter8D8510(NativeEffectMemory!, effect, nativeActionPackets, effect.Binding.Quake38Resources ?? throw new NotSupportedException("Native Quake requires its actual embedded resources."));
        NativeEffectMemory = frame.Memory;
        NativeCamera = NativeCamera!.RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeQuake38(NativeBattleGraphicsState graphics) => new NativeQuake38Execution(this, graphics).Run();

    private sealed partial class NativeQuake38Execution
    {
        private readonly BattleSession session;
        private NativeBattleGraphicsState graphics;
        private NativeBattleOriginalEffectState effect;
        private readonly NativeBattleQuake38Resources resources;
        private NativeBattleEffectHierarchy hierarchy;
        private NativeBattleQuake38State state;
        private int cursor;
        private NativeBattleCameraProjection Projection => session.NativeCamera!.Projection!;
        private NativeCrtRandomState RandomState
        {
            get => session.nativeCrtRandom ?? throw new NotSupportedException("Native Quake requires its actual CRT random stream.");
            set => session.nativeCrtRandom = value;
        }
        private NativeBattleEffectMemory Memory
        {
            get => session.NativeEffectMemory!;
            set => session.NativeEffectMemory = value;
        }

        public NativeQuake38Execution(BattleSession session, NativeBattleGraphicsState graphics)
        {
            this.session = session;
            this.graphics = graphics;
            effect = graphics.OriginalEffect!;
            resources = effect.Binding.Quake38Resources!;
            hierarchy = new(effect.Pools);
            state = Memory.Quake38!;
            cursor = effect.DrawArenaOffset ?? 0;
        }

        public NativeBattleGraphicsState Run()
        {
            NativeBattleEffectHierarchyStep step = hierarchy.Advance(0, (current, address) =>
            {
                hierarchy = current;
                NativeBattleModulePoolTask task = hierarchy.Read(address);
                cursor = (task.ReadWord(92) & 1) != 0 ? 0 : 0x14000;
                state = state.SetDword(0x278C8E0, checked(0x20DFAB8u + (uint)cursor));
                task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(task, effect.Parameters!, graphics.Models, ReadPose);
                RootPhase(address, ref task);
                hierarchy = hierarchy.Write(address, Merge(address, task));
                int survivors = 0;
                for (int pool = 1; pool < 5; pool++)
                {
                    NativeBattleEffectHierarchyStep children = hierarchy.Advance(pool, AdvanceTask);
                    hierarchy = children.Hierarchy;
                    survivors = unchecked((ushort)(survivors + children.Survivors));
                }
                task = hierarchy.Read(address).WriteWord(94, unchecked((ushort)survivors));
                task = task.WriteWord(92, unchecked((ushort)(task.ReadWord(92) + 1)));
                return Finish(address, task);
            });
            hierarchy = step.Hierarchy;
            Memory = Memory with { Quake38 = state };
            return graphics with { OriginalEffect = effect with { Pools = hierarchy.Pools, DrawArenaOffset = cursor, ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x278E648u } };
        }

        private NativeBattleEffectHierarchyCallback AdvanceTask(NativeBattleEffectHierarchy current, uint address)
        {
            hierarchy = current;
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            switch (task.Callback)
            {
                case 0x8D8720:
                    Emitter(address, ref task);
                    break;
                case 0x8D8800:
                    Sequence(address, ref task);
                    break;
                case 0x8DC250:
                    Shake(ref task);
                    break;
                case 0x8DC360:
                    Fallback(ref task);
                    break;
                case 0x8DA900:
                    Debris(ref task);
                    break;
                case 0x8DB030:
                case 0x8DB410:
                case 0x8DB630:
                    Sprite(ref task);
                    break;
                default:
                    throw new NotSupportedException($"Native Quake callback{task.Callback:X} is unsupported.");
            }
            return Finish(address, Merge(address, task));
        }

        private NativeBattleEffectHierarchyCallback Finish(uint address, NativeBattleModulePoolTask task)
        {
            task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
            hierarchy = hierarchy.Write(address, task);
            bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
            if (complete)
            {
                hierarchy = hierarchy.Complete8DC530(address);
            }
            return new(hierarchy, complete ? 2 : 0);
        }

        private NativeBattleModulePoolTask Merge(uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleModulePoolTask current = hierarchy.Read(address);
            return (task with { Next = current.Next }).WriteByte(40, current.ReadByte(40));
        }

        private uint Spawn(uint parent, int pool, uint callback, int size)
        {
            NativeBattleEffectHierarchyAppend result = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(effect.Parameters!, session.nativeActionPackets, pointer, group, target));
            hierarchy = result.Hierarchy;
            return result.Address;
        }

        private NativeBattlePoseSource ReadPose(int actor) => session.NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Quake requires its selected original actor skeleton.");
        private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static NativeBattleModulePoolTask SetWord(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
        private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        private void Sound(uint id, byte channel) => session.PublishNativeModuleSound(new(id, channel, null) { Pan = 128, RawPan = 128 });
        private int Random()
        {
            RandomState = RandomState.Draw(out ushort value);
            return value;
        }
        private void Emit(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(Memory, cursor, graphics.PrimitiveFrame!.Pending, batch);
            Memory = written.Memory;
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            state = state.SetDword(0x278C8E0, checked(0x20DFAB8u + (uint)cursor));
        }
    }
}
