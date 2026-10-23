using System.Buffers.Binary;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private IEnumerable<ModelAssetDiscoveryItem> DiscoverPartyLinks(ModelCatalogEntry selected, int character, bool selectedWeapon, CancellationToken cancellationToken)
    {
        List<(ModelCatalogEntry Entry, BattleModel Model)> weapons = [];
        IGameNativeMemorySource? memory = null;
        BattleModel? body = null;
        ModelCatalogEntry bodyEntry = selected;
        string? error = null;
        try
        {
            memory = ReadNativeMemory();
            if (selectedWeapon)
            {
                string bodyPath = ModelAssetDiscoveryBindings.ResolvePartyFile(memory, character, false, 0);
                bodyEntry = new(bodyPath, Path.GetFileName(bodyPath), "Battle model", bodyPath, bodyPath);
            }
            body = ReadDiscoveryBody(bodyEntry.ResourcePath);
            if (!selectedWeapon)
            {
                bool matched = false;
                for (int costume = 0; costume < 32; costume++)
                {
                    try
                    {
                        if (ModelAssetDiscoveryBindings.ResolvePartyFile(memory, character, false, costume).Equals(selected.ResourcePath, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                            break;
                        }
                    }
                    catch (Exception exception) when (CatalogError(exception))
                    {
                        break;
                    }
                }
                if (!matched)
                {
                    throw new NotSupportedException("Selected body does not resolve through its native costume table.");
                }
            }
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (body is null || memory is null || error is not null)
        {
            yield return DiscoveryDiagnostic(selected, "party-binding", error ?? "The native party binding is unavailable.");
            yield break;
        }
        BattleModel bodyModel = body;
        if (selectedWeapon)
        {
            EditableModel context = ModelExchange.FromBattle(bodyModel);
            yield return new(bodyEntry.Id, "Owner context: " + bodyEntry.Name, ModelAssetDiscoveryKind.Object, bodyEntry.Id, bodyEntry.ResourcePath, "embedded", -1, "native body selector 0 (optional owner context)", () => context);

        }
        BattleWeaponDefinition[] definitions = [];
        IReadOnlyList<BattleNativeAction> nativeActions = [];
        try
        {
            byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
            definitions = BattleKernelReader.Weapons(kernel).Where(definition => definition.CharacterId == character && character != 7).ToArray();
            nativeActions = BattleActionKernelReader.ReadCatalogue(kernel).NativeActions;
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (error is not null)
        {
            yield return DiscoveryDiagnostic(selected, "weapon-kernel", error);
            yield break;
        }
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (BattleWeaponDefinition definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModelCatalogEntry? weaponEntry = null;
            BattleModel? weaponModel = null;
            error = null;
            try
            {
                string path = ModelAssetDiscoveryBindings.ResolvePartyFile(memory, character, true, definition.Tier);
                if (!seen.Add(path))
                {
                    continue;
                }
                byte[] content = resources.Read(new ResourceKey(path));
                if (content.Length == 0)
                {
                    continue;
                }
                if (selectedWeapon && !path.Equals(selected.ResourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                weaponEntry = new(path, definition.Name + " · " + Path.GetFileName(path), "Battle weapon", path, definition.Name);
                weaponModel = resources.GetSource<IBattleModelSource>()?.ReadWeapon(new ResourceKey(path), content, bodyModel) ?? BattleResourceReader.ReadWeapon(Path.GetFileName(path), content, bodyModel);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                error = exception.Message;
            }
            if (weaponModel is null || weaponEntry is null)
            {
                yield return DiscoveryDiagnostic(selected, "weapon-" + definition.Id, error ?? "Weapon resource unavailable.");
                continue;
            }
            weapons.Add((weaponEntry, weaponModel));
            bool reduced = weaponModel.NativeWeaponUsesBodySkeleton == true;
            EditableModel editable = ModelExchange.FromBattle(weaponModel);
            if (reduced)
            {
                editable = editable with { Animations = [] };
            }
            EditableModel captured = editable;
            string provenance = "kernel owner/tier through native linked file selector";
            yield return new(weaponEntry.Id, weaponEntry.Name, ModelAssetDiscoveryKind.Object, bodyEntry.Id, weaponEntry.ResourcePath, "embedded", -1, provenance, () => captured)
            {
                UsesOwnerSkeleton = reduced,
                RigOwnerId = reduced ? bodyEntry.Id : weaponEntry.Id,
                ReplacementPart = reduced ? "mesh/1" : null,
                AlternativeGroup = bodyEntry.Id + "/weapons"
            };
            if (!reduced)
            {
                foreach (ModelAssetDiscoveryItem clip in DiscoverEditable(weaponEntry, captured, "embedded", "paired independent weapon bank", () => captured, false, bodyEntry.Id))
                {
                    yield return clip;
                }
            }
        }
        if (selectedWeapon && weapons.Count == 0)
        {
            yield return DiscoveryDiagnostic(selected, "weapon-owner", "The selected weapon has no kernel/native selector owner binding.");
        }
        if (selectedWeapon && weapons.Count > 0)
        {
            EditableModel context = ModelExchange.FromBattle(bodyModel);
            foreach (ModelAssetDiscoveryItem clip in DiscoverEditable(bodyEntry, context, "embedded", "native weapon owner context; body skeletal bank", () => context, false))
            {
                yield return clip with { RigOwnerId = bodyEntry.Id };
            }
        }
        foreach (ModelBoundExternalBank binding in ModelExternalBankCatalog.ForCharacter(character, nativeActions))
        {
            ModelExternalBankDescriptor descriptor = binding.Descriptor;
            cancellationToken.ThrowIfCancellationRequested();
            if (binding.UnsupportedReason is string reason)
            {
                yield return DiscoveryDiagnostic(selected, descriptor.Bank, reason);
                continue;
            }
            byte[]? bank = null;
            error = null;
            try
            {
                bank = ModelAssetDiscoveryBindings.ReadExternalBank(memory, descriptor.Address, binding.SectionCount);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                error = exception.Message;
            }
            if (bank is null)
            {
                yield return DiscoveryDiagnostic(selected, descriptor.Bank, error ?? "External bank unavailable.");
                continue;
            }
            foreach (ModelAssetDiscoveryItem clip in DiscoverExternalClips(bodyEntry, bodyModel, bank, descriptor.Bank, false, bodyEntry.Id))
            {
                yield return clip;
            }
            foreach ((ModelCatalogEntry weaponEntry, BattleModel weaponModel) in weapons.Where(item => item.Model.NativeWeaponUsesBodySkeleton != true && binding.SectionCount == 3))
            {
                foreach (ModelAssetDiscoveryItem clip in DiscoverExternalClips(weaponEntry, weaponModel, bank, descriptor.Bank, true, bodyEntry.Id))
                {
                    yield return clip;
                }
            }
        }
        foreach (ModelAssetDiscoveryItem clip in DiscoverVictoryBank(selected, bodyEntry, bodyModel, weapons, memory, character, selectedWeapon))
        {
            yield return clip;
        }
    }

    private BattleModel ReadDiscoveryBody(string path)
    {
        byte[] bytes = resources.Read(new ResourceKey(path));
        return resources.GetSource<IBattleModelSource>()?.ReadModel(new ResourceKey(path), bytes) ?? BattleResourceReader.ReadModel(Path.GetFileName(path), bytes);
    }

    private IEnumerable<ModelAssetDiscoveryItem> DiscoverExternalClips(ModelCatalogEntry owner, BattleModel model, byte[] bank, string bankId, bool weapon, string bodyId)
    {
        byte[]? frameCounts = null;
        string? error = null;
        try
        {
            int section = weapon ? 2 : 0;
            int start = BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(4 + section * 4));
            int end = weapon ? bank.Length : BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(8));
            int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bank);
            if (start < 8 + sectionCount * 4 || end < start || end > bank.Length)
            {
                throw new InvalidDataException("External animation section is outside its bank.");
            }
            frameCounts = NativeBattlePoseDecoder.ReadFrameCounts(bank.AsSpan(start, end - start)).ToArray();
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (frameCounts is null)
        {
            yield return DiscoveryDiagnostic(owner, bankId, error ?? "External bank is not decodable for this owner.");
            yield break;
        }
        Lazy<EditableModel> editable = new(() =>
        {
            BattleModel decoded = BattleResourceReader.ReadLinkedAnimationBank(model, bank, weapon);
            return ModelExchange.FromBattle(modelOverrides?.ApplyBattleBank(owner.Id, bankId, decoded) ?? decoded);
        });
        for (int index = 0; index < frameCounts.Length; index++)
        {
            yield return new(ModelAssetDiscoveryBindings.ClipTarget(owner.Id, bankId, index), $"{bankId} / Clip {index:000}", ModelAssetDiscoveryKind.Clip, owner.Id, owner.ResourcePath, bankId, index, "external bank binding; " + (weapon ? "weapon section 2" : "body section 0"), () => editable.Value)
            {
                NativeFrameCount = frameCounts[index],
                GroupId = ModelAssetDiscoveryBindings.ClipTarget(bodyId, bankId, index),
                RigOwnerId = owner.Id
            };
        }
    }
    private IEnumerable<ModelAssetDiscoveryItem> DiscoverVictoryBank(ModelCatalogEntry selected, ModelCatalogEntry bodyEntry, BattleModel body, List<(ModelCatalogEntry Entry, BattleModel Model)> weapons, IGameNativeMemorySource memory, int character, bool selectedWeapon)
    {
        byte[]? bank = null;
        int section = -1;
        string? error = null;
        try
        {
            byte[] mappings = memory.Read(0xB8B7E0, 12);
            for (int index = 0; index < 6; index++)
            {
                if (mappings[index * 2] == character)
                {
                    section = mappings[index * 2 + 1];
                    break;
                }
            }
            if (section >= 0)
            {
                string path = ModelAssetDiscoveryBindings.ResolveBattleFile(memory, 766);
                byte[] resource = resources.Read(new ResourceKey(path));
                int count = BinaryPrimitives.ReadInt32LittleEndian(resource);
                if (count is < 1 or > 128 || section >= count || resource.Length < 8 + count * 4)
                {
                    throw new InvalidDataException("Victory section mapping exceeds its resource directory.");
                }
                int start = BinaryPrimitives.ReadInt32LittleEndian(resource.AsSpan(4 + section * 4));
                int nestedCount = BinaryPrimitives.ReadInt32LittleEndian(resource.AsSpan(start));
                if (nestedCount is < 2 or > 3)
                {
                    throw new NotSupportedException("Victory bank requires a body-only or paired skeletal directory.");
                }
                int end = checked(start + BinaryPrimitives.ReadInt32LittleEndian(resource.AsSpan(start + 4 + nestedCount * 4)));
                if (start < 8 + count * 4 || end < start || end > resource.Length)
                {
                    throw new InvalidDataException("Victory bank extent exceeds its resource.");
                }
                bank = resource[start..end];
            }
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (error is not null)
        {
            yield return DiscoveryDiagnostic(selected, "victory", error);
            yield break;
        }
        if (bank is null)
        {
            yield break;
        }
        string bankId = "victory-" + section;
        foreach (ModelAssetDiscoveryItem clip in DiscoverExternalClips(bodyEntry, body, bank, bankId, false, bodyEntry.Id))
        {
            yield return clip;
        }
        foreach ((ModelCatalogEntry entry, BattleModel model) in weapons.Where(item => item.Model.NativeWeaponUsesBodySkeleton != true))
        {
            foreach (ModelAssetDiscoveryItem clip in DiscoverExternalClips(entry, model, bank, bankId, true, bodyEntry.Id))
            {
                yield return clip;
            }
        }
    }
}
