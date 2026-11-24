using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFlareChimeraTests
{
    [Fact]
    public void InstalledFlareDrawsChimeraAtTheProjectedModelDepth()
    {
        const ushort effect = 124;
        const byte chimeraModel = 47;
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition flare = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 15);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(chimeraModel);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 572, flare, additionalEnemies: 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-10");
        BattleCombatant chimera = battle.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == chimeraModel);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(chimera.Id);
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: chimera.Id, Ability: BattleAbility.Magic, ActionId: flare.Id));

        int rippleFrames = 0;
        bool normalBodyRestored = false;
        for (int tick = 0; tick < 480; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
            NativeBattlePreparedPresentation frame = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
            if (process.Graphics.OriginalEffect?.Binding.Effect == effect && (model.Flags0 & 4) != 0)
            {
                rippleFrames++;
                NativeBattleCameraProjection projection = Assert.IsType<NativeBattleCameraProjection>(frame.PilotProjection);
                NativeBattleGraphicsModel posed = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix!);
                NativeBattleActionMatrix[] bones = NativeBattleBodyBoneMatrices.Require(posed, resource.PoseSource!)
                    .Select(matrix => NativeBattleGfMatrices.Compose56C2F0(projection.View!, matrix)).ToArray();
                int maxDepth = resource.Geometry!.Value.SelectMany(mesh => mesh.Vertices)
                    .Max(vertex => NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection).ReadDepth45E250());
                NativeBattleLogicalCommandReference[] body = frame.Primitives.LastCommands.Select(command => command.Command)
                    .Where(command => command.Pilot.Identity.Bank == 0x6BCAD0 && command.ModelTextureOwner?.RequireActorId() == chimera.Id)
                    .ToArray();
                Assert.NotEmpty(body);
                Assert.InRange(body.Max(command => command.VertexPayload4.Max()), 1, maxDepth);
                if (rippleFrames == 1)
                {
                    BattleRenderer renderer = new(320, 216);
                    HashSet<NativeBattlePrimitiveReference> bodyPackets = body.Select(command => command.Packet).ToHashSet();
                    NativeBattlePreparedPrimitiveFrame withoutBody = frame.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(frame.Primitives.LastCommands.Where(command => !bodyPackets.Contains(command.Command.Packet))) };
                    Assert.False(Render(frame.Primitives).AsSpan().SequenceEqual(Render(withoutBody)), "The rippling Chimera body contributed no pixels.");

                    byte[] Render(NativeBattlePreparedPrimitiveFrame primitives)
                    {
                        return renderer.RenderNativePrimitiveFrame(primitives, frame.Textures, capabilities: frame.PrimitiveCapabilities, textureWindowCache: frame.TextureWindows).ToArray();
                    }
                }
            }
            else if (rippleFrames > 0 && battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id))
            {
                normalBodyRestored = frame.Primitives.LastCommands.Any(command => command.Command.PresentationActorId == chimera.Id);
                break;
            }
        }

        Assert.Equal(40, rippleFrames);
        Assert.True(normalBodyRestored);
    }
}
