using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public enum Ff7CondorPhase
{
    Introduction,
    Deployment,
    StartConfirmation,
    Starting,
    CombatIntroduction,
    Combat,
    Overrun,
    Returning,
    Completed
}

public sealed record Ff7CondorSnapshot(FieldHostRequest Request, int Attack, uint Funds, uint RandomSeed, long Tick, Ff7CondorPhase Phase, Ff7CondorUnitState[] Enemies, int NextEnemy, uint SpawnClock, ushort ClockFrames, ushort ClockMinutes, int IntroState, int Alpha, int FadeAcceleration, int CursorY, int MapY, int TextY, int MenuSelection, int OutcomeCounter, ushort PreviousInput);

public sealed record Ff7CondorFrame(Ff7CondorPhase Phase, uint Funds, IReadOnlyList<Ff7CondorUnitState> Enemies, int MenuSelection, int TextY, int Alpha, int MapY, bool CanAcknowledge);

public sealed class Ff7CondorRuntime
{
    private readonly Ff7CondorResources resources;
    private readonly IReadOnlyList<Ff7CondorEnemyWave> wave;
    private Ff7CondorSnapshot state;

    public Ff7CondorRuntime(Ff7CondorResources resources, FieldHostRequest request, int attack, uint funds, uint randomSeed, Ff7CondorSnapshot? restored = null)
    {
        if (request.Command is not FieldMinigameCommand { Game: 3 } || attack != 6)
        {
            throw new NotSupportedException("The FFVII Condor module requires its supported native attack and departure owner.");
        }
        this.resources = resources;
        wave = resources.ReadWave(attack);
        state = restored ?? new(request, attack, funds, randomSeed, 0, Ff7CondorPhase.Introduction, [], 0, 0, 0, 0, 0, 0, 0, 96, 0, 96, 16, 0, 0);
        Validate(state, request, attack);
        state = state with { Enemies = state.Enemies.ToArray() };
    }

    public FieldHostRequest Request => state.Request;
    public Ff7CondorPhase Phase => state.Phase;
    public uint Funds => state.Funds;
    public uint RandomSeed => state.RandomSeed;
    public byte NativeFieldResult => Finished && state.Enemies.Any(Ff7CondorUnit.ReachedShed) ? (byte)1 : throw new InvalidOperationException("The FFVII Condor native overrun has not returned.");
    public bool Finished => Phase == Ff7CondorPhase.Completed;
    public Ff7CondorFrame Frame => new(Phase, Funds, Array.AsReadOnly(state.Enemies.ToArray()), state.MenuSelection, state.TextY, state.Alpha, state.MapY, Phase == Ff7CondorPhase.Overrun && state.TextY >= 64);
    public string NativeProgress => $"{Phase}:{state.MenuSelection}:" + string.Join(';', state.Enemies.Select(unit => $"{unit.Slot},{unit.PathIndex},{unit.X},{unit.Y}"));
    public Ff7CondorSnapshot Capture() => state with { Enemies = state.Enemies.ToArray() };

    public void Advance(ushort held, ushort pressed)
    {
        if (Finished)
        {
            return;
        }
        state = state with { Tick = checked(state.Tick + 1), PreviousInput = held };
        switch (Phase)
        {
            case Ff7CondorPhase.Introduction:
            case Ff7CondorPhase.CombatIntroduction:
                AdvanceIntroduction();
                break;
            case Ff7CondorPhase.Deployment:
                if ((pressed & 0x40) != 0)
                {
                    state = state with { Phase = Ff7CondorPhase.StartConfirmation, MenuSelection = 16 };
                }
                else if (pressed != 0)
                {
                    throw new NotSupportedException("FFVII Condor friendly deployment requires its native unit command contract.");
                }
                break;
            case Ff7CondorPhase.StartConfirmation:
                if ((pressed & ~0x1020) != 0 || (pressed & 0x20) != 0 && state.MenuSelection != 0)
                {
                    throw new NotSupportedException("The FFVII Condor start menu requires its supported native input branch.");
                }
                if ((pressed & 0x1000) != 0)
                {
                    state = state with { MenuSelection = Math.Max(0, state.MenuSelection - 16) };
                }
                if ((pressed & 0x20) != 0)
                {
                    state = state with { Phase = Ff7CondorPhase.Starting };
                }
                break;
            case Ff7CondorPhase.Starting:
                state = state with { Phase = Ff7CondorPhase.CombatIntroduction, IntroState = 0, CursorY = 96, MapY = 0, TextY = 96 };
                break;
            case Ff7CondorPhase.Combat:
                if (held != 0 || pressed != 0)
                {
                    throw new NotSupportedException("FFVII Condor unit commands require their native gameplay contract.");
                }
                AdvanceCombat();
                break;
            case Ff7CondorPhase.Overrun:
                int counter = state.OutcomeCounter + 1;
                int textY = Math.Min(64, state.TextY + 1);
                bool acknowledged = textY >= 64 && (pressed & 0x60) != 0;
                state = state with { OutcomeCounter = counter, TextY = textY, Phase = acknowledged || counter > 600 ? Ff7CondorPhase.Returning : Phase };
                break;
            case Ff7CondorPhase.Returning:
                state = state with { Phase = Ff7CondorPhase.Completed };
                break;
        }
    }

    private void AdvanceIntroduction()
    {
        if (state.IntroState == 0)
        {
            state = state.Alpha < 128 ? state with { Alpha = Math.Min(128, state.Alpha + state.FadeAcceleration), FadeAcceleration = state.FadeAcceleration + 2 } : state with { IntroState = 1 };
            return;
        }
        if (state.IntroState == 1)
        {
            if (Phase == Ff7CondorPhase.Introduction)
            {
                state = state with { IntroState = 2 };
            }
            else
            {
                int textY = state.TextY - 2;
                state = state with { TextY = textY, IntroState = textY == -24 ? 2 : 1 };
            }
            return;
        }
        for (int index = 0; index < 3; index++)
        {
            int cursor = state.CursorY + 1;
            state = state with { CursorY = cursor, MapY = cursor > 104 ? state.MapY + 1 : state.MapY };
        }
        if (state.MapY >= 328)
        {
            state = state with { Phase = Phase == Ff7CondorPhase.Introduction ? Ff7CondorPhase.Deployment : Ff7CondorPhase.Combat };
        }
    }

    private void AdvanceCombat()
    {
        ushort clock = unchecked((ushort)(state.ClockFrames + 1));
        ushort minutes = state.ClockMinutes;
        if (clock == 3600)
        {
            clock = 0;
            minutes = unchecked((ushort)(minutes + 1));
        }
        state = state with { SpawnClock = unchecked(state.SpawnClock + 2), ClockFrames = clock, ClockMinutes = minutes };
        if (state.NextEnemy < wave.Count && state.Enemies.Length < 20 && wave[state.NextEnemy].SpawnTime <= state.SpawnClock / 60)
        {
            uint seed = unchecked(state.RandomSeed * 214013 + 2531011);
            ushort tint = (ushort)((seed >> 16 & 0x7FFF) & 0x3FF);
            Ff7CondorUnitState enemy = Ff7CondorUnit.Spawn(resources, wave[state.NextEnemy], 20 + state.Enemies.Length, tint);
            state = state with { RandomSeed = seed, Enemies = [.. state.Enemies, enemy], NextEnemy = state.NextEnemy + 1 };
        }
        Ff7CondorUnitState[] enemies = state.Enemies.Select(unit => Ff7CondorUnit.Advance(resources, unit)).ToArray();
        state = state with { Enemies = enemies };
        if (enemies.Any(Ff7CondorUnit.ReachedShed))
        {
            state = state with { Phase = Ff7CondorPhase.Overrun, OutcomeCounter = 0, TextY = -32 };
        }
    }

    private void Validate(Ff7CondorSnapshot snapshot, FieldHostRequest request, int attack)
    {
        if (snapshot.Request != request || snapshot.Attack != attack || snapshot.Tick < 0 || !Enum.IsDefined(snapshot.Phase) || snapshot.Enemies is null || snapshot.Enemies.Length > 20 || snapshot.NextEnemy != snapshot.Enemies.Length || snapshot.NextEnemy > wave.Count || snapshot.IntroState is < 0 or > 2 || snapshot.Alpha is < 0 or > 128 || snapshot.FadeAcceleration < 0 || snapshot.ClockFrames >= 3600 || snapshot.Enemies.Select(unit => unit.Slot).Distinct().Count() != snapshot.Enemies.Length)
        {
            throw new InvalidDataException("The FFVII Condor snapshot has invalid native execution ownership or state.");
        }
        if (snapshot.Phase is Ff7CondorPhase.Overrun or Ff7CondorPhase.Returning or Ff7CondorPhase.Completed && !snapshot.Enemies.Any(Ff7CondorUnit.ReachedShed))
        {
            throw new InvalidDataException("The FFVII Condor terminal state lacks its native enemy overrun.");
        }
        foreach (Ff7CondorUnitState unit in snapshot.Enemies)
        {
            if (unit.Slot is < 20 or >= 40 || unit.Type is < 16 or > 19 || unit.Interval != resources.ReadUnitType(unit.Type).Interval || unit.PathIndex < 0 || unit.PathIndex > resources.ReadPath(unit.Path).Count || unit.Cell < -1 || unit.Cell >= resources.TerrainCells.Count)
            {
                throw new InvalidDataException("The FFVII Condor snapshot has an invalid native enemy.");
            }
            int row = unit.Slot - 20;
            if (row >= snapshot.NextEnemy || unit.Type != wave[row].RowType || unit.Path != wave[row].Path || wave[row].Extra > 3 || unit.Cell >= 0 && !Ff7CondorGeometry.Contains(resources.TerrainCells[unit.Cell].Span, unchecked((short)(unit.X - 256)), unchecked((short)(unit.Y - 512))))
            {
                throw new InvalidDataException("The FFVII Condor snapshot enemy does not match its native wave and terrain.");
            }
        }
    }
}
