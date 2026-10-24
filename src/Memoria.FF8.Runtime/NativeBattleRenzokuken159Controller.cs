using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokuken159Inputs(uint GraphicsFlags, ImmutableArray<byte> AbSegments, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRenzokuken159Frame, NativeBattleRenzokuken159Operation, NativeBattleRenzokuken159Frame> Execute)
{
    public Func<byte, NativeBattleModuleAnchor>? ReadAnchor { get; init; }
    public Func<byte, int>? ReadBoneCount { get; init; }
    public Func<byte, int, short, NativeBattleVector>? ReadBone { get; init; }
    public Func<byte, NativeBattleRenzokuken159WeaponTip>? ReadWeaponTip { get; init; }
}
public abstract record NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159ActorFlags(ushort Flags) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159ActorResource(BattleNativeRenzokukenResource Resource, byte Sequence) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159TargetResource(BattleNativeRenzokukenResource Resource) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159Stage(int Intensity) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159ActorColor(uint Color) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159Sound(uint Descriptor, int First, int Second) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159SharedTask(uint Callback, ushort Counter, ushort Actor) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159TrailDraw(NativeBattleGfGeometryBatch Packets, int CursorAdvance) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159MeshDraw(NativeBattleGfGeometryBatch Packets) : NativeBattleRenzokuken159Operation;
public sealed record NativeBattleRenzokuken159WeaponTip(NativeBattleVector Position, int DirectionX, int DirectionY, int DirectionZ, ushort? PositionPadding, ushort? EndpointPadding);

public static partial class NativeBattleRenzokuken159Controller
{
    public static NativeBattleRenzokuken159Frame Enter600BC0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleRenzokuken159Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 159, Entry: 0x600BC0, Initializer: 0x600BA0 } || effect.Parameters is not { } parameters || resources.ActorResource.MagicId != 159)
        {
            throw new InvalidDataException("Native600BC0 requires its original159 binding, action parameters and embedded actor resource.");
        }
        NativeBattleRenzokuken159State retained = memory.Renzokuken159 ?? throw new NotSupportedException("Native159 requires its explicitly retained local storage; migrate the standalone snapshot.");
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native600BC0 requires the physical first-contact pointer.");
        byte target = packets.ReadContact(contact).Primary.ActorIndex;
        bool initial = NativeBattleActionGraphics.Word(header, 4) == 0xFFFE;
        NativeBattleRenzokuken159State state = retained.Enter(parameters.Actor78, parameters.HeaderIndex7C, target, initial) with { RetainedResources = resources };
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23D63D0, 0x23D63C0, 16, 1);
        (main, int mainSlot) = main.Append(0x602C40);
        main = main with { Slots = main.Slots.SetItem(mainSlot, main.Slots[mainSlot].WriteWord(12, 0)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23D6C38, 0x23D6C48, 36, 100);
        (children, int childSlot) = children.Append(0x600CC0);
        children = children with { Slots = children.Slots.SetItem(childSlot, children.Slots[childSlot].WriteWord(12, 0)) };
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native600BC0 requires its actual505E30 texture owner.");
        textures = textures.Register5099A0(resources.Animation);
        NativeBattleEffectInitializerAsset tim = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native600BC0 requires its original initializer TIM asset.");
        (textures, _) = textures.EnqueueTim505E30(new(tim.Name, tim.Data));
        return new(memory with { Renzokuken159 = state, Textures = textures }, effect with { Pools = [main, children], ReturnedDescriptor = 0x23D63D0, DrawArenaOffset = 0 }, random);
    }

    public static NativeBattleRenzokuken159Frame Advance602C40(NativeBattleRenzokuken159Frame frame, Func<NativeBattleRenzokuken159Frame, NativeBattleEffectTaskPool, int, NativeBattleRenzokuken159ChildStep> advanceChild, Func<NativeBattleRenzokuken159Frame, NativeBattleRenzokuken159Frame> drawParticles)
    {
        if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != 0x23D63D0 || frame.Effect.Pools[1].DescriptorAddress != 0x23D6C38)
        {
            throw new InvalidDataException("Native602C40 lost its main and child task descriptors.");
        }
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        if (main.Head is not int root)
        {
            return frame with { Effect = frame.Effect with { ReturnedDescriptor = 0 } };
        }
        NativeBattleModulePoolTask task = main.Slots[root];
        ushort counter = task.ReadWord(12);
        frame = frame with { Memory = frame.Memory with { Renzokuken159 = State(frame) with { DrawParity = counter & 1 } }, Effect = frame.Effect with { DrawArenaOffset = (counter & 1) * 0x10000 } };
        NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, index) =>
        {
            NativeBattleRenzokuken159ChildStep step = advanceChild(frame, pool, index);
            frame = step.Frame;
            return new(step.Pool, step.Result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
        frame = drawParticles(frame);
        NativeBattleEffectTaskStep advancedMain = main.Advance((pool, index) => new(pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index].WriteWord(12, unchecked((ushort)(counter + 1)))) }, children.Survivors == 0 ? 2 : 0));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, advancedMain.Pool), ReturnedDescriptor = advancedMain.Survivors == 0 ? 0u : main.DescriptorAddress } };
    }

    public static NativeBattleRenzokuken159ChildStep Advance600CC0(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken159State state = State(frame);
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken159ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken159ActorResource(resources.ActorResource, state.InitialControl ? (byte)2 : (byte)1));
            Execute(new NativeBattleRenzokuken159TargetResource(resources.ActorResource));
        }
        if (counter == 2)
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native600CC0 requires the current reached504BB0 unsigned AB byte bank.");
            }
            int duration = inputs.AbSegments.Sum(value => (int)value);
            state = State(frame) with { Duration = duration, AbSegments = inputs.AbSegments };
            byte[] history = state.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = frame with { Memory = frame.Memory with { Renzokuken159 = state with { TrailHistory = history.ToImmutableArray() } } };
            Append(0x601980, child => child.WriteWord(12, 0));
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            Append(0x601EC0, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(value % 24)));
            Append(0x601FF0, child => child.WriteWord(12, 0));
        }
        state = State(frame);
        if (state.InitialControl && counter == state.Duration)
        {
            Execute(new NativeBattleRenzokuken159SharedTask(0x6013C0, 0, state.Actor));
        }
        if (counter == 26)
        {
            AppendAtOrigin(0x602670, 1408);
        }
        if (counter is >= 41 and <= 50)
        {
            AppendAtOrigin(0x6027C0, 1536);
            Burst(false);
        }
        if (counter == 51)
        {
            AppendAtOrigin(0x602910, 4864);
            Burst(true);
        }
        if (counter == 84)
        {
            NativeBattleModuleAnchor anchor = (inputs.ReadAnchor ?? throw new NotSupportedException("Native600CC0 requires the current target571400 anchor."))((byte)state.FirstTarget);
            for (int index = 0; index < 8; index++)
            {
                int x = Rand() % 1800 + anchor.X - 900;
                int z = Rand() % 1800 + anchor.Z - 900;
                int horizontal = (Rand() % 3328 + 1792) / 4;
                int vertical = (Rand() % 2816 + 1792) / 4;
                Append(0x602A50, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(2 * index)).WriteWord(16, unchecked((ushort)x)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)z)).WriteWord(28, (ushort)horizontal).WriteWord(30, (ushort)horizontal).WriteWord(32, (ushort)vertical).WriteWord(34, (ushort)vertical));
            }
        }
        if (counter <= 16)
        {
            Execute(new NativeBattleRenzokuken159Stage(counter << 7));
        }
        else if (counter >= state.Duration - 8 && !state.InitialControl)
        {
            Execute(new NativeBattleRenzokuken159Stage((state.Duration - counter) << 8));
        }
        if (counter <= 8 || counter >= state.Duration - 8)
        {
            int phase = counter <= 8 ? counter : state.Duration - counter;
            uint color = NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(resources.DefaultModelColor, phase << 9, 0x4060F0);
            Execute(new NativeBattleRenzokuken159ActorColor(color));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken159Sound(resources.AppearanceSound, 0x8000, 128));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) <= State(frame).Duration ? 0 : 2;
        if (result == 2)
        {
            if (!state.InitialControl)
            {
                Execute(new NativeBattleRenzokuken159Stage(0));
            }
            Execute(new NativeBattleRenzokuken159ActorColor(resources.DefaultModelColor));
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            Execute(new NativeBattleRenzokuken159ActorFlags((ushort)(task.ReadWord(14) | (flags & 0xF7FF))));
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) }, result);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        void AppendAtOrigin(uint callback, ushort scale)
        {
            NativeBattleRenzokuken159State actual = State(frame);
            Append(callback, child => child.WriteWord(12, 0).WriteWord(16, unchecked((ushort)actual.Origin.X)).WriteWord(18, unchecked((ushort)actual.Origin.Y)).WriteWord(20, unchecked((ushort)actual.Origin.Z)).WriteWord(22, actual.OriginPadding).WriteWord(28, scale).WriteWord(30, scale));
        }
        void Burst(bool final)
        {
            byte[] bank = State(frame).Debris.ToArray();
            for (int emission = 0; emission < (final ? 16 : 4); emission++)
            {
                int offset = Free(bank, 20) * 20;
                if (offset == bank.Length)
                {
                    break;
                }
                NativeBattleRenzokuken159State actual = State(frame);
                Write(bank, offset, 0);
                Write(bank, offset + 2, Rand() % (final ? 4608 : 3584) + (final ? 6400 : 4096));
                Write(bank, offset + 4, actual.Origin.X);
                Write(bank, offset + 6, actual.Origin.Y);
                Write(bank, offset + 8, actual.Origin.Z);
                Write(bank, offset + 10, actual.OriginPadding);
                int x = Rand() % 4096 - 2048;
                int y = final ? Rand() % 4096 - Rand() % 2048 - 2048 : -(Rand() % 4096);
                int z = final ? -(Rand() % 4096) : Rand() % 4096;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int speed = Rand() % (final ? 320 : 220) + (final ? 170 : 140);
                Write(bank, offset + 12, unchecked(direction.X * speed) >> 12);
                Write(bank, offset + 14, unchecked(direction.Y * speed) >> 12);
                Write(bank, offset + 16, unchecked(direction.Z * speed) >> 12);
            }
            frame = WriteState(frame, State(frame) with { Debris = bank.ToImmutableArray() });
        }
        void Execute(NativeBattleRenzokuken159Operation operation)
        {
            frame = inputs.Execute(frame, operation);
        }
        void Append(uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
        {
            (pool, int child) = pool.Append(callback);
            pool = pool with { Slots = pool.Slots.SetItem(child, initialize(pool.Slots[child])) };
        }
    }
}
