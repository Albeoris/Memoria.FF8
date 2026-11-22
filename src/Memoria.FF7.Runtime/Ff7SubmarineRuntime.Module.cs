using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7SubmarineRuntime
{
    public event Action<int>? SoundRequested;
    public Ff7SubmarineFrame Frame => new(Phase, Vessel(-1, state.Player, true), Array.AsReadOnly(state.Ships.Select((ship, index) => Vessel(index, ship, false)).ToArray()), Array.AsReadOnly(state.Torpedoes.Select((torpedo, index) => new Ff7SubmarineTorpedoFrame(index, Read32(torpedo, 0), Read32(torpedo, 4), Read32(torpedo, 8), Read32(torpedo, 0x84), Read32(torpedo, 0x7C))).Where(torpedo => (torpedo.Flags & 6) != 0).ToArray()), ReadGlobal(0x980DCC), state.Mission, ReadGlobal(0xE74768), false);
    public string NativeProgress => $"{Phase}:{ReadGlobal(0xE74768)}:{ReadGlobal(0xE7476C)}:" + string.Join(';', state.Player.Take(5)) + ":" + string.Join(';', state.Ships.Select(ship => $"{Read32(ship, 0)},{Read32(ship, 4)},{Read32(ship, 8)},{Read32(ship, 0x10)},{Read32(ship, 0x34)}")) + ":" + string.Join(';', state.Torpedoes.Where(torpedo => (Read32(torpedo, 0x7C) & 6) != 0).Select(torpedo => $"{Read32(torpedo, 0)},{Read32(torpedo, 4)},{Read32(torpedo, 8)}"));

    public void Advance(ushort held, ushort pressed)
    {
        if (Finished)
        {
            return;
        }
        state = state with { Tick = checked(state.Tick + 1), PreviousInput = held };
        if (Phase == Ff7SubmarinePhase.Returning)
        {
            state = state with { Phase = Ff7SubmarinePhase.Completed };
            return;
        }
        int flags = ReadGlobal(0xE7476C);
        if ((flags & 2) == 0 && (pressed & 0x800) != 0)
        {
            flags ^= 1;
            flags &= ~12;
            WriteGlobal(0xE7476C, flags);
        }
        if ((flags & 1) == 0)
        {
            AdvancePlayerInput(held, pressed);
        }
        if ((flags & 3) == 0)
        {
            AdvancePlayerMotion();
        }
        if ((flags & 1) == 0)
        {
            AdvanceTerrain();
        }
        if ((flags & 3) == 0 && ReadGlobal(0xEC1824) == 0)
        {
            WriteGlobal(0xEC1824, 1);
        }
        if (ReadGlobal(0xEC1824) != 0)
        {
            int radius = ReadGlobal(0xEC1828) + ReadGlobal(0x987400);
            if (radius > ReadGlobal(0x9873F8) + ReadGlobal(0x9873FC))
            {
                radius = 1;
                WriteGlobal(0xEC1824, 0);
            }
            WriteGlobal(0xEC1828, radius);
        }
        UpdateShipDirections();
        if ((flags & 3) == 0)
        {
            AdvanceShips();
        }
        UpdateTargetLocks();
        if ((flags & 3) == 0)
        {
            AdvanceTorpedoes();
        }
        int wall = checked((int)(state.Tick / 30 * 60));
        int delta = wall - ReadGlobal(0x980DB4);
        WriteGlobal(0x980DB4, wall);
        if ((flags & 1) != 0 || (ReadGlobal(0xE7475C) & 0x10) != 0)
        {
            delta = 0;
        }
        WriteGlobal(0x980DBC, delta);
        WriteGlobal(0x980DC8, unchecked(ReadGlobal(0x980DC8) + delta));
        WriteGlobal(0x980DCC, ReadGlobal(0x980DD0) - ReadGlobal(0x980DC8));
        WriteGlobal(0x980DC0, ReadGlobal(0x980DC8) / 60);
        WriteGlobal(0x980DC4, ReadGlobal(0x980DC8) / 3600);
        if (ReadGlobal(0x980DC0) == 17)
        {
            WriteGlobal(0x987020, 1);
        }
        if ((flags & 2) == 0)
        {
            int outcome = Read32(state.Player, 0x10) <= 0 ? 2 : 0;
            if (ReadGlobal(0x980DC8) > ReadGlobal(0x980DD0))
            {
                outcome = 3;
            }
            if ((Read32(state.Ships[0], 0x34) & 3) == 0 || ReadGlobal(0xE981C4) + ReadGlobal(0xE981C8) >= ReadGlobal(0xE981C0) || ReadGlobal(0xE981C4) >= ReadGlobal(0xE981C0))
            {
                outcome = 1;
            }
            if (outcome != 0)
            {
                WriteGlobal(0xE74768, outcome);
                WriteGlobal(0xE7476C, flags | 2);
                WriteGlobal(0x987020, 0);
                if (outcome == 1)
                {
                    WriteGlobal(0xE74770, ReadGlobal(0x980DCC));
                }
                state = state with { Phase = Ff7SubmarinePhase.Result };
            }
        }
        else
        {
            WriteGlobal(0x980DD4, ReadGlobal(0x980DD4) - delta);
            if (ReadGlobal(0x980DD4) <= 0)
            {
                state = state with { Phase = Ff7SubmarinePhase.Returning };
            }
        }
    }

    private static Ff7SubmarineVesselFrame Vessel(int slot, int[] words, bool player)
    {
        return new(slot, Read32(words, 0), Read32(words, 4), Read32(words, 8), Read16(words, player ? 0x16 : 0x1E), Read16(words, player ? 0x14 : 0x1C), Read32(words, player ? 0xA4 : 0x2C), Read32(words, 0x10), player ? 1 : Read32(words, 0x34), !player && slot == 0);
    }

    private void InitializeModule()
    {
        for (int slot = 0; slot < state.Torpedoes.Length; slot++)
        {
            int[] torpedo = state.Torpedoes[slot];
            Write16(torpedo, 0x3C, 4096);
            Write16(torpedo, 0x4C, 4096);
            Write32(torpedo, 0x68, 4096);
            Write32(torpedo, 0x74, 48);
            Write32(torpedo, 0x78, 32);
            Write32(torpedo, 0x6C, ReadGlobal(slot < 16 ? 0x988510 : 0x98851C));
            Write32(torpedo, 0x7C, 1);
            Write32(torpedo, 0x84, -2);
        }
        WriteGlobal(0xE74768, 0);
        WriteGlobal(0xE7476C, 0);
        WriteGlobal(0x987020, 0);
        WriteGlobal(0x980DD0, ReadGlobal(0x9884F8));
        WriteGlobal(0x980DCC, ReadGlobal(0x9884F8));
        WriteGlobal(0x980DD4, 900);
        state = state with { Phase = Ff7SubmarinePhase.Playing };
    }

    private void EmitSound(int sound) => SoundRequested?.Invoke(sound);
    private static int NativeSin(int angle) => unchecked((int)(Math.Sin((float)(angle * 0.087890625) * 0.017453292519944444) * 4096));
    private static int NativeCos(int angle) => unchecked((int)(Math.Cos((float)(angle * 0.087890625) * 0.017453292519944444) * 4096));
    private int NextRandom()
    {
        uint seed = unchecked(state.RandomSeed * 214013 + 2531011);
        state = state with { RandomSeed = seed };
        return (int)((seed >> 16) & 32767);
    }

    private (int Length, int X, int Y, int Z) Normalize(int x, int y, int z)
    {
        int square = unchecked(x * x + y * y + z * z);
        int leading = BitOperations.LeadingZeroCount((uint)square) & ~1;
        int shift = (31 - leading) / 2;
        int index = leading == 32 ? 0 : leading < 24 ? square >> (24 - leading) : square << (leading - 24);
        if ((uint)index >= resources.NativeRoots.Count || (uint)index >= resources.NativeReciprocals.Count)
        {
            throw new InvalidDataException("FFVII submarine native vector normalization exceeds its original table.");
        }
        int root = resources.NativeRoots[index];
        int reciprocal = resources.NativeReciprocals[index];
        return (unchecked(root << shift) >> 12, unchecked((short)(unchecked(x * reciprocal) >> shift)), unchecked((short)(unchecked(y * reciprocal) >> shift)), unchecked((short)(unchecked(z * reciprocal) >> shift)));
    }

    private void Validate()
    {
        if (state.Request.Command is not Memoria.FF7.Events.FieldMinigameCommand { Game: 4 } || state.Mission != 0 || state.Tick < 0 || !Enum.IsDefined(state.Phase) || state.Player is not { Length: 0x1B8 / 4 } || state.Ships is not { Length: 12 } || state.Ships.Any(ship => ship is not { Length: 0x70 / 4 }) || state.Torpedoes is not { Length: 32 } || state.Torpedoes.Any(torpedo => torpedo is not { Length: 0xC8 / 4 }) || state.Cells is not { Length: 4096 * 8 } || state.Globals is null)
        {
            throw new InvalidDataException("The FFVII submarine continuation has invalid native state allocations or ownership.");
        }
        if (state.Phase == Ff7SubmarinePhase.Introduction)
        {
            return;
        }
        int outcome = ReadGlobal(0xE74768);
        bool terminal = state.Phase is Ff7SubmarinePhase.Result or Ff7SubmarinePhase.Returning or Ff7SubmarinePhase.Completed;
        bool nativeCause = outcome switch
        {
            1 => (Read32(state.Ships[0], 0x34) & 3) == 0 || ReadGlobal(0xE981C4) + ReadGlobal(0xE981C8) >= ReadGlobal(0xE981C0),
            2 => Read32(state.Player, 0x10) <= 0,
            3 => ReadGlobal(0x980DC8) > ReadGlobal(0x980DD0),
            _ => false
        };
        int total = resources.Fleet.Count(ship => (ship.Flags & 1) != 0);
        if (ReadGlobal(0xE981C0) != total || ReadGlobal(0xE981C4) < 0 || ReadGlobal(0xE981C8) < 0 || ReadGlobal(0xE981C4) + ReadGlobal(0xE981C8) > total || terminal && (!nativeCause || (ReadGlobal(0xE7476C) & 2) == 0) || !terminal && outcome != 0 || state.Phase is Ff7SubmarinePhase.Returning or Ff7SubmarinePhase.Completed && ReadGlobal(0x980DD4) > 0 || state.Torpedoes.Any(torpedo => Read32(torpedo, 0x84) is < -2 or > 11))
        {
            throw new InvalidDataException("The FFVII submarine continuation has no matching native outcome or target owner.");
        }
    }
}
