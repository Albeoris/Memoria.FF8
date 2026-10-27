using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokuken160Inputs(uint GraphicsFlags, ImmutableArray<byte> AbSegments, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Operation, NativeBattleRenzokuken160Frame> Execute)
{
    public Func<byte, int>? ReadBoneCount { get; init; }
    public Func<byte, int, short, NativeBattleVector>? ReadBone { get; init; }
    public Func<byte, NativeBattleRenzokuken160WeaponTip>? ReadWeaponTip { get; init; }
}
public abstract record NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160ActorFlags(ushort Flags) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160ActorResource(BattleNativeRenzokukenResource Resource, byte Sequence) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160Stage(int Intensity) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160ActorColor(uint Color) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160Sound(uint Descriptor, int First, int Second) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160SharedTask(uint Callback, ushort Counter, ushort Actor) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160TrailDraw(NativeBattleGfGeometryBatch Packets, int CursorAdvance) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160MeshDraw(NativeBattleGfGeometryBatch Packets) : NativeBattleRenzokuken160Operation;
public sealed record NativeBattleRenzokuken160WeaponTip(NativeBattleVector Position, int DirectionX, int DirectionY, int DirectionZ, ushort? PositionPadding, ushort? EndpointPadding);

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleRenzokuken160Frame Enter5FF080(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRenzokuken160Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 160, Entry: 0x5FF080, Initializer: 0x5FF060 } || effect.Parameters is not { } parameters || resources.ActorResource.MagicId != 160)
        {
            throw new InvalidDataException("Native5FF080 requires its original160 binding, action parameters and embedded actor resource.");
        }
        NativeBattleRenzokuken160State retained = memory.Renzokuken160 ?? throw new NotSupportedException("Native160 requires its explicitly retained local storage; migrate the standalone snapshot.");
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native5FF080 requires the physical first-contact pointer.");
        byte target = packets.ReadContact(contact).Primary.ActorIndex;
        bool initial = NativeBattleActionGraphics.Word(header, 4) == 0xFFFE;
        NativeBattleRenzokuken160State state = retained.Enter(parameters.Actor78, parameters.HeaderIndex7C, target, initial) with { RetainedResources = resources };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23D4230, 0x23D4220, 16, 1);
        (main, int mainSlot) = main.Append(0x600930);
        main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23D4778, 0x23D4788, 36, 100);
        (children, int childSlot) = children.Append(0x5FF180);
        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native5FF080 requires its actual505E30 texture owner.");
        textures = textures.Register5099A0(resources.Animation);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native5FF080 requires its original initializer TIM asset.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { Renzokuken160 = state, Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23D4230, DrawArenaOffset = 0 }, random);
    }

    public static NativeBattleRenzokuken160Frame Advance600930(NativeBattleRenzokuken160Frame frame, Func<NativeBattleRenzokuken160Frame, NativeBattleEffectTaskPool, int, NativeBattleRenzokuken160ChildStep> advanceChild, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Frame> drawParticles)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23D4230 || frame.Effect.Pools[1].DescriptorAddress != 0x23D4778)
        {
            throw new InvalidDataException("Native600930 lost its main and child task descriptors.");
        }
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        if (main.Head is not int root)
        {
            return frame with { Effect = frame.Effect with { ReturnedDescriptor = 0 } };
        }
        NativeBattleModulePoolTask task = main.Slots[root];
        ushort counter = task.ReadWord(12);
        frame = frame with { Memory = frame.Memory with { Renzokuken160 = State(frame) with { DrawParity = counter & 1 } }, Effect = frame.Effect with { DrawArenaOffset = (counter & 1) * 0x8000 } };
        NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
        {
            NativeBattleRenzokuken160ChildStep step = advanceChild(frame, pool, index);
            frame = step.Frame;
            return new(step.Pool, step.Result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
        frame = drawParticles(frame);
        NativeBattleEffectTaskStep advancedMain = main.Advance((pool, index) => new(pool.WriteSlot(index, pool.Slots[index].WriteWord(12, unchecked((ushort)(counter + 1)))), children.Survivors == 0 ? 2 : 0));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advancedMain.Pool), ReturnedDescriptor = advancedMain.Survivors == 0 ? 0u : main.DescriptorAddress } };
    }

    public static NativeBattleRenzokuken160ChildStep Advance5FF180(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken160State state = State(frame);
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160ActorResource(resources.ActorResource, state.InitialControl ? (byte)2 : (byte)1));
        }
        if (counter == 2)
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native5FF180 requires the current reached504BB0 unsigned AB byte bank.");
            }
            int duration = inputs.AbSegments.Sum(value => (int)value);
            state = State(frame) with { Duration = duration, AbSegments = inputs.AbSegments };
            byte[] history = state.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = frame with { Memory = frame.Memory with { Renzokuken160 = state with { TrailHistory = history.ToImmutableArray() } } };
            Append(0x5FFAA0, child => child.WriteWord(12, 0));
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            Append(0x5FFFE0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(value % 24)));
            Append(0x600110, child => child.WriteWord(12, 0));
        }
        state = State(frame);
        if (state.InitialControl && counter == state.Duration)
        {
            Execute(new NativeBattleRenzokuken160SharedTask(0x5FF4E0, 0, state.Actor));
        }
        if (counter == 45)
        {
            short rotation = inputs.ReadModel(state.Actor).Rotation14 ?? throw new NotSupportedException("Native5FF180 requires original model rotationWORD14.");
            Append(0x600780, child => child.WriteWord(16, unchecked((ushort)state.Origin.X)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)state.Origin.Z)).WriteWord(12, 0).WriteWord(32, 4096).WriteWord(34, 819).WriteWord(24, unchecked((ushort)rotation)));
        }
        if (counter <= 16)
        {
            Execute(new NativeBattleRenzokuken160Stage(counter << 7));
        }
        else if (counter >= state.Duration - 8 && !state.InitialControl)
        {
            Execute(new NativeBattleRenzokuken160Stage((state.Duration - counter) << 8));
        }
        if (counter <= 8 || counter >= state.Duration - 8)
        {
            int phase = counter <= 8 ? counter : state.Duration - counter;
            uint color = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.DefaultModelColor, phase << 9, 0x4060F0);
            Execute(new NativeBattleRenzokuken160ActorColor(color));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160Sound(resources.AppearanceSound, 0x8000, 128));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) <= State(frame).Duration ? 0 : 2;
        if (result == 2)
        {
            if (!state.InitialControl)
            {
                Execute(new NativeBattleRenzokuken160Stage(0));
            }
            Execute(new NativeBattleRenzokuken160ActorColor(resources.DefaultModelColor));
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(task.ReadWord(14) | (flags & 0xF7FF))));
        }
        return new(frame, pool.WriteSlot(slot, task), result);

        void Execute(NativeBattleRenzokuken160Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Append(uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            (pool, int child) = pool.Append(callback);
            pool = pool.WriteSlot(child, initialize(pool.Slots[child]));
        }
    }
}
