using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGatlingGun305Slots
{
    public static (NativeBattleGatlingGun305Frame Frame, int Result) Advance(NativeBattleGatlingGun305Frame frame, uint taskAddress, NativeBattleGatlingGun305Resources resources, NativeBattleGatlingGun305Inputs inputs)
    {
        uint callback = NativeBattleGatlingGun305Controller.Read(frame, taskAddress).Callback;
        int mode = callback switch
        {
            0x5A5DA0 => 0,
            0x5A6330 => 1,
            0x5A68C0 => 2,
            _ => throw new NotSupportedException("Gatling Gun slot callback is unsupported.")
        };
        uint bank = mode == 2 ? SlotBankA : SlotBankB;
        uint source = mode == 2 ? resources.SpriteC.Address : resources.SpriteA.Address;
        if (source != (mode == 2 ? 0xCED504u : 0xCED358u))
        {
            throw new InvalidDataException("Gatling Gun slots require their original sprite resource.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 180);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, mode == 2 ? (ushort)1 : (ushort)4);
        if (mode != 2)
        {
            staging = staging.Write(allocation.Address + 0x1C, BitConverter.GetBytes(0x404040u));
        }
        frame = frame with { Staging = staging };
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int live = 0;
        for (int index = 0; index < SlotCount; index++)
        {
            uint row = checked(bank + (uint)(index * SlotStride));
            uint mask = mode == 0 ? 1u : unchecked((uint)(int)(short)NativeBattleGatlingGun305Controller.Read(frame, taskAddress).ReadWord(14));
            bool selected = mode == 0 ? (frame.State.Read(row, 1)[0] & 1) != 0 : (frame.State.ReadDword(row) & mask) != 0;
            if (!selected)
            {
                continue;
            }
            ushort phase = frame.State.ReadWord(row + 4);
            short scale = Signed(frame.State, row + 6);
            frame = frame with { Staging = Staging(frame).Write(allocation.Address + 4, BitConverter.GetBytes(phase)) };
            if (mode == 2)
            {
                frame = frame with { Staging = Staging(frame).Write(allocation.Address + 8, BitConverter.GetBytes((int)Signed(frame.State, row + 22))) };
            }
            NativeBattleEffectArena packed = frame.State.ReadView(row + 8, 8);
            NativeBattleVector position = new(Word(packed, 0), Word(packed, 2), Word(packed, 4));
            (byte[] wBytes, bool[] wKnown) = packed.ReadSnapshot(6, 2);
            short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, w, scale, unchecked((short)-(scale >> 4))) };
            frame = inputs.Execute(frame, new NativeBattleGatlingGun305DrawSprite180(allocation.Address, source, phase));
            NativeBattleGatlingGun305State state = frame.State;
            if (paused)
            {
                continue;
            }
            state = state.WriteWord(row + 4, unchecked((ushort)(state.ReadWord(row + 4) + 1)));
            if (BinaryPrimitives.ReadInt16LittleEndian(Staging(frame).Read(allocation.Address + 0x28, 2)) < 0)
            {
                frame = frame with { State = state.WriteDword(row, 0) };
                continue;
            }
            if (mode != 2)
            {
                state = state.WriteWord(row + 6, unchecked((ushort)(state.ReadWord(row + 6) + (mode == 0 ? 24 : 32))));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if (mode == 1 && axis == 1)
                {
                    continue;
                }
                uint coordinate = checked(row + 8 + (uint)axis * 2);
                short velocity = Signed(state, checked(row + 16 + (uint)axis * 2));
                state = state.WriteWord(coordinate, unchecked((ushort)(state.ReadWord(coordinate) + velocity)));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if (mode != 2 && axis == 1)
                {
                    continue;
                }
                uint velocityAddress = checked(row + 16 + (uint)axis * 2);
                short velocity = Signed(state, velocityAddress);
                state = state.WriteWord(velocityAddress, unchecked((ushort)(velocity - (mode == 2 ? velocity / 3 : velocity >> 3))));
            }
            frame = frame with { State = state };
            live++;
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 180) };
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = NativeBattleGatlingGun305Controller.Read(frame, taskAddress);
        short age = unchecked((short)task.ReadWord(12));
        bool emit = mode == 0 ? age is >= 0 and <= 23 : age == 0;
        if (emit)
        {
            int births = mode == 0 ? 3 : mode == 1 ? 10 : 12;
            for (int birth = 0; birth < births; birth++)
            {
                int index = 0;
                while (index < SlotCount && frame.State.ReadDword(checked(bank + (uint)(index * SlotStride))) != 0)
                {
                    index++;
                }
                if (index == SlotCount)
                {
                    break;
                }
                uint row = checked(bank + (uint)(index * SlotStride));
                frame = mode switch
                {
                    0 => BirthContinuous(frame, row),
                    1 => BirthPlanar(frame, row, taskAddress),
                    _ => BirthBurst(frame, row, taskAddress)
                };
            }
        }
        task = NativeBattleGatlingGun305Controller.Read(frame, taskAddress);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = NativeBattleGatlingGun305Controller.Write(frame, taskAddress, task.WriteWord(12, next));
        return (frame, unchecked((short)next) >= (mode == 2 ? 15 : 16) && live == 0 ? 2 : 0);
    }

    private static NativeBattleGatlingGun305Frame BirthContinuous(NativeBattleGatlingGun305Frame frame, uint row)
    {
        frame = frame with { State = frame.State.WriteDword(row, 1).WriteWord(row + 4, 0) };
        frame = Random(frame, out ushort size);
        frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)(size % 1536 + 1024)).WriteView(row + 8, frame.State.ReadView(AnchorGlobal, 8), 8) };
        (frame, var direction) = Direction(frame);
        frame = Random(frame, out ushort offset);
        frame = Offset(frame, row, direction.X, direction.Y, direction.Z, offset % 20);
        frame = Random(frame, out ushort velocity);
        int speed = velocity % 50 + 20;
        frame = frame with { State = frame.State.WriteWord(row + 16, Component(direction.X, speed)) };
        frame = Random(frame, out ushort y);
        return frame with { State = frame.State.WriteWord(row + 18, unchecked((ushort)(-10 - y % 35))).WriteWord(row + 20, Component(direction.Z, speed)) };
    }

    private static NativeBattleGatlingGun305Frame BirthPlanar(NativeBattleGatlingGun305Frame frame, uint row, uint taskAddress)
    {
        NativeBattleModulePoolTask task = NativeBattleGatlingGun305Controller.Read(frame, taskAddress);
        frame = frame with { State = frame.State.WriteDword(row, unchecked((uint)(int)(short)task.ReadWord(14))).WriteWord(row + 4, 0) };
        frame = Random(frame, out ushort size);
        frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)(size % 1536 + 2048)).WriteView(row + 8, TaskPosition(task), 8) };
        frame = Random(frame, out ushort angle);
        int x = NativeBattleCameraMath.Cosine(angle & 4095);
        int z = NativeBattleCameraMath.Sine(angle & 4095);
        frame = Random(frame, out ushort offset);
        int radius = offset % 50 + 50;
        frame = frame with { State = frame.State.WriteWord(row + 8, unchecked((ushort)(frame.State.ReadWord(row + 8) + (unchecked(radius * x) >> 12)))).WriteWord(row + 12, unchecked((ushort)(frame.State.ReadWord(row + 12) + (unchecked(radius * z) >> 12)))) };
        frame = Random(frame, out ushort velocity);
        int speed = velocity % 30 + 20;
        return frame with { State = frame.State.WriteWord(row + 16, Component(x, speed)).WriteWord(row + 20, Component(z, speed)) };
    }

    private static NativeBattleGatlingGun305Frame BirthBurst(NativeBattleGatlingGun305Frame frame, uint row, uint taskAddress)
    {
        NativeBattleModulePoolTask task = NativeBattleGatlingGun305Controller.Read(frame, taskAddress);
        frame = frame with { State = frame.State.WriteDword(row, unchecked((uint)(int)(short)task.ReadWord(14))).WriteWord(row + 4, 0) };
        frame = Random(frame, out ushort size);
        frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)((size & 1023) + 1280)).WriteView(row + 8, TaskPosition(task), 8) };
        (frame, var direction) = Direction(frame);
        frame = Random(frame, out ushort offset);
        frame = Offset(frame, row, direction.X, direction.Y, direction.Z, offset % 100 + 90);
        frame = Random(frame, out ushort velocity);
        int speed = velocity % 380 + 130;
        NativeBattleGatlingGun305State state = frame.State.WriteWord(row + 16, Component(direction.X, speed)).WriteWord(row + 18, Component(direction.Y, speed)).WriteWord(row + 20, Component(direction.Z, speed));
        frame = Random(frame with { State = state }, out ushort angle);
        return frame with { State = frame.State.WriteWord(row + 22, (ushort)(angle & 2047)) };
    }

    private static (NativeBattleGatlingGun305Frame Frame, (int X, int Y, int Z) Direction) Direction(NativeBattleGatlingGun305Frame frame)
    {
        frame = Random(frame, out ushort x);
        frame = Random(frame, out ushort y);
        frame = Random(frame, out ushort z);
        return (frame, NativeBattleActionGeometryRules.NormalizeVector56BC50((x & 4095) - 2048, (y & 4095) - 2048, (z & 4095) - 2048));
    }

    private static NativeBattleGatlingGun305Frame Offset(NativeBattleGatlingGun305Frame frame, uint row, int x, int y, int z, int distance)
    {
        NativeBattleGatlingGun305State state = frame.State;
        int[] components = [x, y, z];
        for (int axis = 0; axis < 3; axis++)
        {
            uint address = checked(row + 8 + (uint)axis * 2);
            state = state.WriteWord(address, unchecked((ushort)(state.ReadWord(address) + (unchecked(components[axis] * distance) >> 12))));
        }
        return frame with { State = state };
    }

    private static NativeBattleEffectArena TaskPosition(NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(4, 8));
        if (task.UnknownPayloadRanges is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(0x10, range.Offset);
                int end = Math.Min(0x18, range.Offset + range.Length);
                if (first < end)
                {
                    result = result.MarkUnknown(first - 0x10, end - first);
                }
            }
        }
        return result;
    }

    private static ushort Component(int direction, int speed) => unchecked((ushort)(unchecked(direction * speed) >> 12));
    private static NativeBattleGatlingGun305Frame Random(NativeBattleGatlingGun305Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleFileStaging Staging(NativeBattleGatlingGun305Frame frame) => frame.Staging ?? throw new NotSupportedException("Gatling Gun slots require their physical sprite temporary.");
    private static short Signed(NativeBattleGatlingGun305State state, uint address) => unchecked((short)state.ReadWord(address));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
