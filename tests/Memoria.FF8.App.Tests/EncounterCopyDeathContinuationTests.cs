using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterCopyDeathContinuationTests
{
    [Fact]
    public void DefeatedSummonedCopyReleasesItsInstalledModelAndRestoresIdentically()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENCOUNTER_COPY_DEATH_FIXTURE");
        Assert.SkipUnless(Directory.Exists(directory), "Configured installed native resources are required.");
        Assert.SkipUnless(fixture is not null && File.Exists(fixture), "Configure a standalone encounter with a defeated summoned copy awaiting graphical removal.");
        GameSession session = new(GameSession.Load(fixture!));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        BattleSession battle = world.Battle ?? throw new InvalidDataException("The fixture requires an active native battle.");
        battle.SetMode(BattleAtbMode.TurnBased);
        BattleSnapshot source = battle.Capture();
        string original = source.Definition.EncounterMod!.OriginalActorId;
        NativeBattleEnemyAllocation[] defeated = source.NativeAllocations.Where(allocation => allocation.EncounterBatchId is not null && battle.Combatants.Single(actor => actor.Id == allocation.Id) is { IsActive: true, IsAlive: false }).ToArray();
        Assert.NotEmpty(defeated);
        int originalHp = battle.Combatants.Single(actor => actor.Id == original).CurrentHp;
        BattleSession restored = (BattleSession)typeof(GameWorld).GetMethod("AttachNativeEffectResources", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!)])!;
        for (int callback = 0; callback < 1000; callback++)
        {
            battle.Tick();
            restored.Tick();
            if (defeated.All(allocation => battle.Capture().NativeAllocations.Single(current => current.Id == allocation.Id).Retirement is { ResourcesReleased: true }))
            {
                break;
            }
        }
        foreach (NativeBattleEnemyAllocation allocation in defeated)
        {
            EncounterAllocationRetirement retirement = battle.Capture().NativeAllocations.Single(current => current.Id == allocation.Id).Retirement!;
            Assert.NotNull(retirement);
            Assert.True(retirement.ResourcesReleased);
            Assert.Equal(0, battle.NativeGraphics!.Models[retirement.PhysicalIndex].Flags0 & 3);
            Assert.False(battle.Combatants.Single(actor => actor.Id == allocation.Id).IsActive);
            Assert.Single(battle.Events, entry => entry.Kind == "NativeModelRemoved" && entry.ActorId == allocation.Id);
        }
        Assert.Equal(originalHp, battle.Combatants.Single(actor => actor.Id == original).CurrentHp);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.False(battle.EncounterPolicies!.CleanedUp);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
