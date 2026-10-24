using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance5FD230(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, Func<byte, NativeBattleModuleAnchor> readAnchor)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleRenzokuken160State state = ReadState(frame).Core;
        if (counter == 0)
        {
            ushort flags = inputs.ReadModel(state.Actor).Flags0;
            task = task.WriteWord(14, (ushort)(flags & 0x800));
            Execute(new NativeBattleRenzokuken160ActorFlags((ushort)(flags | 0x800)));
        }
        if (counter == 1)
        {
            Execute(new NativeBattleRenzokuken160ActorResource(resources.ActorResource, state.InitialControl ? (byte)2 : (byte)1));
            Execute(new NativeBattleRenzokukenSpecialTargetResource(resources.ActorResource, (byte)state.FirstTarget, 0, 1));
        }
        if (counter == 2)
        {
            if (inputs.AbSegments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Native5FD230 requires the current reached504BB0 unsigned AB byte bank.");
            }
            int duration = inputs.AbSegments.Sum(value => (int)value);
            state = ReadState(frame).Core with { Duration = duration, AbSegments = inputs.AbSegments };
            byte[] history = state.TrailHistory.ToArray();
            for (int offset = 0; offset < history.Length; offset += 20)
            {
                BinaryPrimitives.WriteInt32LittleEndian(history.AsSpan(offset), 0);
            }
            frame = WriteState(frame, state with { TrailHistory = history.ToImmutableArray() });
            Append(0x5FDDD0, child => child.WriteWord(12, 0));
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            Append(0x5FE310, child => child.WriteWord(12, 0).WriteWord(14, (ushort)(value % 24)));
            Append(0x5FE440, child => child.WriteWord(12, 0));
        }
        state = ReadState(frame).Core;
        if (state.InitialControl && counter == state.Duration)
        {
            Execute(new NativeBattleRenzokuken160SharedTask(0x5FD810, 0, state.Actor));
        }
        if (counter is >= 28 and <= 80)
        {
            frame = EmitTarget161(frame, inputs);
        }
        if (counter == 46)
        {
            NativeBattleModuleAnchor original = readAnchor((byte)state.FirstTarget);
            NativeBattleVector anchor = new(original.X, original.W, original.Z);
            Append(0x5FEA70, child => child.WriteWord(12, 0).WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(28, 6144).WriteWord(30, 6144));
            frame = EmitBurst161(frame, anchor);
        }
        if (counter is >= 46 and <= 50)
        {
            Execute(new NativeBattleRenzokukenSpecialCameraRotation((short)((counter & 1) != 0 ? -32 : 32)));
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
        int result = unchecked((short)task.ReadWord(12)) <= ReadState(frame).Core.Duration ? 0 : 2;
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
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) }, result);

        void Execute(NativeBattleRenzokuken160Operation operation)
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
