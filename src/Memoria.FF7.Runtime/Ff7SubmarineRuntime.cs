using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7SubmarineSnapshot(FieldHostRequest Request, int Mission, uint RandomSeed, long Tick, Ff7SubmarinePhase Phase, int[] Player, int[][] Ships, int[][] Torpedoes, ushort[] Cells, Dictionary<int, int> Globals, ushort PreviousInput = 0);

public sealed partial class Ff7SubmarineRuntime
{
    private readonly Ff7SubmarineResources resources;
    private Ff7SubmarineSnapshot state;

    public Ff7SubmarineRuntime(Ff7SubmarineResources resources, FieldHostRequest request, int mission, uint randomSeed, Ff7SubmarineSnapshot? restored = null)
    {
        this.resources = resources;
        if (request.Command is not FieldMinigameCommand { Game: 4 } || mission != 0)
        {
            throw new NotSupportedException("FFVII submarine gameplay requires its supported native mission and field owner.");
        }
        state = restored ?? new(request, mission, randomSeed, 0, Ff7SubmarinePhase.Introduction, new int[0x1B8 / 4], Enumerable.Range(0, 12).Select(_ => new int[0x70 / 4]).ToArray(), Enumerable.Range(0, 32).Select(_ => new int[0xC8 / 4]).ToArray(), new ushort[4096 * 8], []);
        Validate();
        if (state.Request != request || state.Mission != mission)
        {
            throw new InvalidDataException("The FFVII submarine continuation lost its actual field departure owner.");
        }
        if (restored is { Phase: Ff7SubmarinePhase.Introduction })
        {
            throw new InvalidDataException("The FFVII submarine save cannot contain an uninitialized gameplay allocation.");
        }
        state = Clone(state);
        if (restored is null)
        {
            foreach (var initial in resources.InitialGlobals)
            {
                WriteGlobal(initial.Key, initial.Value);
            }
            state = state with { RandomSeed = (uint)NextRandom() };
            InitializeTerrain();
            InitializePlayer();
            InitializeShips();
            InitializeModule();
            InitializeMines();
        }
        Validate();
    }

    public FieldHostRequest Request => state.Request;
    public long Tick => state.Tick;
    public ushort PreviousInput => state.PreviousInput;
    public bool Finished => state.Phase == Ff7SubmarinePhase.Completed;
    public uint RandomSeed => state.RandomSeed;
    public Ff7SubmarinePhase Phase => state.Phase;
    public ushort NativeFieldResult => Finished ? (ushort)(state.Globals[0xE74768] == 1 ? 1 : 0) : throw new InvalidOperationException("The native submarine mission has not returned.");
    public Ff7SubmarineSnapshot Capture() => Clone(state);

    internal (int Flags, ushort PreviousInput, int SpeedIndex, bool LauncherReady, bool TargetLocked, int DirectionX, int DirectionY, int DirectionZ) ReadAutoPlayFacts()
    {
        return (ReadGlobal(0xE7476C), state.PreviousInput, Read32(state.Player, 0xA0), Enumerable.Range(0, 4).Any(index => Read32(state.Player, 0xAC + index * 4) == 0x20000), (Read32(state.Ships[0], 0x38) & 0x800) != 0, Read16(state.Ships[0], 0x14), Read16(state.Ships[0], 0x16), Read16(state.Ships[0], 0x18));
    }

    private static Ff7SubmarineSnapshot Clone(Ff7SubmarineSnapshot value)
    {
        return value with { Player = value.Player.ToArray(), Ships = value.Ships.Select(ship => ship.ToArray()).ToArray(), Torpedoes = value.Torpedoes.Select(torpedo => torpedo.ToArray()).ToArray(), Cells = value.Cells.ToArray(), Globals = new(value.Globals) };
    }

    private static int Read32(int[] data, int offset) => data[offset / 4];
    private static void Write32(int[] data, int offset, int value) => data[offset / 4] = value;
    private static short Read16(int[] data, int offset) => unchecked((short)(data[offset / 4] >> ((offset & 2) * 8)));
    private static void Write16(int[] data, int offset, int value)
    {
        int shift = (offset & 2) * 8;
        data[offset / 4] = data[offset / 4] & ~(65535 << shift) | (value & 65535) << shift;
    }

    private int ReadGlobal(int address)
    {
        return address >= 0x987338 && address < 0x9874F0 ? Read32(state.Player, address - 0x987338) : state.Globals.GetValueOrDefault(address);
    }

    private void WriteGlobal(int address, int value)
    {
        if (address >= 0x987338 && address < 0x9874F0)
        {
            Write32(state.Player, address - 0x987338, value);
        }
        else
        {
            state.Globals[address] = value;
        }
    }
}
