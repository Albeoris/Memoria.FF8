using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private readonly Dictionary<(NativeBattlePoseSource Source, NativeBattlePoseSource Recipient, string Bank, string Revision), NativeBattleAnimationBankOverride> loadoutAnimationBanks = [];

    private NativeBattleGraphicsResource BindLoadoutAnimationBanks(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor, BattleModel recipient, BattleLimitFamily? selectedFamily = null)
    {
        int style = actor.WeaponProfile?.ResourceOwner ?? (actor.WeaponId is int weapon ? ReadWeaponProfile(weapon).ResourceOwner : actor.ModelId);
        resource = resource with { CombatStyleModelId = checked((byte)style) };
        if (selectedFamily is null && style != actor.ModelId)
        {
            resource = BindLoadoutVictoryBank(resource, style, actor.ModelId, recipient);
        }
        if (selectedFamily is null)
        {
            return resource;
        }
        List<NativeBattleAnimationBankOverride> overrides = resource.AnimationBankOverrides.ToList();
        foreach (int sourceCharacter in new[] { BattleLimitFamilyRules.SourceCharacter(selectedFamily.Value) }.Where(source => source != actor.ModelId))
        {
            BattleModel source = LoadPlayer(sourceCharacter);
            NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(sourceCharacter, source, actor.ModelId, recipient);
            foreach (ModelBoundExternalBank binding in ModelExternalBankCatalog.ForCharacter(sourceCharacter, NativeActions).Where(binding => binding.ActorRole != ModelExternalBankActorRole.ContactTarget))
            {
                // Replacements live only on this active performer's resource. Script-selected targets and companions retain their own banks.
                byte[] bank = ModelAssetDiscoveryBindings.ReadExternalBank(GameDataResources.ReadNativeMemory(resources), binding.Descriptor.Address, binding.SectionCount);
                BattleModel external = BattleResourceReader.ReadLinkedAnimationBank(source, bank, false);
                NativeBattlePoseSource motion = external.NativePoseSource ?? throw new InvalidDataException("A transferred family animation bank requires its actual source skeleton.");
                string fingerprint = Convert.ToHexString(SHA256.HashData(motion.Animations.AsSpan()));
                NativeBattleAnimationBankOverride replacement = CompileLoadoutAnimationBank(source, recipient, compiler, motion, binding.Descriptor.Bank, binding.Descriptor.Address, fingerprint);
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

    private NativeBattleGraphicsResource BindLoadoutVictoryBank(NativeBattleGraphicsResource resource, int style, int character, BattleModel recipient)
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
        NativeBattlePoseSource motion = external.NativePoseSource ?? throw new InvalidDataException("A victory bank requires its actual source skeleton.");
        NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(style, source, character, recipient);
        NativeBattleAnimationBankOverride replacement = CompileLoadoutAnimationBank(source, recipient, compiler, motion, "victory-" + section, null, Convert.ToHexString(SHA256.HashData(motion.Animations.AsSpan())));
        return resource with { AnimationBankOverrides = resource.AnimationBankOverrides.Add(replacement) };
    }

    private NativeBattleAnimationBankOverride CompileLoadoutAnimationBank(BattleModel source, BattleModel recipient, NativeBattlePoseRetargeter compiler, NativeBattlePoseSource motion, string bank, uint? directory, string revision)
    {
        var key = (source.NativePoseSource!, recipient.NativePoseSource!, bank, revision);
        if (!loadoutAnimationBanks.TryGetValue(key, out NativeBattleAnimationBankOverride? compiled))
        {
            NativeBattlePoseSource pose = compiler.Compile(motion);
            compiled = new(bank, directory, false, revision, pose.Animations) { ActionBoneAliases = pose.ActionBoneAliases, RetargetBinding = pose.RetargetBinding };
            loadoutAnimationBanks.Add(key, compiled);
        }
        return compiled;
    }
}
