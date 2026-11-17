namespace Memoria.FF8.Runtime;

public readonly struct BattlePresentationUiState
{
    private readonly NativeBattleGraphicsState? graphics;
    private readonly NativeBattleUiServices? services;
    private readonly bool vitalsVisible;

    public BattlePresentationUiState(bool commandMenuHidden, bool vitalsVisible, bool guardianBoostOwnsInput, bool paused)
    {
        CommandMenuHidden = commandMenuHidden;
        this.vitalsVisible = vitalsVisible;
        GuardianBoostOwnsInput = guardianBoostOwnsInput;
        Paused = paused;
    }

    internal BattlePresentationUiState(NativeBattleGraphicsState graphics, NativeBattleUiServices services)
    {
        this.graphics = graphics;
        this.services = services;
        CommandMenuHidden = (services.Flags942 & 0x841) != 0x40;
        GuardianBoostOwnsInput = (services.Flags942 & 1) != 0;
        Paused = services.PauseDE9;
    }

    public bool CommandMenuHidden { get; }
    public bool GuardianBoostOwnsInput { get; }
    public bool Paused { get; }
    public bool VitalsVisible => services is { } ui ? Windows.DrawsVitals4B1740(ui.Flags942) : vitalsVisible;

    public bool AllowsVitalsRow(string actorId)
    {
        if (graphics is not { } state)
        {
            return vitalsVisible;
        }
        for (byte row = 0; row < NativeBattleSlots.AllyCount; row++)
        {
            BattleModelOwner? owner = state.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(row), state.ActorModels);
            if (owner is BattlePartyActorModel party && party.Actor.ActorId == actorId)
            {
                return Windows.IsVitalsRowVisible4B1740(row);
            }
        }
        return state.ActorModels.Roster.ContainsPartyActor(actorId);
    }

    private NativeBattleUiWindows Windows => services?.Windows ?? throw new NotSupportedException("Battle HUD requires its retained UI window bank. Migrate the named snapshot explicitly.");
}

public sealed partial class BattlePresentationProcess
{
    public BattlePresentationUiState UiState
    {
        get
        {
            NativeBattleGraphicsState graphics = Graphics;
            if (graphics.UiServices is { } services)
            {
                return new(graphics, services);
            }
            if (graphics.EntryReady)
            {
                throw new InvalidOperationException("Battle entry completed without publishing its UI state.");
            }
            return new(commandMenuHidden: true, vitalsVisible: false, guardianBoostOwnsInput: false, paused: false);
        }
    }
}
