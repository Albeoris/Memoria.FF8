using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattleEffectContactBridgeTests
{
    [Theory]
    [InlineData("Fire", 1)]
    [InlineData("Quake", 4)]
    public void OneOriginalEffectPublishesThePreparedContactsToExtendedEnemySlots(string spell, int count)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, candidate => candidate.Command == 2 && candidate.Name == spell);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = 0 });
        NativeGlobalRandomState global = new(0);
        NativeBattleVector origin = new(0, 0, 1800);
        NativeBattleGraphicsModel caster = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
        {
            Transform = new(origin, origin, 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, ref global);
        caster = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(caster, 1), resource, new(0, 0)).Model;
        caster = NativeBattlePoseMatrices.PublishShadowBounds5086F0(caster, resource.PoseSource!);
        caster = caster with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(caster, resource.PoseSource!) };

        int[] positions = count == 1 ? [5] : [1, 3, 4, 5];
        Dictionary<BattleRosterSlot, int> health = Enumerable.Range(0, 6).ToDictionary(BattleRosterSlot.Enemy, _ => 1000);
        BattleRosterSlot[] recipients = positions.Select(BattleRosterSlot.Enemy).ToArray();
        var targets = recipients.Select((slot, index) => (Slot: slot, Contact: new NativeBattleContactHalf(checked((byte)(index + 3)), 4, 0, 0, 0, checked((ushort)(70 + slot.Position)), 0))).ToArray();
        NativeBattleEffectContactBridge<BattleRosterSlot> bridge = new(targets);
        (NativeBattleActionPacketsState packets, ushort mask) = bridge.Prepare(action.Command, 1, action.ActionId, action.MagicId);
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(count, packets.ContactCount);
        NativeBattleGraphicsResource enemyResource = assets.ReadNativeGraphicsResource(new() { ModelId = 28 });
        ImmutableArray<NativeBattleForeignActor> actors = targets.Select(target =>
        {
            NativeBattleVector position = ExpandedBattleFormation.Position(target.Slot.Position, health.Count, isAlly: false);
            int index = target.Contact.ActorIndex;
            NativeGlobalRandomState random = new(0);
            NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(index, 0)
            {
                Transform = new(position, position, 0, 2048, 4096),
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
            }, 44, 0, ref random, assets.ReadEnemyNativeInformation246(28));
            model = NativeBattleSequenceMachine.Advance(NativeBattleGraphicsModels.SelectSequence(model, 0), enemyResource, new(0, 0)
            {
                GlobalRandom = () =>
                {
                    random = random.Draw(out ushort value);
                    return value;
                }
            }).Model;
            NativeBattlePoseSource poseSource = enemyResource.PoseSource!;
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, poseSource) };
            NativeBattleGraphicsModel posed = model;
            NativeBattleEffectActorGeometry anchors = new(
                NativeBattlePoseMatrices.ReadBone(posed, poseSource, 240, 0),
                NativeBattlePoseMatrices.ReadBone(posed, poseSource, 241, 0),
                position.Y,
                NativeBattlePoseMatrices.LocalBounds8DC870(poseSource, poseSource.Sample(posed.SequenceState!.Pose!.Value, posed.SequenceState.PoseFrame114!.Value, posed.SequenceState.PoseFlags109)));
            return new NativeBattleForeignActor(index, posed.Transform!, NativeBattlePoseMatrices.ReadAnchor(posed, poseSource), (selector, fraction) => NativeBattlePoseMatrices.ReadBone(posed, poseSource, selector, fraction))
            {
                Facing = 2048,
                Geometry = new(posed.GeometryWorldMatrix!, anchors, [])
            };
        }).ToImmutableArray();
        NativeBattleGraphicsDefinition graphics = new(game.ReadNativeBattleGraphicsRules(), [new("source", 0, resource)], [])
        {
            PrimitiveTables = assets.ReadNativePrimitiveTables(),
            SoftwarePrimitiveTable = assets.ReadNativeSoftwarePrimitiveTable(),
            PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8()
        };
        BattleDefinition resources = new()
        {
            NativeGraphics = graphics,
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeGlobalRandom = global,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory()
        };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 7000, 4096), 216, 160, 108);
        NativeBattleEffectPresentation effect = new(new(resources, caster, resource, packets, new(0, 0, mask, 0, 0, 0), action.MagicId, NativeBattleEffectTextureState.FromColdProcess(), NativeCrtRandomState.FromColdProcess(), projection, actors));
        Assert.Equal(action.MagicId, effect.Graphics.OriginalEffect!.Binding.Effect);
        foreach (NativeBattleForeignActor actor in actors)
        {
            Assert.Equal(actor.Transform.Position28, effect.Graphics.Models[actor.Index].Transform!.Position28);
        }

        List<BattleRosterSlot> hits = [];
        bool drewEffect = false;
        HashSet<uint> sounds = [];
        for (int callback = 0; callback < 4096 && !effect.Complete; callback++)
        {
            NativeBattleEffectPresentationStep step = effect.Advance(effect.Source, effect.Random, projection, actors, effect.Memory.Textures!);
            drewEffect |= !effect.Graphics.ModuleDraws.IsEmpty || effect.Graphics.PrimitiveFrame?.Pending.Proxies.Count > 0;
            sounds.UnionWith(step.Sounds.Select(sound => sound.Sound));
            foreach (NativeBattleContactPacket contact in step.Contacts)
            {
                BattleRosterSlot recipient = bridge.Resolve(contact);
                Assert.DoesNotContain(recipient, hits);
                hits.Add(recipient);
                health[recipient] -= contact.Primary.PresentedAmount;
            }
        }
        Assert.True(effect.Complete);
        if (count == 1)
        {
            Assert.True(drewEffect);
        }
        else
        {
            NativeBattleQuake38Resources quake = effect.Graphics.OriginalEffect!.Binding.Quake38Resources!;
            Assert.Contains(quake.RumbleSound, sounds);
            Assert.Contains(quake.QuakeSound, sounds);
        }
        Assert.Equal(recipients, hits);
        foreach ((BattleRosterSlot slot, int hp) in health)
        {
            Assert.Equal(positions.Contains(slot.Position) ? 930 - slot.Position : 1000, hp);
        }
    }
}
