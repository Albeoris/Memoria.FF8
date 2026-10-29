using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltEye158Controller
{
    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceStream(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int ordinal = DrawingWord(task, 14);
        uint curve = DrawingCurve(ordinal);
        uint mask = 1u << ordinal;
        int sample = DrawingWord(task, 0x18);
        int scale = DrawingWord(task, 0x1C);
        int iterations = 0;
        int active = 0;
        (frame, uint temporary) = DrawingAllocate(frame, 180);
        frame = DrawingBeginSprite(frame, temporary, resources.SpriteC.Address);
        while (iterations < 7)
        {
            if (sample < 0)
            {
                break;
            }
            if (sample < 48)
            {
                frame = DrawingSprite(frame, temporary, resources.SpriteC.Address, checked(curve + 0x38 + (uint)(sample * 8)), unchecked((short)scale), unchecked((short)-(scale >> 3)), unchecked((short)(task.ReadByte(12) & 3)), inputs, NativeBattlePilotVisualLifetimes.Identity(158, task, 0x10000 + sample) with { ResourceGeneration = curve });
            }
            sample--;
            scale -= 48;
            iterations++;
        }
        if (!DrawingPaused(inputs) && DrawingWord(Read(frame, address), 0x18) < 55)
        {
            for (int index = 0; index < 5; index++)
            {
                int slot = DrawingFindSlot(frame.State, SlotBankB, SlotCountB);
                if (slot >= 0 && iterations >= 7 && sample >= 0)
                {
                    uint row = checked(SlotBankB + (uint)(slot * SlotStride));
                    short phase = unchecked((short)(index + 4));
                    frame = frame with { State = frame.State.WriteWord(row + 4, unchecked((ushort)phase)) };
                    if (phase < 8)
                    {
                        NativeBattleEffectArena position = frame.State.ReadView(checked(curve + 0x38 + (uint)(sample * 8)), 8);
                        frame = frame with { State = frame.State.WriteDword(row, mask).WriteWord(row + 6, unchecked((ushort)scale)).WriteView(row + 8, position, 8) };
                        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, row, 0);
                    }
                }
                sample--;
            }
        }
        for (int index = 0; index < SlotCountB; index++)
        {
            uint row = checked(SlotBankB + (uint)(index * SlotStride));
            if ((frame.State.ReadDword(row) & mask) == 0)
            {
                continue;
            }
            short phase = unchecked((short)frame.State.ReadWord(row + 4));
            short rowScale = unchecked((short)frame.State.ReadWord(row + 6));
            frame = DrawingSprite(frame, temporary, resources.SpriteC.Address, row + 8, rowScale, unchecked((short)-(rowScale >> 4)), phase, inputs, RowIdentity158(frame, Read(frame, address), row, 0x20000 + index));
            if (!DrawingPaused(inputs))
            {
                phase = unchecked((short)(phase + 2));
                NativeBattleMeltEye158State state = frame.State.WriteWord(row + 4, unchecked((ushort)phase));
                if (phase >= 8)
                {
                    state = state.WriteDword(row, 0);
                }
                else
                {
                    active++;
                }
                frame = frame with { State = state };
            }
        }
        frame = DrawingRelease(frame, 180);
        if (DrawingPaused(inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short velocity = DrawingWord(task, 0x1A);
        short next = unchecked((short)(DrawingWord(task, 0x18) + velocity));
        task = task.WriteWord(0x18, unchecked((ushort)next)).WriteWord(12, unchecked((ushort)(DrawingWord(task, 12) + 1)));
        if (velocity > 5)
        {
            task = task.WriteWord(0x1A, unchecked((ushort)(velocity - 1)));
        }
        return (Write(frame, address, task), next >= 55 && active == 0 ? 2 : 0);
    }

    public static (NativeBattleMeltEye158Frame Frame, int Result) AdvanceForkEmitter(NativeBattleMeltEye158Frame frame, uint address, NativeBattleMeltEye158Resources resources, NativeBattleMeltEye158Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int mask = DrawingWord(task, 14);
        int active = 0;
        (frame, uint temporary) = DrawingAllocate(frame, 180);
        frame = DrawingBeginSprite(frame, temporary, resources.SpriteB.Address);
        for (int index = 0; index < SlotCountA; index++)
        {
            uint row = checked(SlotBankA + (uint)(index * SlotStride));
            if ((frame.State.ReadDword(row) & unchecked((uint)mask)) == 0)
            {
                continue;
            }
            short phase = unchecked((short)frame.State.ReadWord(row + 4));
            short scale = unchecked((short)frame.State.ReadWord(row + 6));
            frame = DrawingSprite(frame, temporary, resources.SpriteB.Address, row + 8, scale, unchecked((short)-(scale >> 4)), phase, inputs, RowIdentity158(frame, Read(frame, address), row, 0x30000 + index));
            if (DrawingPaused(inputs))
            {
                continue;
            }
            phase = unchecked((short)(phase + 1));
            NativeBattleMeltEye158State state = frame.State.WriteWord(row + 4, unchecked((ushort)phase));
            short feedback = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(frame.Staging!.Read(temporary + 0x28, 2));
            if (feedback < 0)
            {
                frame = frame with { State = state.WriteDword(row, 0) };
                continue;
            }
            state = state.WriteWord(row + 6, unchecked((ushort)(scale - 4)));
            for (uint axis = 0; axis < 3; axis++)
            {
                short position = unchecked((short)state.ReadWord(row + 8 + axis * 2));
                short velocity = unchecked((short)state.ReadWord(row + 16 + axis * 2));
                state = state.WriteWord(row + 8 + axis * 2, unchecked((ushort)(position + velocity))).WriteWord(row + 16 + axis * 2, unchecked((ushort)(velocity - (velocity >> 5))));
            }
            frame = frame with { State = state };
            active++;
        }
        frame = DrawingRelease(frame, 180);
        if (DrawingPaused(inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        int age = DrawingWord(task, 12);
        if (age is >= 0 and <= 16)
        {
            int vectorIndex = DrawingWord(task, 0x18);
            if (vectorIndex < 0 || vectorIndex >= resources.Vectors.Length - 1)
            {
                throw new NotSupportedException("Melt-Eye fork endpoints leave their original vector pairs.");
            }
            NativeBattleActionMatrix world = WorldRotation(frame, Caster(frame), inputs);
            NativeBattleModuleAnchor first = resources.Vectors[vectorIndex];
            NativeBattleModuleAnchor second = resources.Vectors[vectorIndex + 1];
            NativeBattleVector start = NativeBattleGfMatrices.RotateShort56C4F0(world, new(first.X, first.Y, first.Z));
            NativeBattleVector end = NativeBattleGfMatrices.RotateShort56C4F0(world, new(second.X, second.Y, second.Z));
            NativeBattleVector offset = TaskVector(task, 0x10);
            start = new(unchecked((short)(start.X + offset.X)), unchecked((short)(start.Y + offset.Y)), unchecked((short)(start.Z + offset.Z)));
            end = new(unchecked((short)(end.X + offset.X)), unchecked((short)(end.Y + offset.Y)), unchecked((short)(end.Z + offset.Z)));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            for (int index = 0; index < 2; index++)
            {
                int slot = DrawingFindSlot(frame.State, SlotBankA, SlotCountA);
                if (slot < 0)
                {
                    break;
                }
                uint row = checked(SlotBankA + (uint)(slot * SlotStride));
                frame = frame with { State = frame.State.WriteDword(row, unchecked((uint)mask)).WriteWord(row + 4, 0) };
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, row, 0);
                frame = Random(frame, out ushort sizeRoll);
                NativeBattleMeltEye158State state = frame.State.WriteWord(row + 6, unchecked((ushort)(sizeRoll % 1536 + 1792)));
                state = state.WriteWord(row + 8, unchecked((ushort)start.X)).WriteWord(row + 10, unchecked((ushort)start.Y)).WriteWord(row + 12, unchecked((ushort)start.Z));
                state = state.WriteView(row + 14, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2);
                frame = frame with { State = state };
                frame = Random(frame, out ushort speedRoll);
                int speed = speedRoll % 75 + 30;
                int[] axes = [direction.X, direction.Y, direction.Z];
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = Random(frame, out ushort jitter);
                    frame = frame with { State = frame.State.WriteWord(row + 16 + (uint)(axis * 2), unchecked((ushort)((unchecked(speed * axes[axis]) >> 12) + jitter % 40 - 20))) };
                }
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), DrawingWord(task, 12) >= 4 && active == 0 ? 2 : 0);
    }

    private static NativeBattleMeltEye158Frame DrawingSprite(NativeBattleMeltEye158Frame frame, uint temporary, uint source, uint positionAddress, short scale, short depth, short phase, NativeBattleMeltEye158Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector position = DrawingStateVector(frame.State, positionAddress);
        (byte[] bytes, bool[] known) = frame.State.ReadView(positionAddress + 6, 2).ReadSnapshot(0, 2);
        short? w = known.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return inputs.Execute(frame, new NativeBattleMeltEye158DrawSprite180(temporary, source, position, w, scale, depth, phase) { PilotIdentity = pilotIdentity });
    }

    private static NativeBattlePilotVisualIdentity RowIdentity158(NativeBattleMeltEye158Frame frame, NativeBattleModulePoolTask task, uint row, int primitive) => NativeBattlePilotVisualLifetimes.Identity(158, task, primitive) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(158, frame.State.Globals, row, 0).Generation };

    private static int DrawingFindSlot(NativeBattleMeltEye158State state, uint bank, int count)
    {
        for (int index = 0; index < count; index++)
        {
            if (state.ReadDword(checked(bank + (uint)(index * SlotStride))) == 0)
            {
                return index;
            }
        }
        return -1;
    }
}
