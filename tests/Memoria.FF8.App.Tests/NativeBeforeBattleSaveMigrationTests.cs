using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBeforeBattleSaveMigrationTests
{
    [Fact]
    public void Explicitly_migrate_the_named_before_battle_checkpoint_without_changing_its_gameplay_moment()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEFORE_BATTLE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEFORE_BATTLE_OUTPUT");
        string? expectedHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEFORE_BATTLE_SHA256");
        string? profilePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEFORE_BATTLE_STACK_PROFILE");
        string? profileHash = Environment.GetEnvironmentVariable("MEMORIA_FF8_BEFORE_BATTLE_STACK_SHA256");
        Assert.SkipUnless(source is not null && output is not null && expectedHash is not null && profilePath is not null && profileHash is not null, "Select an exact standalone source, distinct output and authenticated execution environment explicitly.");
        Assert.NotEqual(Path.GetFullPath(source!), Path.GetFullPath(output!));
        byte[] sourceBytes = File.ReadAllBytes(source!);
        byte[] profileBytes = File.ReadAllBytes(profilePath!);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(sourceBytes)));
        Assert.Equal(profileHash, Convert.ToHexString(SHA256.HashData(profileBytes)));
        NativeBattleExecutionStack profile = JsonSerializer.Deserialize<NativeBattleExecutionStack>(profileBytes)!;
        profile.Validate();
        GameState before = GameSession.Load(source!);
        Assert.Equal(SceneKind.Field, before.Scene);
        Assert.Null(before.Battle);
        Assert.Null(before.PendingBattleEncounter);
        NativeBattleEffectMemory memory = before.NativeBattleMemory!.EffectMemory!;
        Assert.Null(memory.QuezacotlExecutableMemory);
        Assert.Null(memory.ExecutionStack);
        Assert.Empty(memory.GuardianWorkBuffer!.Regions);
        Assert.Null(memory.Arena);
        Assert.Equal(0, memory.AllocationCount);
        Assert.Equal(0, memory.Active);
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleQuezacotlExecutableMemory executable = assets.ReadColdProcessNativeBattleMemory().EffectMemory!.QuezacotlExecutableMemory!;
        Assert.Equal(profile.ExecutableHash, before.NativeBattleMemory.ColdExecutableSha256);
        JsonNode original = JsonNode.Parse(sourceBytes)!;
        JsonNode migrated = original.DeepClone();
        JsonObject owner = migrated["NativeBattleMemory"]!["EffectMemory"]!.AsObject();
        owner["QuezacotlExecutableMemory"] = JsonSerializer.SerializeToNode(executable);
        owner["ExecutionStack"] = JsonSerializer.SerializeToNode(profile);
        JsonNode reverted = migrated.DeepClone();
        reverted["NativeBattleMemory"]!["EffectMemory"]!["QuezacotlExecutableMemory"] = null;
        Assert.True(reverted["NativeBattleMemory"]!["EffectMemory"]!.AsObject().Remove("ExecutionStack"));
        Assert.True(JsonNode.DeepEquals(original, reverted));
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        GameSession session = new(GameSession.Load(output!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        GameSession independent = new(GameSession.Load(output!));
        using GameWorld clone = new(independent, TestResources.GameDirectory);
        clone.EnsureField();
        using GameWorld originalWorld = new(new GameSession(before), TestResources.GameDirectory);
        originalWorld.EnsureField();
        Assert.Equal(originalWorld.Render(TimeSpan.Zero), world.Render(TimeSpan.Zero));
        long frame = world.Events.Frame;
        world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1, WaitWhilePlayable: true));
        clone.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1, WaitWhilePlayable: true));
        Assert.True(world.Events.Frame > frame);
        session.SetEventState(world.Events.Capture());
        independent.SetEventState(clone.Events.Capture());
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
        Assert.Equal(world.Render(TimeSpan.Zero), clone.Render(TimeSpan.Zero));
        Assert.Null(session.State.NativeBattleMemory!.EffectMemory!.Parameter82);
        session.Save(output! + ".continued.json");
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Original preserved; only retained executable bytes and explicitly selected execution stack added. Output SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; independent next field frame={world.Events.Frame}; initial pixels unchanged; historical parameter82 remains unknown.");
    }
}
