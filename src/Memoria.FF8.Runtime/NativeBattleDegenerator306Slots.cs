using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDegenerator306Slots
{
    public static (NativeBattleDegenerator306Frame Frame, int Result) Advance(NativeBattleDegenerator306Frame frame, uint taskAddress, NativeBattleDegenerator306Resources resources, NativeBattleDegenerator306Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, taskAddress);
        if (task.Callback != 0x5A4D80 || resources.SpriteB.Address != 0xCE62A8)
        {
            throw new InvalidDataException("Degenerator slot drawing requires its original callback and sprite source.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, resources.SpriteB.Address, 0) };
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int live = 0;
        for (int index = 0; index < SlotCount; index++)
        {
            uint address = checked(SlotBank + (uint)(index * SlotStride));
            if ((frame.State.Read(address, 1)[0] & 2) == 0)
            {
                continue;
            }
            ushort phase = frame.State.ReadWord(address + 4);
            short scale = Signed(frame.State, address + 6);
            NativeBattleModuleAnchor anchor = new(Signed(frame.State, address + 8), Signed(frame.State, address + 10), Signed(frame.State, address + 12), Signed(frame.State, address + 14));
            frame = frame with { Staging = RequireStaging(frame).Write(allocation.Address + 4, BitConverter.GetBytes(phase)), Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), anchor, scale, unchecked((short)-(scale >> 4))) };
            frame = inputs.Execute(frame, new NativeBattleDegenerator306DrawSprite180(allocation.Address, resources.SpriteB.Address, phase) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(306, frame.State.Globals, SlotBank, index) with { ResourceGeneration = unchecked(NativeBattlePilotVisualLifetimes.Identity(306, task).Generation * 397 ^ resources.SpriteB.Address) } : null });
            if (paused)
            {
                continue;
            }
            NativeBattleDegenerator306State state = frame.State.WriteWord(address + 4, unchecked((ushort)(phase + 1)));
            if (BinaryPrimitives.ReadInt16LittleEndian(RequireStaging(frame).Read(allocation.Address + 0x28, 2)) < 0)
            {
                frame = frame with { State = state.WriteDword(address, 0) };
                continue;
            }
            state = state.WriteWord(address + 6, unchecked((ushort)(scale - (scale >> 4))));
            for (int axis = 0; axis < 3; axis++)
            {
                uint position = checked(address + 8 + (uint)axis * 2);
                uint velocity = checked(address + 16 + (uint)axis * 2);
                short speed = Signed(state, velocity);
                state = state.WriteWord(position, unchecked((ushort)(state.ReadWord(position) + speed)));
                state = state.WriteWord(velocity, unchecked((ushort)(speed - (speed >> 2))));
            }
            frame = frame with { State = state };
            live++;
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        if (paused)
        {
            return (frame, 0);
        }
        short age = unchecked((short)task.ReadWord(12));
        if (age is >= 0 and <= 2)
        {
            for (int birth = 0; birth < 20; birth++)
            {
                int slot = 0;
                while (slot < SlotCount && frame.State.ReadDword(checked(SlotBank + (uint)(slot * SlotStride))) != 0)
                {
                    slot++;
                }
                if (slot == SlotCount)
                {
                    break;
                }
                uint address = checked(SlotBank + (uint)(slot * SlotStride));
                frame = DrawRandom(frame, out ushort size);
                NativeBattleDegenerator306State state = frame.State.WriteDword(address, 2).WriteWord(address + 4, 0).WriteWord(address + 6, (ushort)(size % 768 + 256)).Write(address + 8, task.ReadBytes(0x10, 8));
                frame = DrawRandom(frame with { State = state }, out ushort x);
                frame = DrawRandom(frame, out ushort y);
                frame = DrawRandom(frame, out ushort z);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50((x & 4095) - 2048, (y & 4095) - 2048, (z & 4095) - 2048);
                frame = DrawRandom(frame, out ushort magnitude);
                int speed = magnitude % 200 + 170;
                frame = frame with { State = frame.State.WriteWord(address + 16, unchecked((ushort)(unchecked(direction.X * speed) >> 12))).WriteWord(address + 18, unchecked((ushort)(unchecked(direction.Y * speed) >> 12))).WriteWord(address + 20, unchecked((ushort)(unchecked(direction.Z * speed) >> 12))) };
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, SlotBank, slot);
                }
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(age + 1)));
        frame = NativeBattleDegenerator306Controller.Write(frame, taskAddress, task);
        return (frame, unchecked((short)task.ReadWord(12)) >= 4 && live == 0 ? 2 : 0);
    }

    private static short Signed(NativeBattleDegenerator306State state, uint address) => unchecked((short)state.ReadWord(address));
    private static NativeBattleFileStaging RequireStaging(NativeBattleDegenerator306Frame frame) => frame.Staging ?? throw new NotSupportedException("Degenerator slots require their actual shared sprite temporary memory.");
    private static NativeBattleDegenerator306Frame DrawRandom(NativeBattleDegenerator306Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
}
