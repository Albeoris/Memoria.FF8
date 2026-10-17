using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMidBattleSaveMigrationTests
{
    [Fact]
    public void Migrate_named_midbattle_snapshot_and_validate_independent_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_MIDBATTLE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MIDBATTLE_OUTPUT");
        string? expectedHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_MIDBATTLE_SHA256");
        Assert.SkipUnless(source is not null && output is not null && expectedHash is not null, "Configure a named source, SHA256 and distinct migration output.");
        Assert.NotEqual(Path.GetFullPath(source!), Path.GetFullPath(output!));
        byte[] originalBytes = File.ReadAllBytes(source!);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        JsonNode original = JsonNode.Parse(originalBytes)!;
        JsonNode migrated = original.DeepClone();
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectMemory cold = assets.ReadColdProcessNativeBattleMemory().EffectMemory!;
        Assert.NotNull(cold.QuezacotlExecutableMemory);
        foreach (JsonNode owner in new[]
        {
            migrated["NativeBattleMemory"]!["EffectMemory"]!,
            migrated["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!,
            migrated["Battle"]!["NativeEffectMemory"]!
        })
        {
            Assert.Null(owner["QuezacotlExecutableMemory"]);
            Assert.Empty(owner["GuardianWorkBuffer"]!["Regions"]!.AsArray());
            owner["QuezacotlExecutableMemory"] = JsonSerializer.SerializeToNode(cold.QuezacotlExecutableMemory);
        }
        NativeMidBattleSaveMigration.MigratePersistentFlags(migrated, assets);
        JsonNode reverted = migrated.DeepClone();
        foreach (JsonNode owner in new[] { reverted["NativeBattleMemory"]!["EffectMemory"]!, reverted["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!, reverted["Battle"]!["NativeEffectMemory"]! })
        {
            owner["QuezacotlExecutableMemory"] = null;
        }
        reverted["EventState"]!["Scene"]!.AsObject().Remove("NativeBattleFlagWrites");
        reverted["EventState"]!["Scene"]!["NativeBattleCompletions"]![0]!["FlagConsumer"]!["PersistentModeFlags"] = null;
        foreach (JsonNode timer in new[] { reverted["ExamTimer"]!, reverted["EventState"]!["ExamTimer"]!, reverted["Battle"]!["NativeExamTimer"]!, reverted["Battle"]!["Definition"]!["NativeExamTimerAtEntry"]! })
        {
            timer["Event183Mask1"] = null;
        }
        reverted["Battle"]!["Definition"]!["NativeFieldBattleMode"] = 0;
        Assert.True(JsonNode.DeepEquals(original, reverted));
        Assert.Equal(original["Battle"]!["Frame"]!.ToJsonString(), migrated["Battle"]!["Frame"]!.ToJsonString());
        Assert.Equal(original["Battle"]!["NativeHostCallbacks"]!.ToJsonString(), migrated["Battle"]!["NativeHostCallbacks"]!.ToJsonString());
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        GameSession session = new(GameSession.Load(output!));
        JsonElement savedState = JsonSerializer.SerializeToElement(session.State);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(JsonElement.DeepEquals(savedState, JsonSerializer.SerializeToElement(session.State)));
        long before = world.Battle!.NativeHostCallbacks!.Value;
        world.Battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
        world.SynchronizeBattlePresentation();
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        Assert.True(world.Battle.NativeHostCallbacks > before);
        string continuation = TestResources.ArtifactFile("midbattle-continued.json");
        session.Save(continuation, compact: true);
        GameSession restored = new(GameSession.Load(continuation));
        using GameWorld second = new(restored, TestResources.GameDirectory);
        second.EnsureField();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Migrated source preserved; output SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; next native callback={world.Battle.NativeHostCallbacks}.");
    }
    [Fact]
    public void Migrated_snapshot_returns_to_the_field_after_victory()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_MIDBATTLE_OUTPUT");
        Assert.SkipUnless(path is not null && File.Exists(path), "Configure the separately migrated standalone.");
        GameSession session = new(GameSession.Load(path!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.Equal(BattleOutcome.Victory, world.Battle!.Outcome);
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        string destination = TestResources.ArtifactFile("midbattle-victory.json");
        session.Save(destination, compact: true);
        GameSession restored = new(GameSession.Load(destination));
        using GameWorld second = new(restored, TestResources.GameDirectory);
        second.EnsureField();
        Assert.Null(second.Battle);
        Console.WriteLine($"Victory return restored: field={session.State.FieldId}, frame={world.Events.Frame}.");
    }

}
