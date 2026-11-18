using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FinalBattleReplacementCandidateTests
{
    [Fact]
    public void Installed_final_encounter_keeps_admitted_candidates_when_no_reserve_rows_remain()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        NativeCharacterVitals original = session.State.CharacterVitals!.DeepCopy();
        byte[] eligible = Enumerable.Range(0, Math.Min(8, original.Records.Length)).Where(row => (original.Records[row].MemberFlags & 1) != 0).Select(row => checked((byte)row)).ToArray();
        Assert.Equal(new byte[] { 0 }, eligible);
        BattleSession battle = world.StartDiagnosticBattle(new(511, 0, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [], true) { CombatParty = [0, 255, 255] }
        });
        BattleDefinition definition = battle.ReadDefinition();
        Assert.Equal(511, definition.EncounterId);
        BattleCombatantDefinition party = Assert.Single(definition.Combatants, actor => actor.IsPlayer);
        Assert.Equal(0, party.MagicRecordId);
        KeyValuePair<byte, BattleCombatantDefinition> candidate = Assert.Single(definition.PartyReplacementCandidates);
        Assert.Equal((byte)0, candidate.Key);
        Assert.Equal(party.Id, candidate.Value.Id);
        Assert.Equal(party.ModelId, candidate.Value.ModelId);
        Assert.Equal(party.CurrentHp, candidate.Value.CurrentHp);
        Assert.Equal(new byte[] { 0, 255, 255 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(original.Records[0].Experience, session.State.CharacterVitals.Records[0].Experience);
        Assert.Equal(original.Records[0].MagicInventory, session.State.CharacterVitals.Records[0].MagicInventory);
        Assert.Equal(0, battle.Frame);
    }
}
