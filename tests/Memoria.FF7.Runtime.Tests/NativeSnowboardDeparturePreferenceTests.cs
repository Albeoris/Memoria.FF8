using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardDeparturePreferenceTests
{
    private const string Program = "A513104CED946295581FA2C752FA5E3804FC542FAF7DA93C594F3A47485F9658";

    internal static void Run()
    {
        Ff7AutoPlayTarget departure = new("line:1:entity:11:script:2", 11, 2, Ff7AutoPlayTargetKind.LinePush, "departure-cf", false, default);
        Ff7AutoPlayTarget optional = new("optional", 5, 1, Ff7AutoPlayTargetKind.ActorTalk, "optional-cf", false, default);
        Ff7AutoPlayObservation observation = new("visit", Program, 1, [optional, departure], true, true) { NavigationKey = "local" };
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 4, Model = 0, PlayerControlled = true, PartyCharacter = 0 }], Lines = [new(1, 11, 0, 0, 0, 10, 0, 0)] };
        FieldVariables Variables(byte flags = 0x42, byte party = 0)
        {
            FieldVariables variables = new();
            variables.WriteByte(FieldBank.Persistent0Byte, 0x82, flags);
            variables.WriteByte(FieldBank.Persistent1Byte, 9, party);
            return variables;
        }
        FieldVariables variables = Variables();
        string native = JsonSerializer.Serialize(new { process, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() });
        Check(Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation, departure, process, variables), "The current native ready LinePush departure was not preferred.");
        foreach (byte flags in new byte[] { 0, 2, 0x40, 0xC2 })
        {
            Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation, departure, process, Variables(flags)), "Missing acquisition or a completed-departure flag received preference.");
        }
        Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation, departure, process, Variables(party: 1)), "A different native party0 received departure preference.");
        foreach (Ff7AutoPlayTarget target in new[] { departure with { Script = 3, Kind = Ff7AutoPlayTargetKind.LineCross }, departure with { AnalysisComplete = false }, departure with { Entity = 12 } })
        {
            Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation with { Events = [target] }, target, process, variables), "A RET Cross entry or unrelated/incomplete target received departure preference.");
        }
        Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation with { ProgramIdentity = "other" }, departure, process, variables), "An unknown profile received departure preference.");
        Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation, departure, process with { Lines = [] }, variables), "A missing native LINE received preference.");
        Check(!Ff7AutoPlayPolicy.IsNativeSnowboardDeparture(observation, departure, process with { Actors = [process.Actors[0] with { PartyCharacter = 1 }] }, variables), "A different controlled character received preference.");

        Ff7AutoPlayState State()
        {
            Ff7AutoPlayState state = new();
            var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
            planner.Observe(new("visit", Program), [new(optional.Id, AutoPlayEventKind.Interaction, ConditionSignature: optional.ConditionFingerprint)]);
            state.Active = new("visit", optional.Id, optional.ConditionFingerprint, 1, optional.Entity, optional.Script, optional.Kind, null, 0, "before", NavigationKey: "local");
            state.NavigationGoal = new("local", optional.Id, optional.ConditionFingerprint);
            return state;
        }
        foreach (Action<Ff7AutoPlayState> change in new Action<Ff7AutoPlayState>[]
        {
            state => state.Active = state.Active! with { Accepted = true },
            state => state.Active = state.Active! with { Returned = true },
            state => state.Active = state.Active! with { TransitionOwned = true },
            state => state.Active = state.Active! with { Generation = 2 },
            state => state.Active = state.Active! with { NavigationKey = "other" },
            state => state.DestinationField = 600,
            state => state.NavigationGoal = new("remote", "other", "other"),
            state => state.TravelCommitment = new(new("remote", "other", "other"), "remote", null),
            state => state.PendingRestore = new("before-choice")
        })
        {
            Ff7AutoPlayState invalid = State();
            change(invalid);
            string before = JsonSerializer.Serialize(invalid);
            Check(!Ff7AutoPlayPolicy.RetireOptionalPlanForSnowboard(invalid, observation, process, variables) && before == JsonSerializer.Serialize(invalid), "Owned work, explicit intent or a changed source was retired.");
        }
        Ff7AutoPlayState valid = State();
        string history = JsonSerializer.Serialize(valid.Coverage);
        Check(Ff7AutoPlayPolicy.RetireOptionalPlanForSnowboard(valid, observation, process, variables) && valid.Active is null && valid.NavigationGoal is null, "The unrelated unaccepted local optional plan prevented native departure preference.");
        Check(history == JsonSerializer.Serialize(valid.Coverage) && native == JsonSerializer.Serialize(new { process, Persistent = variables.CopyPersistent(), Map = variables.CopyMap() }), "Preference rewrote native facts or completion history.");
        Ff7AutoPlayState local = State();
        local.TravelCommitment = new(local.NavigationGoal!, "local", null);
        Check(Ff7AutoPlayPolicy.RetireOptionalPlanForSnowboard(local, observation, process, variables) && local.Active is null && local.NavigationGoal is null && local.TravelCommitment is null, "An exact unaccepted local optional commitment prevented departure or survived its plan retirement.");
        Console.WriteLine("Native snowboard departure preference and optional-plan retirement guards passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
