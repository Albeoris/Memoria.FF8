using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleStatusExpiryContinuationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Migrate_only_the_named_stale_sleep_graphics_and_validate_saved_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_EXPIRY_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_EXPIRY_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone save and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(output));
        byte[] originalBytes = File.ReadAllBytes(source!);
        GameState state = GameSession.Load(source!);
        BattleSnapshot original = Assert.IsType<BattleSnapshot>(state.Battle);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        NativeBattleGraphicsRules rules = original.Definition.NativeGraphics!.Rules;
        uint sleepFlag = rules.Project(0, 1);
        Assert.NotEqual(0u, sleepFlag);
        BattleSession session = BattleSession.Restore(original);
        MethodInfo apply = typeof(BattleSession).GetMethod("ApplyNativeDescriptorStatus502380", BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo address = typeof(BattleSession).GetMethod("NativeParticipantAddress", BindingFlags.NonPublic | BindingFlags.Static)!;
        int migratedCount = 0;
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            string? actorId = graphics.ActorIds[index];
            BattleCombatantSnapshot? actor = original.Combatants.SingleOrDefault(actor => actor.Id == actorId);
            if (actor?.NativeStatus is not { } status || (status.Battle & 1) != 0 || (graphics.Models[index].Flags8.GetValueOrDefault() & sleepFlag) == 0)
            {
                continue;
            }
            uint previous = graphics.Models[index].Flags8!.Value;
            uint projected = (previous & 0x1800000) | rules.Project(status.Saved, status.Battle);
            Assert.Equal(sleepFlag, previous ^ projected);
            byte[] bytes = new byte[12];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 117);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)address.Invoke(null, [index])!);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), checked((short)index));
            NativeBattleDescriptorPacket packet = new(ImmutableArray.CreateRange(bytes));
            graphics = (NativeBattleGraphicsState)apply.Invoke(session, [graphics, packet])!;
            Assert.Equal(projected, graphics.Models[index].Flags8);
            Assert.Equal(previous & 0x1800000, graphics.Models[index].Flags8!.Value & 0x1800000);
            migratedCount++;
        }
        Assert.True(migratedCount > 0, "The selected save contains no stale Sleep graphics.");
        BattleSnapshot migrated = original with { NativeGraphics = graphics };
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(migrated with { NativeGraphics = original.NativeGraphics }));
        GameState migratedState = state with { Battle = migrated };
        bool compact = originalBytes.AsSpan().StartsWith("MFF8SAVE"u8);
        new GameSession(migratedState).Save(output!, compact);
        Assert.Equal(compact, File.ReadAllBytes(output!).AsSpan().StartsWith("MFF8SAVE"u8));
        BattleSnapshot saved = GameSession.Load(output!).Battle!;
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(saved));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleSession first = BattleSession.Restore(saved);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        long beforeCallback = first.NativeGraphics!.Callback;
        first.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        Assert.Equal(beforeCallback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        AssertNoStaleSleep(first.Capture(), sleepFlag);
        byte[] pixels = new BattleScenePresentation(assets, saved.Definition, true).Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, saved.Definition, true).Render(second, 320, 224).ToArray());
        NativeBattleRestorationPresentationTests.Publish(Path.GetDirectoryName(Path.GetFullPath(output!)), Path.GetFileNameWithoutExtension(output!) + "-next-callback", first.Capture(), pixels);
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(File.ReadAllBytes(source!)));
    }

    [Fact]
    public void Named_before_sleep_resources_support_an_in_memory_physical_wake_contract_fixture()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_EXPIRY_BEFORE");
        Assert.SkipUnless(source is not null, "Select the standalone pre-Sleep battle save.");
        byte[] originalBytes = File.ReadAllBytes(source!);
        BattleSnapshot original = GameSession.Load(source!).Battle!;
        BattleCombatantDefinition caster = Assert.Single(original.Definition.Combatants, actor => actor.IsPlayer && actor.ModelId == 3);
        BattleCombatantDefinition target = original.Definition.Combatants.First(actor => !actor.IsPlayer && actor.ModelId != 0);
        BattleCombatantSnapshot originalTarget = original.Combatants.Single(actor => actor.Id == target.Id);
        NativeBattleStatusSnapshot status = originalTarget.NativeStatus!;
        short[] timers = status.Timers.ToArray();
        timers[0] = 30000;
        uint sleeping = status.Battle | 1;
        //Synthetic physical-contact boundary only: inject Sleep on the existing enemy with its original resources.
        //A long timer excludes expiry, so only the queued ordinary physical hit can prove waking.
        BattleCombatantSnapshot sleepingTarget = originalTarget with { NativeStatus = status with { Battle = sleeping, Timers = timers, Presentation = new(status.Saved, sleeping) } };
        BattleSnapshot synthetic = original with { Combatants = original.Combatants.Select(actor => actor.Id == target.Id ? sleepingTarget : actor).ToArray() };
        BattleSession setup = BattleSession.Restore(synthetic);
        NativeBattleGraphicsState graphics = synthetic.NativeGraphics!;
        int targetIndex = graphics.ActorIds.IndexOf(target.Id);
        MethodInfo apply = typeof(BattleSession).GetMethod("ApplyNativeDescriptorStatus502380", BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo address = typeof(BattleSession).GetMethod("NativeParticipantAddress", BindingFlags.NonPublic | BindingFlags.Static)!;
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 117);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)address.Invoke(null, [targetIndex])!);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), checked((short)targetIndex));
        graphics = (NativeBattleGraphicsState)apply.Invoke(setup, [graphics, new NativeBattleDescriptorPacket(ImmutableArray.CreateRange(bytes))])!;
        synthetic = synthetic with { NativeGraphics = graphics };
        uint sleepFlag = original.Definition.NativeGraphics!.Rules.Project(0, 1);
        Assert.NotEqual(0u, graphics.Models[targetIndex].Flags8.GetValueOrDefault() & sleepFlag);
        BattleSession battle = BattleSession.Restore(synthetic);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(synthetic))!);
        battle.QueueAttack(caster.Id, target.Id);
        second.QueueAttack(caster.Id, target.Id);
        bool sawPhysicalWake = false;
        bool completed = false;
        for (int step = 0; step < 300; step++)
        {
            battle.Tick();
            second.Tick();
            BattleSnapshot after = battle.Capture();
            Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(second.Capture()));
            BattleCombatantSnapshot current = after.Combatants.Single(actor => actor.Id == target.Id);
            sawPhysicalWake |= current.CurrentHp < originalTarget.CurrentHp && current.NativeParticipant?.LastAction?.AttackType == 0 && (current.NativeStatus!.Battle & 1) == 0;
            completed = sawPhysicalWake && (after.NativeGraphics!.Models[targetIndex].Flags8.GetValueOrDefault() & sleepFlag) == 0 && after.ActiveAction is null;
            if (completed)
            {
                break;
            }
        }
        Assert.True(sawPhysicalWake, "The ordinary physical hit must wake the synthetically sleeping target before its long timer expires.");
        Assert.True(completed, "The physical contact and graphics completion must clear the Sleep icon.");
        Assert.DoesNotContain(battle.Capture().Events, entry => entry.Frame > original.Frame && entry.Kind == "NativeStatusExpired" && entry.ActorId == target.Id && entry.Amount == 16);
        AssertNoStaleSleep(battle.Capture(), sleepFlag);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        restored.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        Assert.Equal(new BattleScenePresentation(assets, original.Definition, true).Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, original.Definition, true).Render(restored, 320, 224).ToArray());
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(File.ReadAllBytes(source!)));
    }

    [Fact]
    public void Named_snapshot_supplies_an_in_memory_expiring_sleep_contract_fixture()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_EXPIRY_SOURCE");
        Assert.SkipUnless(source is not null, "Select a standalone battle save with stale Sleep graphics.");
        byte[] originalBytes = File.ReadAllBytes(source!);
        BattleSnapshot original = GameSession.Load(source!).Battle!;
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        uint sleepFlag = original.Definition.NativeGraphics!.Rules.Project(0, 1);
        string[] targets = graphics.ActorIds.Where((id, index) => id is not null && (graphics.Models[index].Flags8.GetValueOrDefault() & sleepFlag) != 0).Select(id => id!).ToArray();
        Assert.NotEmpty(targets);
        NativeBattlePartyStatusMirrors mirrors = original.NativePartyStatusMirrors!;
        BattleCombatantSnapshot[] actors = original.Combatants.Select(actor =>
        {
            if (!targets.Contains(actor.Id))
            {
                return actor;
            }
            NativeBattleStatusSnapshot status = actor.NativeStatus!;
            short[] timers = status.Timers.ToArray();
            timers[0] = 0;
            uint battle = status.Battle | 1;
            int index = graphics.ActorIds.IndexOf(actor.Id);
            mirrors = mirrors.Observe(index, status.Saved, battle);
            return actor with { NativeStatus = status with { Battle = battle, Timers = timers, Presentation = new(status.Saved, battle) } };
        }).ToArray();
        //Synthetic timer boundary only: the source's resources and existing icon state remain intact.
        //This in-memory contract fixture is never written as a gameplay save.
        BattleSnapshot synthetic = original with { Combatants = actors, NativePartyStatusMirrors = mirrors };
        BattleSession first = BattleSession.Restore(synthetic);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(synthetic))!);
        bool completed = false;
        for (int callback = 0; callback < 16; callback++)
        {
            first.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            completed = targets.All(id =>
            {
                int index = current.NativeGraphics!.ActorIds.IndexOf(id);
                return (current.Combatants.Single(actor => actor.Id == id).NativeStatus!.Battle & 1) == 0 && (current.NativeGraphics.Models[index].Flags8.GetValueOrDefault() & sleepFlag) == 0;
            });
            if (completed)
            {
                break;
            }
        }
        Assert.True(completed, "The expiry producer and queued opcode117 must clear the original Sleep graphics.");
        BattleSnapshot after = first.Capture();
        foreach (string target in targets)
        {
            Assert.Contains(after.Events, entry => entry.Frame > original.Frame && entry.Kind == "NativeStatusExpired" && entry.ActorId == target && entry.Amount == 16);
            int index = after.NativeGraphics!.ActorIds.IndexOf(target);
            Assert.Equal(graphics.Models[index].Flags8!.Value & 0x1800000, after.NativeGraphics!.Models[index].Flags8!.Value & 0x1800000);
        }
        AssertNoStaleSleep(after, sleepFlag);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(after))!);
        first.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        restored.AdvanceNativeGraphicsPresentation(Callback, TimeSpan.Zero);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(restored.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        Assert.Equal(new BattleScenePresentation(assets, original.Definition, true).Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, original.Definition, true).Render(restored, 320, 224).ToArray());
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(File.ReadAllBytes(source!)));
    }
    private static void AssertNoStaleSleep(BattleSnapshot snapshot, uint sleepFlag)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            BattleCombatantSnapshot? actor = snapshot.Combatants.SingleOrDefault(actor => actor.Id == graphics.ActorIds[index]);
            if (actor?.NativeStatus is { } status && (status.Battle & 1) == 0)
            {
                Assert.Equal(0u, graphics.Models[index].Flags8.GetValueOrDefault() & sleepFlag);
            }
        }
    }
}
