using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private readonly Dictionary<(BattleModel Source, BattleModel Recipient, BattleModel? ContactWeapon, string Bank, uint? Directory, string Revision), NativeBattleAnimationBankOverride> loadoutAnimationBanks = [];

    private NativeBattleGraphicsResource BindLoadoutAnimationBanks(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor, BattleModel recipient, BattleLimitFamily? selectedFamily = null)
    {
        int style = actor.WeaponProfile?.ResourceOwner ?? (actor.WeaponId is int weapon ? ReadWeaponProfile(weapon).ResourceOwner : actor.ModelId);
        resource = resource with { CombatStyleModelId = checked((byte)style) };
        if (selectedFamily is null && style != actor.ModelId)
        {
            resource = BindLoadoutVictoryBank(resource, style, actor.ModelId, recipient, actor.WeaponId);
        }
        if (selectedFamily is null)
        {
            return resource;
        }
        List<NativeBattleAnimationBankOverride> overrides = resource.AnimationBankOverrides.ToList();
        foreach (int sourceCharacter in new[] { BattleLimitFamilyRules.SourceCharacter(selectedFamily.Value) }.Where(source => source != actor.ModelId))
        {
            BattleModel source = LoadPlayer(sourceCharacter);
            BattleModel? contactWeapon = ReadContactWeapon(sourceCharacter, source, actor.WeaponId);
            NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(sourceCharacter, source, actor.ModelId, recipient);
            foreach (ModelBoundExternalBank binding in ModelExternalBankCatalog.ForCharacter(sourceCharacter, NativeActions).Where(binding => binding.ActorRole != ModelExternalBankActorRole.ContactTarget))
            {
                // Replacements live only on this active performer's resource. Script-selected targets and companions retain their own banks.
                byte[] bank = ModelAssetDiscoveryBindings.ReadExternalBank(GameDataResources.ReadNativeMemory(resources), binding.Descriptor.Address, binding.SectionCount);
                BattleModel external = BattleResourceReader.ReadLinkedAnimationBank(source, bank, false, binding.BodyAnimationCount, binding.LinkedAnimationBoneCount);
                external = modelOverrides?.ApplyBattleBank("battle/" + source.Name.ToLowerInvariant(), binding.Descriptor.Bank, external) ?? external;
                NativeBattlePoseSource motion = external.NativePoseSource ?? throw new InvalidDataException("A transferred family animation bank requires its actual source skeleton.");
                NativeBattlePoseSource? linkedMotion = contactWeapon is not null && binding.SectionCount == 3 ? ReadContactBankMotion(contactWeapon, bank, binding.Descriptor.Bank) : null;
                int originalStart = BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(4));
                int originalEnd = BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(8));
                string fingerprint = Convert.ToHexString(SHA256.HashData(bank.AsSpan(originalStart, originalEnd - originalStart)));
                NativeBattleAnimationBankOverride replacement = CompileLoadoutAnimationBank(sourceCharacter, source, recipient, compiler, motion, binding.Descriptor.Bank, binding.Descriptor.Address, fingerprint, contactWeapon, linkedMotion);
                NativeBattleAnimationBankOverride? existing = overrides.SingleOrDefault(item => !item.Linked && item.Bank == replacement.Bank);
                if (existing is not null)
                {
                    if (existing.SourceFingerprint != replacement.SourceFingerprint || !existing.Animations.AsSpan().SequenceEqual(replacement.Animations.AsSpan()))
                    {
                        throw new NotSupportedException("Assigned families require conflicting bindings for the same native actor animation bank.");
                    }
                    continue;
                }
                overrides.Add(replacement);
            }
        }
        return resource with { AnimationBankOverrides = overrides.ToImmutableArray() };
    }

    private NativeBattleGraphicsResource BindLoadoutVictoryBank(NativeBattleGraphicsResource resource, int style, int character, BattleModel recipient, int? weaponId)
    {
        IGameNativeMemorySource memory = GameDataResources.ReadNativeMemory(resources);
        byte[] mapping = memory.Read(0xB8B7E0, 12);
        int pair = Enumerable.Range(0, 6).FirstOrDefault(index => mapping[index * 2] == style, -1);
        if (pair < 0)
        {
            return resource;
        }
        int section = mapping[pair * 2 + 1];
        byte[] file = Read(Path.GetFileName(ModelAssetDiscoveryBindings.ResolveBattleFile(memory, 766)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(file);
        if (count is < 1 or > 128 || section >= count || file.Length < 8 + count * 4)
        {
            throw new InvalidDataException("The selected victory animation section exceeds its resource directory.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4 + section * 4));
        int end = section + 1 < count ? BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8 + section * 4)) : file.Length;
        if (start < 8 + count * 4 || end <= start || end > file.Length)
        {
            throw new InvalidDataException("The selected victory animation bank exceeds its installed resource.");
        }
        BattleModel source = LoadPlayer(style);
        BattleModel external = BattleResourceReader.ReadLinkedAnimationBank(source, file[start..end], false);
        string bankName = "victory-" + section;
        external = modelOverrides?.ApplyBattleBank("battle/" + source.Name.ToLowerInvariant(), bankName, external) ?? external;
        NativeBattlePoseSource motion = external.NativePoseSource ?? throw new InvalidDataException("A victory bank requires its actual source skeleton.");
        BattleModel? contactWeapon = ReadContactWeapon(style, source, weaponId);
        NativeBattlePoseSource? linkedMotion = contactWeapon is null ? null : ReadContactBankMotion(contactWeapon, file[start..end], bankName);
        NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(style, source, character, recipient);
        int animationStart = start + BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(start + 4));
        int animationEnd = start + BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(start + 8));
        NativeBattleAnimationBankOverride replacement = CompileLoadoutAnimationBank(style, source, recipient, compiler, motion, bankName, null, Convert.ToHexString(SHA256.HashData(file.AsSpan(animationStart, animationEnd - animationStart))), contactWeapon, linkedMotion);
        return resource with { AnimationBankOverrides = resource.AnimationBankOverrides.Add(replacement) };
    }

    private NativeBattleAnimationBankOverride CompileLoadoutAnimationBank(int sourceCharacter, BattleModel source, BattleModel recipient, NativeBattlePoseRetargeter compiler, NativeBattlePoseSource motion, string bank, uint? directory, string revision, BattleModel? contactWeapon = null, NativeBattlePoseSource? linkedMotion = null)
    {
        return WithPlayerModelResources(() => ReadLoadoutAnimationBank(sourceCharacter, source, recipient, compiler, motion, bank, directory, revision, contactWeapon, linkedMotion));
    }

    private NativeBattleAnimationBankOverride ReadLoadoutAnimationBank(int sourceCharacter, BattleModel source, BattleModel recipient, NativeBattlePoseRetargeter compiler, NativeBattlePoseSource motion, string bank, uint? directory, string revision, BattleModel? contactWeapon, NativeBattlePoseSource? linkedMotion)
    {
        motion.Validate();
        if (!motion.Skeleton.AsSpan().SequenceEqual(source.NativePoseSource!.Skeleton.AsSpan()))
        {
            throw new InvalidDataException("A source body bank no longer matches its calibrated actor skeleton.");
        }
        if (linkedMotion is not null)
        {
            linkedMotion.Validate();
            if (contactWeapon?.NativePoseSource is not { } weaponPose || !linkedMotion.Skeleton.AsSpan().SequenceEqual(weaponPose.Skeleton.AsSpan()))
            {
                throw new InvalidDataException("A source contact bank no longer matches its calibrated equipment skeleton.");
            }
        }
        string compiledRevision = revision + ":" + Convert.ToHexString(SHA256.HashData(motion.Animations.AsSpan())) + ":" + (linkedMotion is null ? "" : Convert.ToHexString(SHA256.HashData(linkedMotion.Animations.AsSpan())));
        var key = (source, recipient, contactWeapon, bank, directory, compiledRevision);
        if (!loadoutAnimationBanks.TryGetValue(key, out NativeBattleAnimationBankOverride? compiled))
        {
            var contacts = contactWeapon is null ? default : BattleWeaponContactFrames.Read(sourceCharacter, source, contactWeapon, motion, linkedMotion);
            NativeBattlePoseSource pose = compiler.Compile(motion, contacts);
            compiled = new(bank, directory, false, revision, pose.Animations) { ActionBoneAliases = pose.ActionBoneAliases, ActionBoneFrames = pose.ActionBoneFrames, RetargetBinding = pose.RetargetBinding };
            loadoutAnimationBanks.Add(key, compiled);
        }
        return compiled;
    }

    private NativeBattlePoseSource ReadContactBankMotion(BattleModel weapon, byte[] bank, string bankName)
    {
        BattleModel external = BattleResourceReader.ReadLinkedAnimationBank(weapon, bank, true);
        external = modelOverrides?.ApplyBattleBank("battle/" + weapon.Name.ToLowerInvariant(), bankName, external) ?? external;
        return external.NativePoseSource ?? throw new InvalidDataException("A source firearm contact bank requires its linked motion.");
    }
}
