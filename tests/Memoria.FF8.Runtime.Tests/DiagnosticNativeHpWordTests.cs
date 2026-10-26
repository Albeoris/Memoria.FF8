using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticNativeHpWordTests
{
    [Theory]
    [InlineData(0, (short)100, 100)]
    [InlineData(500, (short)100, 600)]
    [InlineData(100, (short)-101, -1)]
    [InlineData(int.MaxValue, (short)1, int.MinValue)]
    [InlineData(int.MinValue, (short)-1, int.MaxValue)]
    [InlineData(100, short.MinValue, -32668)]
    public void Opcode60_observation_preserves_signed_word_addition_raw_flags_and_complete_state(int before, short amount, int expected)
    {
        ushort operand = unchecked((ushort)amount);
        byte[] script = [60, unchecked((byte)operand), unchecked((byte)(operand >> 8)), 0];
        BattleSession observed = Create(script, before);
        BattleSession unobserved = Create(script, before);
        BattleDiagnosticCombatant initial = observed.ObserveDiagnosticCombatants().Single(actor => actor.Id == "enemy");
        List<BattleDiagnosticResolution> receipts = [];
        observed.DiagnosticResolution += receipts.Add;

        Execute(observed);
        Execute(unobserved);

        BattleDiagnosticResolution resolution = Assert.Single(receipts);
        BattleDiagnosticNativeHpWord proof = Assert.IsType<BattleDiagnosticNativeHpWord>(resolution.NativeHpWord);
        Assert.Equal("native_ai_hp_word", resolution.Kind);
        Assert.Null(resolution.ActorId);
        Assert.Equal(before, resolution.BeforeHp);
        Assert.Equal(expected, resolution.AfterHp);
        Assert.Equal(amount, resolution.ResolvedAmount);
        Assert.Equal(operand, proof.Operand);
        Assert.Equal(unchecked((ushort)before), proof.BeforeHpWord);
        Assert.Equal(unchecked((ushort)expected), proof.AfterHpWord);
        Assert.Equal(script, proof.ScriptBytes.ToArray());
        Assert.Equal(0, proof.ScriptIndex);
        Assert.Equal(0, proof.ProgramCounter);
        BattleDiagnosticCombatant final = observed.ObserveDiagnosticCombatants().Single(actor => actor.Id == "enemy");
        Assert.Equal(initial.Statuses0, final.Statuses0);
        Assert.Equal(initial.Statuses1, final.Statuses1);
        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(observed.Capture()));
    }

    private static void Execute(BattleSession battle)
    {
        MethodInfo interpret = typeof(BattleSession).GetMethod("InterpretNativeAi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        interpret.Invoke(battle, [battle.Combatants.Single(actor => actor.Id == "enemy"), 0]);
    }

    private static BattleSession Create(byte[] script, int hp)
    {
        NativeBattleAiDefinition ai = new([script, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 500, CurrentHp = 100, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        // Native raw DWORD values can exceed initial fixture bounds without an HP-commit flag refresh.
        FieldInfo rawHp = typeof(BattleCombatant).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        rawHp.SetValue(battle.Combatants.Single(actor => actor.Id == "enemy"), hp);
        return battle;
    }
}
