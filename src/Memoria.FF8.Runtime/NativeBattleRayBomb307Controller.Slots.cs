namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb307Controller
{
    public static (NativeBattleRayBomb307Frame Frame, int Alive) AdvanceSlots(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Inputs inputs)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, 0xCE1760, 0) };
        int alive = 0;
        for (int index = 0; index < 100; index++)
        {
            int row = index * 24;
            if ((frame.State.Particles.Read(row, 1)[0] & 1) == 0)
            {
                continue;
            }
            ushort phase = unchecked((ushort)SlotWord(frame, row + 4));
            if (phase != 0)
            {
                short scale = SlotWord(frame, row + 6);
                NativeBattleEffectArena packed = NativeBattleEffectNodeContextMemory.Copy(frame.State.Particles, row + 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                frame = SetupSprite(frame, packed, scale, unchecked((short)-(scale >> 3)), inputs);
                frame = frame with { Staging = RequireStaging(frame).Write(allocation.Address + 4, BitConverter.GetBytes(phase)) };
                frame = inputs.Execute(frame, new NativeBattleRayBomb307DrawSprite180(allocation.Address, 0xCE1760, phase, 0));
            }
            else
            {
                frame = frame with { Staging = RequireStaging(frame).Write(allocation.Address + 0x28, BitConverter.GetBytes((ushort)0)) };
            }
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = SlotWord(frame, row + 4, unchecked((short)(SlotWord(frame, row + 4) + 1)));
            short feedback = BitConverter.ToInt16(RequireStaging(frame).Read(allocation.Address + 0x28, 2));
            if (feedback < 0)
            {
                frame = SlotDword(frame, row, 0);
                continue;
            }
            short currentScale = SlotWord(frame, row + 6);
            frame = SlotWord(frame, row + 6, unchecked((short)(currentScale + (currentScale >> 5))));
            for (int component = 0; component < 3; component++)
            {
                int velocityOffset = row + 16 + component * 2;
                short velocity = SlotWord(frame, velocityOffset);
                frame = SlotWord(frame, row + 8 + component * 2, unchecked((short)(SlotWord(frame, row + 8 + component * 2) + velocity)));
                frame = SlotWord(frame, velocityOffset, unchecked((short)(velocity - (velocity >> 3))));
            }
            alive++;
        }
        return (frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) }, alive);
    }
}
