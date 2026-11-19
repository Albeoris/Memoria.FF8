using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticCompletionStateTests
{
    [Fact]
    public void Completion_observation_bounds_and_detaches_callback_phase_scalars()
    {
        BattleSession battle = new(new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        FieldInfo field = typeof(BattleSession).GetField("diagnosticFollowupCallbacks", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing callback owner.");
        IDictionary callbacks = (IDictionary)(Activator.CreateInstance(field.FieldType) ?? throw new InvalidOperationException("Missing callback collection."));
        Type phaseType = field.FieldType.GetGenericArguments()[1];
        for (int index = 0; index < 20; index++)
        {
            object phase = Activator.CreateInstance(phaseType, [(long)index, (long?)(index + 1), null]) ?? throw new InvalidOperationException("Missing callback phase.");
            callbacks.Add(new BattleExpandedAiCallbackRequest(new BattlePartyActorReference("ally-" + index), 2), phase);
        }
        field.SetValue(battle, callbacks);
        object observed = battle.ObserveDiagnosticInvocationCompletionState();
        JsonElement json = JsonSerializer.SerializeToElement(observed);
        Assert.Equal(20, json.GetProperty("CallbackCount").GetInt32());
        JsonElement phases = json.GetProperty("Callbacks");
        Assert.Equal(16, phases.GetArrayLength());
        Assert.Equal("ally-15", phases[15].GetProperty("Actor").GetString());
        Assert.Equal(15, phases[15].GetProperty("Queued").GetInt64());
        Assert.Equal(16, phases[15].GetProperty("Started").GetInt64());
        Assert.Equal(JsonValueKind.Null, phases[15].GetProperty("Completed").ValueKind);
        Assert.Equal(0, json.GetProperty("TextureCount").GetInt32());
        Assert.False(json.GetProperty("FollowupRetirementCaptured").GetBoolean());
        string before = JsonSerializer.Serialize(observed);
        callbacks.Clear();
        Assert.Equal(before, JsonSerializer.Serialize(observed));
        Assert.Equal(0, JsonSerializer.SerializeToElement(battle.ObserveDiagnosticInvocationCompletionState()).GetProperty("CallbackCount").GetInt32());
    }
}
