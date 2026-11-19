using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private readonly Dictionary<(string SourceName, string RecipientName, NativeBattlePoseSource Source, NativeBattlePoseSource Recipient), NativeBattlePoseSource> limitGunbladeMotion = [];
    private readonly Dictionary<(string SourceName, string RecipientName, NativeBattlePoseSource Source, NativeBattlePoseSource Recipient, string Bank, uint Directory, string Revision), NativeBattleAnimationBankOverride> limitGunbladeBanks = [];

    private NativeBattleGraphicsResource BindLimitGunbladeMotion(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor, BattleLimitFamily family, int sourceCharacter, int equippedStyle)
    {
        if (BattleLimitFamilyRules.RequiredWeapon(family) != BattleWeaponCapabilities.Gunblade || sourceCharacter == equippedStyle || equippedStyle is not (0 or 6))
        {
            return resource;
        }
        int sourceWeaponId = WeaponDefinitions.First(weapon => weapon.CharacterId == sourceCharacter).Id;
        BattleModel sourceBody = LoadPlayer(sourceCharacter);
        BattleModel source = ResolvePlayerRenderWeapon(sourceCharacter, sourceWeaponId, sourceBody) ?? throw new InvalidDataException("The selected gunblade family requires its original independent weapon rig.");
        int weaponId = actor.WeaponId ?? throw new InvalidDataException("A transferred gunblade family requires its equipped weapon record.");
        BattleModel recipient = ResolvePlayerRenderWeapon(actor.ModelId, weaponId, LoadPlayer(actor.ModelId, actor.Costume)) ?? throw new InvalidDataException("The equipped gunblade requires its independent weapon rig.");
        NativeBattlePoseRetargeter compiler = BattleLinkedWeaponRigProfiles.CreateGunbladeCompiler(source, recipient);
        NativeBattlePoseSource sourcePose = source.NativePoseSource!;
        NativeBattlePoseSource recipientPose = recipient.NativePoseSource!;
        foreach (var obsolete in limitGunbladeMotion.Keys.Where(key => key.SourceName == source.Name && key.RecipientName == recipient.Name && (!ReferenceEquals(key.Source, sourcePose) || !ReferenceEquals(key.Recipient, recipientPose))).ToArray())
        {
            limitGunbladeMotion.Remove(obsolete);
        }
        foreach (var obsolete in limitGunbladeBanks.Keys.Where(key => key.SourceName == source.Name && key.RecipientName == recipient.Name && (!ReferenceEquals(key.Source, sourcePose) || !ReferenceEquals(key.Recipient, recipientPose))).ToArray())
        {
            limitGunbladeBanks.Remove(obsolete);
        }
        var motionKey = (source.Name, recipient.Name, sourcePose, recipientPose);
        if (!limitGunbladeMotion.TryGetValue(motionKey, out NativeBattlePoseSource? motion))
        {
            motion = compiler.Compile(sourcePose);
            limitGunbladeMotion.Add(motionKey, motion);
        }
        ImmutableArray<NativeBattleAnimationBankOverride>.Builder banks = resource.AnimationBankOverrides.ToBuilder();
        foreach (ModelBoundExternalBank binding in ModelExternalBankCatalog.ForCharacter(sourceCharacter, NativeActions).Where(binding => binding.SectionCount == 3 && binding.ActorRole != ModelExternalBankActorRole.ContactTarget))
        {
            byte[] bank = ModelAssetDiscoveryBindings.ReadExternalBank(GameDataResources.ReadNativeMemory(resources), binding.Descriptor.Address, binding.SectionCount);
            BattleModel external = BattleResourceReader.ReadLinkedAnimationBank(source, bank, true);
            NativeBattlePoseSource original = external.NativePoseSource!;
            string fingerprint = Convert.ToHexString(SHA256.HashData(original.Animations.AsSpan()));
            external = modelOverrides?.ApplyBattleBank("battle/" + source.Name.ToLowerInvariant(), binding.Descriptor.Bank, external) ?? external;
            NativeBattlePoseSource changed = external.NativePoseSource ?? throw new InvalidDataException("A source gunblade bank override requires native weapon motion.");
            changed.Validate();
            if (!changed.Skeleton.AsSpan().SequenceEqual(sourcePose.Skeleton.AsSpan()))
            {
                throw new InvalidDataException("A source gunblade bank no longer matches its calibrated weapon skeleton.");
            }
            string revision = fingerprint + ":" + Convert.ToHexString(SHA256.HashData(changed.Animations.AsSpan()));
            var key = (source.Name, recipient.Name, sourcePose, recipientPose, binding.Descriptor.Bank, binding.Descriptor.Address, revision);
            if (!limitGunbladeBanks.TryGetValue(key, out NativeBattleAnimationBankOverride? replacement))
            {
                NativeBattlePoseSource compiled = compiler.Compile(changed);
                replacement = new(binding.Descriptor.Bank, binding.Descriptor.Address, true, fingerprint, compiled.Animations) { ActionBoneAliases = compiled.ActionBoneAliases, RetargetBinding = compiled.RetargetBinding };
                limitGunbladeBanks.Add(key, replacement);
            }
            banks.Add(replacement);
        }
        return resource with { LinkedPoseSource = motion, LinkedAnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(motion.Animations.AsSpan()), AnimationBankOverrides = banks.ToImmutable() };
    }
}
