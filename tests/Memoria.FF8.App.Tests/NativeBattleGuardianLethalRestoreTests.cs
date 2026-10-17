using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianLethalRestoreTests
{
    [Fact]
    public void Named20RealBoostLethalResultRestoresAfterTheNextOutcomePublication()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PREFIX_SNAPSHOT");
        Assert.SkipUnless(source is not null, "The exact20 catalog/cold-BD9 standalone is required.");
        byte[] bytes = File.ReadAllBytes(source!);
        Assert.Equal("B3C4556737E5E0AFAF02AE50995E1B76B49211720CAB231A8BCF83EE7BE4F4F1", Convert.ToHexString(SHA256.HashData(bytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        BattleSession battle = BattleSession.Restore(original);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
        BattleSnapshot lethal = battle.Capture();
        Assert.Equal(254L, lethal.Frame);
        Assert.Equal(334L, lethal.NativeGraphics!.Callback);
        BattleCombatantSnapshot target = Assert.Single(lethal.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        Assert.Equal(0, target.CurrentHp);
        Assert.NotEqual(0, target.Atb);
        Console.WriteLine($"Post-Boost snapshot frame{lethal.Frame}/callback{lethal.NativeGraphics.Callback}: Outcome={lethal.Outcome}, target3 HP{target.CurrentHp}/ATB{target.Atb}, ActionPause={lethal.NativeClock?.ActionPause}, ActiveHeader={lethal.NativeActionPackets!.ActiveHeaderIndex}.");
        Assert.Equal(BattleOutcome.Active, lethal.Outcome);
        InvalidDataException immediate = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(lethal))!));
        Assert.Contains("Battle outcome contradicts", immediate.Message);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
        BattleSnapshot settled = battle.Capture();
        Assert.Equal(255L, settled.Frame);
        Assert.Equal(335L, settled.NativeGraphics!.Callback);
        Assert.Equal(BattleOutcome.Victory, settled.Outcome);
        target = Assert.Single(settled.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        Assert.Equal(0, target.CurrentHp);
        Assert.NotEqual(0, target.Atb);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(settled))!);
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(restored.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, original.Definition, true);
        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
        Assert.Equal(pixels, scene.Render(restored, 320, 224).ToArray());
        foreach ((int hp, int atb) in new[] { (-1, target.Atb), (original.Definition.Combatants.Single(actor => actor.Id == target.Id).MaximumHp + 1, target.Atb), (0, BattleSession.AtbMaximum + 1) })
        {
            BattleCombatantSnapshot[] actors = settled.Combatants.Select(actor => actor.Id == target.Id ? actor with { CurrentHp = hp, Atb = atb } : actor).ToArray();
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(settled with { Combatants = actors }));
        }
        Assert.Equal("B3C4556737E5E0AFAF02AE50995E1B76B49211720CAB231A8BCF83EE7BE4F4F1", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Post-Boost254/334 is a strict unlatched-outcome diagnostic; next actual255/335 publishesVictory with target3 HP0/ATB{target.Atb}. Independent JSON restore and copied renders match without saved-state edits.");
    }
}
