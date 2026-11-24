using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDemiMalboroTests
{
    [Fact]
    public void InstalledDemiDrawsMalboroAtTheProjectedModelDepth()
    {
        const ushort effect = 107;
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition demi = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 13);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(48);
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 376, demi, additionalEnemies: 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-10");
        BattleCombatant malboro = battle.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == 48);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(malboro.Id);
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: malboro.Id, Ability: BattleAbility.Magic, ActionId: demi.Id));

        int deformedFrames = 0;
        for (int tick = 0; tick < 480; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
            NativeBattlePreparedPresentation frame = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
            if (process.Graphics.OriginalEffect?.Binding.Effect == effect && (model.Flags0 & 4) != 0)
            {
                deformedFrames++;
                NativeBattleCameraProjection projection = Assert.IsType<NativeBattleCameraProjection>(frame.PilotProjection);
                IReadOnlyList<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ModelGroupMatrices50F190(model, resource.PoseSource!, projection.View!);
                int maxDepth = resource.Geometry!.Value.SelectMany(mesh => mesh.Vertices)
                    .Max(vertex => NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection).ReadDepth45E250());
                NativeBattleLogicalCommandReference[] body = ReadBody(frame);
                Assert.NotEmpty(body);
                int deepestPacket = body.Max(command => command.VertexPayload4.Max());
                Assert.InRange(deepestPacket, 1, maxDepth);
                if (deformedFrames == 1)
                {
                    BattleRenderer renderer = new(320, 216);
                    HashSet<NativeBattlePrimitiveReference> bodyPackets = body.Select(command => command.Packet).ToHashSet();
                    NativeBattlePreparedPrimitiveFrame withoutBody = frame.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(frame.Primitives.LastCommands.Where(command => !bodyPackets.Contains(command.Command.Packet))) };
                    byte[] scene = Render(frame.Primitives);
                    Assert.False(scene.AsSpan().SequenceEqual(Render(withoutBody)), "The deformed model contributed no pixels.");

                    byte[] Render(NativeBattlePreparedPrimitiveFrame primitives)
                    {
                        return renderer.RenderNativePrimitiveFrame(primitives, frame.Textures, capabilities: frame.PrimitiveCapabilities, textureWindowCache: frame.TextureWindows).ToArray();
                    }
                }
            }
            else if (deformedFrames > 0 && battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id))
            {
                Assert.Contains(frame.Primitives.LastCommands, command => command.Command.PresentationActorId == malboro.Id);
                break;
            }
        }

        Assert.Equal(60, deformedFrames);
        Assert.Contains(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);

        NativeBattleLogicalCommandReference[] ReadBody(NativeBattlePreparedPresentation frame)
        {
            return frame.Primitives.LastCommands.Select(resolved => resolved.Command)
                .Where(command => command.Pilot.Identity.Effect == effect && command.ModelTextureOwner?.RequireActorId() == malboro.Id)
                .ToArray();
        }
    }
}
