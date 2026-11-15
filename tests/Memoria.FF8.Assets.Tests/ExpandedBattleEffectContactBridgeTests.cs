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
        BattleModelOwner source = new BattlePartyActorModel(new("source"));
        NativeBattleGraphicsModel caster = NativeBattleGraphicsModels.CompleteLoad(new(0)
        {
            Transform = new(origin, origin, 0, 0, 4096),
            LinkedTransform = new(default, default),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, 0, 0, true, ref global);
        caster = NativeBattleSequenceMachine.Advance(source, NativeBattleGraphicsModels.SelectSequence(caster, 1), resource, new(0, 0)).Model;
        caster = NativeBattlePoseMatrices.PublishShadowBounds5086F0(caster, resource.PoseSource!);
        caster = caster with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(caster, resource.PoseSource!) };

        int[] positions = count == 1 ? [5] : [1, 3, 4, 5];
        Dictionary<BattleRosterSlot, int> health = Enumerable.Range(0, 6).ToDictionary(BattleRosterSlot.Enemy, _ => 1000);
        BattleRosterSlot[] recipients = positions.Select(BattleRosterSlot.Enemy).ToArray();
        var targets = recipients.Select(slot =>
        {
            var owner = new BattleForeignEnemyModel(new($"enemy-slot-{slot.Position}"));
            return (Slot: slot, Contact: new NativeBattleContactHalf(owner, 4, 0, 0, 0, checked((ushort)(70 + slot.Position)), 0));
        }).ToArray();
        BattleModelRoster roster = new([source, .. targets.Select(target => target.Contact.Recipient)]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), source), .. targets.Select((target, index) => (NativeBattleModelSlot.Decode(checked((byte)(index + 3))), target.Contact.Recipient))]);
        NativeBattleEffectContactBridge<BattleRosterSlot> bridge = new(targets, roster);
        NativeBattleActionPacketsState initial = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (NativeBattleActionPacketsState packets, BattleModelTargets selected) = bridge.Prepare(initial, action.Command, 1, action.ActionId, action.MagicId, source);
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(count, packets.ContactCount);
        NativeBattleGraphicsResource enemyResource = assets.ReadNativeGraphicsResource(new() { ModelId = 28 });
        ImmutableArray<NativeBattleForeignActor> actors = targets.Select(target =>
        {
            NativeBattleVector position = ExpandedBattleFormation.Position(target.Slot.Position, health.Count, isAlly: false);
            BattleModelOwner owner = target.Contact.Recipient;
            NativeGlobalRandomState random = new(0);
            NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0)
            {
                Transform = new(position, position, 0, 2048, 4096),
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
            }, 44, 0, false, ref random, assets.ReadEnemyNativeInformation246(28));
            model = NativeBattleSequenceMachine.Advance(owner, NativeBattleGraphicsModels.SelectSequence(model, 0), enemyResource, new(0, 0)
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
            return new NativeBattleForeignActor(owner, posed.Transform!, NativeBattlePoseMatrices.ReadAnchor(posed, poseSource), (selector, fraction) => NativeBattlePoseMatrices.ReadBone(posed, poseSource, selector, fraction))
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
        NativeBattleEffectParameters parameters = new(source, 0, selected, new BattleActionHeaderReference(0), 0, 0);
        NativeBattleEffectInvocation invocation = new NativeBattleEffectInvocation.Combat(packets, parameters);
        using NativeBattleEffectPresentation effect = new(new(resources, caster, resource, invocation, action.MagicId, NativeBattleEffectTextureState.FromColdProcess(), NativeCrtRandomState.FromColdProcess(), projection, actors));
        Assert.Equal(action.MagicId, effect.Graphics.OriginalEffect!.Binding.Effect);
        foreach (NativeBattleForeignActor actor in actors)
        {
            Assert.Equal(actor.Transform.Position28, effect.Graphics.ActorModels.Get(actor.Owner).Transform!.Position28);
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
