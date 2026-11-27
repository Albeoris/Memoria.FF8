using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private SceneLightingClock? sceneLightingClock;
    private bool sceneLightingContextRestored;
    private string? sceneLightingEncounter;
    private SceneFieldLighting? sceneLightingBattleField;

    private SceneLightingProfile LightingProfile => (modelOverrides as ISceneLightingProfileSource)?.SceneLightingProfile ?? SceneLightingProfile.Empty;

    internal SceneLightingState? ReadSceneLighting()
    {
        SceneLightingProfile profile = LightingProfile;
        if (ReferenceEquals(profile, SceneLightingProfile.Empty))
        {
            return null;
        }
        GameState state = session.State;
        int moment = ReadSceneLightingMoment(state);
        double seconds = GetSceneLightingClock(profile, state).Seconds;
        if (battle is not null)
        {
            SceneFieldLighting field = ResolveSceneLightingBattleField(profile, state, moment);
            return profile.ResolveScene(true, battle.EntryStageId ?? -1, moment, field, IsWorldBattle(state), seconds);
        }
        return state.Scene switch
        {
            SceneKind.WorldMap => profile.ResolveScene(false, -1, moment, default, true, seconds),
            SceneKind.Field => profile.ResolveFieldState(profile.ResolveField(state.FieldName, moment), moment, seconds),
            _ => null
        };
    }

    internal void AdvanceSceneLighting(TimeSpan elapsed, bool battleTransition)
    {
        SceneLightingProfile profile = LightingProfile;
        if (!profile.ClockEnabled)
        {
            return;
        }
        GameState state = session.State;
        if (state.Scene == SceneKind.Title)
        {
            sceneLightingClock = null;
            sceneLightingContextRestored = false;
            sceneLightingEncounter = null;
            return;
        }
        int moment = ReadSceneLightingMoment(state);
        SceneLightingClock clock = GetSceneLightingClock(profile, state);
        bool inBattle = battleTransition || battle is not null;
        SceneFieldLighting field;
        if (inBattle)
        {
            field = ResolveSceneLightingBattleField(profile, state, moment);
        }
        else
        {
            sceneLightingEncounter = null;
            sceneLightingBattleField = null;
            field = state.Scene == SceneKind.WorldMap ? default : profile.ResolveField(state.FieldName, moment);
        }
        clock.Advance(profile.Cycle, elapsed, inBattle, field.Time != SceneTime.Cycle);
    }

    private GameState PrepareSceneLightingSnapshot(GameState state)
    {
        SceneLightingProfile profile = LightingProfile;
        if (!profile.ClockEnabled || state.Scene == SceneKind.Title)
        {
            return state;
        }
        SceneLightingClock clock = GetSceneLightingClock(profile, state);
        SceneFieldLighting? battleField = state.PendingBattleEncounter is null || IsWorldBattle(state)
            ? null : ResolveSceneLightingBattleField(profile, state, ReadSceneLightingMoment(state));
        if (profile.ClockPerSave)
        {
            return state with { SceneClock = new(clock.Seconds, clock.Paused, battleField) };
        }
        if (state.SceneClock is { } saved)
        {
            return state with { SceneClock = saved with { BattleField = battleField } };
        }
        return battleField is null
            ? state
            : state with { SceneClock = new(profile.Cycle.Wrap(profile.Cycle.Fade / 2 + (state.Playtime?.Seconds ?? 0)), false, battleField) };
    }

    private SceneLightingClock GetSceneLightingClock(SceneLightingProfile profile, GameState state)
    {
        if (!sceneLightingContextRestored)
        {
            sceneLightingContextRestored = true;
            sceneLightingEncounter = state.PendingBattleEncounter?.OccurrenceId;
            sceneLightingBattleField = state.SceneClock?.BattleField;
        }
        if (!profile.ClockPerSave && modelOverrides is GameResourceHost host)
        {
            return host.ReadGlobalSceneClock(profile.Cycle);
        }
        return sceneLightingClock ??= new(profile.Cycle, state.SceneClock, state.Playtime?.Seconds ?? 0);
    }

    private SceneFieldLighting ResolveSceneLightingBattleField(SceneLightingProfile profile, GameState state, int moment)
    {
        if (IsWorldBattle(state))
        {
            return default;
        }
        string? encounter = state.PendingBattleEncounter?.OccurrenceId;
        if (sceneLightingBattleField is not { } field || sceneLightingEncounter != encounter)
        {
            field = profile.ResolveField(state.FieldName, moment);
            sceneLightingEncounter = encounter;
            sceneLightingBattleField = field;
        }
        return field;
    }

    private static bool IsWorldBattle(GameState state) => (state.PendingBattleEncounter?.SourceScene ?? state.Scene) == SceneKind.WorldMap;

    private static int ReadSceneLightingMoment(GameState state)
    {
        if (state.EventMemory.HasRepresentedExactBytes(256, 2))
        {
            return state.EventMemory.Read(256, 2).RequireExact();
        }
        return state.WorldEntryMemory?.StoryWord ?? -1;
    }
}
