namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38WaveStep(NativeBattleQuake38State State, NativeCrtRandomState Random);
public sealed record NativeBattleQuake38SettleStep(NativeBattleQuake38State State, bool Settled);

public static class NativeBattleQuake38Waves
{
    public static NativeBattleQuake38WaveStep Initialize8DA580(NativeBattleQuake38State state, NativeCrtRandomState random)
    {
        for (int index = 0; index < 10; index++)
        {
            uint address = checked(0x278C910u + (uint)index * 16);
            state = state.SetWord(address, (short)index);
            NativeBattleQuake38WaveStep reset = Reset8DA5B0(state, random, address);
            state = reset.State;
            random = reset.Random;
        }
        return new(state, random);
    }

    public static NativeBattleQuake38WaveStep Advance8DA680(NativeBattleQuake38State state, NativeCrtRandomState random)
    {
        for (int index = 0; index < 10; index++)
        {
            uint address = checked(0x278C910u + (uint)index * 16);
            short phase = state.Word(address + 10);
            if (phase == 0)
            {
                short angle = unchecked((short)(state.Word(address + 4) + state.Word(address + 6)));
                if (angle > 1024)
                {
                    angle = 1024;
                    state = state.SetWord(address + 10, 1);
                }
                short height = unchecked((short)(state.Word(address + 8) * NativeBattleCameraMath.Sine(angle) / -4096));
                state = state.SetWord(address + 4, angle).SetWord(address + 2, height);
            }
            else if (phase == 1)
            {
                short velocity = unchecked((short)(state.Word(address + 12) + 96));
                short height = unchecked((short)(state.Word(address + 2) + velocity));
                state = state.SetWord(address + 12, velocity).SetWord(address + 2, height);
                if (height >= 0)
                {
                    NativeBattleQuake38WaveStep reset = Reset8DA5B0(state, random, address);
                    state = reset.State;
                    random = reset.Random;
                }
            }
        }
        return new(state, random);
    }

    public static NativeBattleQuake38SettleStep Settle8DA710(NativeBattleQuake38State state)
    {
        bool settled = true;
        for (int index = 0; index < 10; index++)
        {
            uint address = checked(0x278C910u + (uint)index * 16);
            short height = state.Word(address + 2);
            if (height == 0)
            {
                continue;
            }
            short velocity = unchecked((short)(state.Word(address + 12) + 96));
            height = unchecked((short)(height + velocity));
            if (height < 0)
            {
                settled = false;
            }
            else
            {
                height = 0;
            }
            state = state.SetWord(address + 12, velocity).SetWord(address + 2, height);
        }
        return new(state, settled);
    }

    private static NativeBattleQuake38WaveStep Reset8DA5B0(NativeBattleQuake38State state, NativeCrtRandomState random, uint address)
    {
        short zone = state.Word(address);
        state = state.SetWord(address + 2, 0).SetWord(address + 4, 0).SetWord(address + 10, 0).SetWord(address + 12, 0);
        if (zone == 0)
        {
            state = state.SetWord(address + 6, 0).SetWord(address + 8, 0);
        }
        else if (zone is >= 1 and <= 9)
        {
            random = random.Draw(out ushort increment);
            random = random.Draw(out ushort amplitude);
            short span = zone == 5 ? (short)((amplitude & 0xFF) + 256) : zone is 6 or 9 ? (short)((amplitude & 0x3FF) + 1024) : (short)((amplitude & 0x3FF) + 256);
            state = state.SetWord(address + 6, (short)((increment & 0x7F) + 256)).SetWord(address + 8, span);
        }
        return new(state, random);
    }
}
