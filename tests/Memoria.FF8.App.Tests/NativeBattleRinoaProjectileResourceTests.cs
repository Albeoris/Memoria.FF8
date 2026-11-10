using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRinoaProjectileResourceTests
{
    [Fact]
    public void Installed_attack_draws_linked_projectile_without_overwriting_body_and_repeats_after_json_restore()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleDefinition definition = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 4, 86);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        bool checkedProjectile = false;
        for (int attack = 0; attack < 2; attack++)
        {
            for (int wait = 0; wait < 400 && !battle.CanChoosePlayerCommand("party"); wait++)
            {
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand("party"));
            int initialHp = battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp;
            battle.QueueAttack("party", "enemy");
            bool root = false;
            bool projectile = false;
            bool sparks = false;
            bool ribbon = false;
            bool completed = false;
            for (int callback = 0; callback < 600; callback++)
            {
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                NativeBattleModuleState modules = graphics.ModuleEffects;
                root |= modules.Slots.Any(task => task.Callback == 0x56F9C0 && (task.Flags & 1) != 0);
                projectile |= modules.Slots.Any(task => task.Callback == 0x56FE10 && (task.Flags & 1) != 0);
                sparks |= modules.Slots.Any(task => task.Callback == 0x56FCD0 && (task.Flags & 1) != 0);
                ribbon |= graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Opcode == 0x3A);
                int flyingSlot = Enumerable.Range(0, modules.Slots.Length).FirstOrDefault(index => modules.Slots[index].Callback == 0x56FE10 && (modules.Slots[index].Flags & 1) != 0 && BinaryPrimitives.ReadInt16LittleEndian(modules.Slots[index].Payload.AsSpan()) == 16, -1);
                if (!checkedProjectile && flyingSlot >= 0)
                {
                    AssertProjectileResources(battle.Capture(), flyingSlot);
                    CompareNext();
                    checkedProjectile = true;
                }
                if (root && modules.Head is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0)
                {
                    Assert.True(projectile);
                    Assert.True(sparks);
                    Assert.True(ribbon);
                    Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp < initialHp);
                    Assert.Equal(0, graphics.Models[0].Flags0 & 0x200);
                    CompareNext();
                    completed = true;
                    break;
                }
            }
            Assert.True(completed);
        }
        Assert.True(checkedProjectile);

        void CompareNext()
        {
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(new BattleScenePresentation(assets, definition, true).Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(restored, 320, 224).ToArray());
        }
    }

    private static void AssertProjectileResources(BattleSnapshot snapshot, int slot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        int actor = graphics.ModuleEffects.RinoaPhysicalHit!.Actor;
        NativeBattleGraphicsModel before = graphics.Models[actor];
        NativeBattleGraphicsState drawn = Draw(snapshot, graphics);
        Assert.Same(before.CurrentBodyBoneMatrices, drawn.Models[actor].CurrentBodyBoneMatrices);
        NativeBattleBodyBoneMatrices linked = drawn.Models[actor].CurrentLinkedBoneMatrices!;
        Assert.Equal(before.CurrentLinkedBoneMatrices!.SkeletonSha256, linked.SkeletonSha256);
        Assert.NotEqual(before.CurrentBodyBoneMatrices!.SkeletonSha256, linked.SkeletonSha256);
        Assert.All(linked.Matrices, matrix => Assert.Equal(linked.Matrices[0], matrix));
        Assert.NotEqual(Ordering(graphics), Ordering(drawn));
        //A projectile callback has no dependency on the primary body's geometry row.
        NativeBattleGraphicsState withoutBodyGeometry = graphics with { Models = graphics.Models.SetItem(actor, before with { ActiveResourceRow100 = null, ActiveStagedResourceRow100 = null }) };
        NativeBattleGraphicsState linkedOnly = Draw(snapshot, withoutBodyGeometry);
        Assert.Equal(Ordering(drawn), Ordering(linkedOnly));

        static string Ordering(NativeBattleGraphicsState state)
        {
            NativeBattleLogicalOrdering pending = state.PrimitiveFrame!.Pending;
            return JsonSerializer.Serialize(new { Table = pending.Table.ToArray(), Proxies = pending.Proxies.ToArray(), Packets = pending.Packets.ToArray() });
        }

        NativeBattleGraphicsState Draw(BattleSnapshot source, NativeBattleGraphicsState input)
        {
            BattleSession isolated = BattleSession.Restore(source);
            input = input with { PrimitiveFrame = isolated.NativeGraphics!.PrimitiveFrame };
            MethodInfo advance = typeof(BattleSession).GetMethod("AdvanceNativeRinoaPhysicalHit", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var result = ((NativeBattleGraphicsState Graphics, NativeBattleModuleCallback Callback))advance.Invoke(isolated, [input, input.ModuleEffects, slot, new NativeBattleModuleInputs(0)])!;
            return result.Graphics;
        }
    }
}
