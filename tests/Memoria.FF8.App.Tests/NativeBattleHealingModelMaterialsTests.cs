using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleHealingModelMaterialsTests
{
    [Theory]
    [InlineData(1, BattleAbility.Magic)]
    [InlineData(25, BattleAbility.Magic)]
    [InlineData(50, BattleAbility.Item)]
    [InlineData(51, BattleAbility.Item)]
    public void OriginalHealingEffectDrawsSquallWithHisOwnTexturesDuringTheHandoff(ushort effectId, BattleAbility ability)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition action = assets.ActionDefinitions
            .Where(action => action.Ability == ability && action.NativeAction is { } native && ScenarioDiscovery.ResolveCatalogEffect(native, assets.NativeActions) == effectId)
            .OrderBy(action => action.Id)
            .First();
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 87, action, additionalEnemies: 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);

        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-10");
        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == "party-0");
        BattleModelOwner owner = battle.ReadPresentationModelOwner(target.Id);
        target.CurrentHp = 1;
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: ability, ActionId: action.Id));

        bool sawHandoff = false;
        bool comparedContinuation = false;
        bool restoredOrdinaryBody = false;
        for (int tick = 0; tick < 480; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
            if (graphics.OriginalEffect is not { } effect || effect.Binding.Effect != effectId)
            {
                continue;
            }

            NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
            if ((graphics.ActorModels.Get(owner).Flags0 & 4) != 0)
            {
                NativeBattleLogicalResolvedCommand[] body = ReadBody(prepared);
                Assert.NotEmpty(body);
                Assert.All(body.Select(command => command.Command.ModelTextureOwner?.RequireActorId()), material => Assert.Equal(target.Id, material));
                Assert.All(prepared.Primitives.LastCommands.Where(command => command.Command.PilotGeometry?.Identity.Effect == effectId && (command.Command.PilotGeometry.Sprite is not null || (command.Command.Opcode & 0xFC) is 0x34 or 0x3C)), command => Assert.Null(command.Command.ModelTextureOwner));
                if (!sawHandoff)
                {
                    sawHandoff = true;
                    HashSet<NativeBattlePrimitiveReference> packets = body.Select(command => command.Command.Packet).ToHashSet();
                    BattleRenderer renderer = new(320, 216);
                    byte[] scene = Render(prepared.Primitives);
                    byte[] withoutBody = Render(prepared.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(prepared.Primitives.LastCommands.Where(command => !packets.Contains(command.Command.Packet))) });
                    Assert.False(scene.AsSpan().SequenceEqual(withoutBody), "The replacement body contributed no pixels.");

                    byte[] Render(NativeBattlePreparedPrimitiveFrame frame)
                    {
                        return renderer.RenderNativePrimitiveFrame(frame, prepared.Textures, capabilities: prepared.PrimitiveCapabilities, textureWindowCache: prepared.TextureWindows).ToArray();
                    }
                }
                else if (ability == BattleAbility.Item && !comparedContinuation)
                {
                    BattleSession fork = battle.ForkForContinuation();
                    fork.Tick();
                    battle.Tick();
                    Assert.Equal(ReadBody(battle.PreparedPresentation!).Select(command => command.Command.Packet), ReadBody(fork.PreparedPresentation!).Select(command => command.Command.Packet));
                    Assert.Equal(ReadBody(battle.PreparedPresentation!).Select(command => command.Command.Words.ToArray()), ReadBody(fork.PreparedPresentation!).Select(command => command.Command.Words.ToArray()));
                    Assert.Equal(ReadBody(battle.PreparedPresentation!).Select(command => command.Command.ModelTextureOwner), ReadBody(fork.PreparedPresentation!).Select(command => command.Command.ModelTextureOwner));
                    comparedContinuation = true;
                }
            }
            else if (sawHandoff && !restoredOrdinaryBody)
            {
                Assert.Contains(prepared.Primitives.LastCommands, command => command.Command.PresentationActorId == target.Id && command.Command.ModelTextureOwner?.RequireActorId() == target.Id);
                restoredOrdinaryBody = true;
            }

            if (effect.ReturnedDescriptor == 0 && sawHandoff && !caster.IsBusy)
            {
                break;
            }
        }

        Assert.True(sawHandoff);
        if (ability == BattleAbility.Item)
        {
            Assert.True(comparedContinuation);
        }
        Assert.True(restoredOrdinaryBody);
        Assert.True(target.CurrentHp > 1);
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);

        NativeBattleLogicalResolvedCommand[] ReadBody(NativeBattlePreparedPresentation prepared)
        {
            return prepared.Primitives.LastCommands.Where(command => command.Pass == 1 && command.Command.PilotGeometry is { Sprite: null } geometry && geometry.Identity.Effect == effectId && (command.Command.Opcode & 0xFC) is 0x24 or 0x2C).ToArray();
        }
    }
}
