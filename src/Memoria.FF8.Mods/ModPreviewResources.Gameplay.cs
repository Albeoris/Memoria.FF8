using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.KernelBin;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    private const string LevelBonusPreview = "gameplay:level-based-stat-bonuses";
    private const string RosterPreview = "gameplay:extended-roster";

    internal static ModPreviewEntry? DescribeGameplayPath(ModFile file)
    {
        if (file.Format == ModFormat.Memoria)
        {
            string path = file.RelativePath;
            string name = Path.GetFileNameWithoutExtension(path);
            if (StatusMods.IsStatusDocument(file))
            {
                return new(path, "Gameplay", "Statuses", name);
            }
            if (EncounterMods.IsPayloadPath(path))
            {
                return new(path, "Gameplay", "Encounters", name);
            }
            if (path.Equals(GuardianForceAbilityMods.PayloadPath, StringComparison.OrdinalIgnoreCase))
            {
                return new(path, "Gameplay", "Guardian Forces", "Ability learning");
            }
            if (path.Equals(GuardianForceRules.PayloadPath, StringComparison.OrdinalIgnoreCase))
            {
                return new(path, "Gameplay", "Guardian Forces", "Ability costs, descriptions and summon limits");
            }
            if (IsFf7EnemyDocument(path))
            {
                return new(path, "Gameplay", "FFVII enemies", name);
            }
            if (IsFieldLightingDocument(path))
            {
                return new(path, "Lighting", "Fields", name);
            }
        }
        return DescribeNativeDataPath(file);
    }

    internal static IEnumerable<ModPreviewEntry> DescribeBundledGameplay(ModPackage package)
    {
        if (!package.IsBundled || package.Format != ModFormat.Memoria)
        {
            yield break;
        }
        string? component = OfficialModCatalog.SeedComponents.FirstOrDefault(candidate => candidate.RuntimePackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase))?.Id;
        if (component == "level-based-stat-bonuses")
        {
            yield return new(LevelBonusPreview, "Gameplay", "Character growth", "HP and stat bonus abilities");
        }
        if (component == "extended-ffviii-roster")
        {
            yield return new(RosterPreview, "Gameplay", "Party", "Additional battle party members");
        }
    }

    internal static ModPreviewComparison? CompareGameplay(ModCatalogSnapshot catalog, ModPreviewEntry entry, ModFile? file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (file is null && catalog.Packages.SelectMany(DescribeBundledGameplay).Contains(entry))
        {
            return entry.Id == LevelBonusPreview ? CompareLevelBonuses() : CompareRoster(profile);
        }
        if (file is null && entry.Id.StartsWith(FieldGameplayPrefix, StringComparison.Ordinal))
        {
            return CompareFieldGameplay(catalog, entry, profile, cancellationToken);
        }
        if (file is not { Format: ModFormat.Memoria })
        {
            return file is null ? null : CompareNativeData(catalog, file, profile, cancellationToken);
        }
        if (StatusMods.IsStatusDocument(file))
        {
            return CompareStatusChanges(catalog, file, profile);
        }
        if (EncounterMods.IsPayloadPath(file.RelativePath))
        {
            return CompareEncounterChanges(catalog, file, profile);
        }
        if (file.RelativePath.Equals(GuardianForceAbilityMods.PayloadPath, StringComparison.OrdinalIgnoreCase))
        {
            return CompareGuardianAbilityChanges(file, profile);
        }
        if (file.RelativePath.Equals(GuardianForceRules.PayloadPath, StringComparison.OrdinalIgnoreCase))
        {
            return CompareGuardianRules(catalog, profile);
        }
        if (IsFf7EnemyDocument(file.RelativePath))
        {
            return CompareFf7EnemyChanges(catalog, file, profile, cancellationToken);
        }
        return IsFieldLightingDocument(file.RelativePath) ? CompareFieldLighting(catalog, file) : null;
    }

    private static ModPreviewComparison CompareLevelBonuses()
    {
        int[] levels = [10, 30, 60, 100];
        BattleNativeCharacterGrowth sample = new(0, [0, 0, 0, 0, 0, 0]);
        string Example(int level)
        {
            BattleNativeCharacterGrowth hp = sample.Effective(true, level, 65);
            BattleNativeCharacterGrowth stats = sample.Effective(true, level, 66u | 67u << 8 | 68u << 16 | 69u << 24);
            return $"Lv {level}  ·  HP +{hp.HitPointBonus}  ·  STR, VIT, MAG or SPR +{stats.StatBonuses[0]} each";
        }
        return new(new("Equipped Bonus abilities permanently increase the corresponding stat on level-up."), new("Equipped Bonus abilities grant a bonus based on current level.\n\n" + string.Join("\n", levels.Select(Example))));
    }

    private static ModPreviewComparison CompareRoster(ModPreviewResourcesProfile profile)
    {
        using IResourceSource source = OpenSource(profile);
        ResourceService resources = new(source, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: resources);
        NativeCharacterVitals before = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals after = before.WithExtendedRoster(true);
        KernelData kernel = ReadKernel(resources);
        string Name(int id) => kernel.Characters.ElementAtOrDefault(id)?.Name ?? $"Character {id}";
        string[] guests = Enumerable.Range(0, after.Records.Length).Where(row => NativeCharacterVitals.IsExtendedRosterCharacter(row)).Select(row => Name(after.Records[row].CharacterId)).ToArray();
        return new(new($"{before.Records.Length} native character records.\nDream and guest characters are available at their story events."), new($"{after.Records.Length} character records.\nAdditional permanent battle party choices\n\n{string.Join("\n", guests)}"));
    }

    private static ModPreviewComparison CompareStatusChanges(ModCatalogSnapshot catalog, ModFile file, ModPreviewResourcesProfile profile)
    {
        if (!StatusMods.TryPrepare(catalog, out StatusMods status, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        List<string> before = [];
        List<string> after = [];
        foreach (StatusStatModifier rule in status.Modifiers.Where(rule => status.ModifierSource(rule.Status, rule.Stat) == file))
        {
            StatusStatModifier native = StatusStatRules.Native.FirstOrDefault(item => item.Status == rule.Status && item.Stat == rule.Stat);
            string label = $"{rule.Status} · {rule.Stat}";
            before.Add($"{label}  −{native.Value}%");
            after.Add($"{label}  −{rule.Value}%");
        }
        StatusNameOverride[] names = status.Names.Where(name => name.Source == file).ToArray();
        if (names.Length != 0)
        {
            using IResourceSource source = OpenSource(profile);
            ResourceService resources = new(source, profile.Language);
            NativeLocalizationTextReferences references = new(resources);
            foreach (StatusNameOverride name in names)
            {
                string key = name.Reference.ResolveKey(resources.Source.Language);
                string semanticId = "battle.status." + (name.Status == NativeStatusId.Vit0 ? "vit-0" : System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(name.Status.ToString()));
                string original = SystemTextCatalog.Defaults.GetValueOrDefault(semanticId) ?? name.Status.ToString();
                before.Add($"Status name  {original}");
                after.Add($"Status name  {references.ResolveOriginal(key)}");
            }
        }
        return CompareLines(before, after);
    }

    private static ModPreviewComparison CompareGuardianAbilityChanges(ModFile file, ModPreviewResourcesProfile profile)
    {
        using IResourceSource source = OpenSource(profile);
        ResourceService resources = new(source, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: resources);
        using BattleAssets battle = new(profile.GameDirectory, resources);
        var abilities = ReadAbilityText(ReadKernel(resources));
        var baseline = assets.ReadNewGameCharacterVitals().ResolveGuardianForceTemplates();
        GuardianForceAbilityPatchDocument patch = GuardianForceAbilityMods.Parse(file.ReadBytes());
        GuardianForceAbilityComposition modified = GuardianForceAbilityComposer.Compose(baseline, [new(file.PackageId, patch)]);
        int[] guardians = patch.Operations.Select(operation => operation.GuardianForce).Distinct().Order().ToArray();
        string Read(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates) => string.Join("\n\n", guardians.Select(id =>
        {
            string label = battle.ActionDefinitions.FirstOrDefault(action => action.Ability == BattleAbility.GuardianForce && action.Id == id)?.DisplayName ?? $"GF {id}";
            return label + "\n" + string.Join("\n", templates[id].Select(ability => $"{abilities.ElementAtOrDefault(ability.Ability).Name ?? $"Ability {ability.Ability}"} · {(ability.InitialLearned ? "learned" : $"{ability.RequiredPoints} AP")}"));
        }));
        return new(new(Read(baseline)), new(Read(modified.Templates)));
    }

    private static ModPreviewComparison CompareGuardianRules(ModCatalogSnapshot catalog, ModPreviewResourcesProfile profile)
    {
        if (!GuardianForceRules.TryPrepare(catalog, out GuardianForceRules rules, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        using IResourceSource source = OpenSource(profile);
        ResourceService resources = new(source, profile.Language);
        using GameAssets assets = new(profile.GameDirectory, resources: resources);
        var abilities = ReadAbilityText(ReadKernel(resources));
        ImmutableArray<byte> costs = assets.ReadNewGameCharacterVitals().MenuDefinitions.AbilityPoints;
        List<string> before = [];
        List<string> after = [];
        if (rules.DefaultSummonDamageLimit is int defaultLimit)
        {
            before.Add("Summons use the game's damage limits.");
            after.Add($"Default summon damage limit  {defaultLimit:N0}");
        }
        for (int id = 1; id < costs.Length; id++)
        {
            var ability = abilities.ElementAtOrDefault(id);
            string name = ability.Name ?? $"Ability {id}";
            if (rules.AbilityPointCosts.TryGetValue(id, out byte cost))
            {
                before.Add($"{name}  ·  {costs[id]} AP");
                after.Add($"{name}  ·  {cost} AP");
            }
            if (rules.Description(id, resources.Source.Language) is { } description)
            {
                before.Add($"{name}  ·  {ability.Description ?? "—"}");
                after.Add($"{name}  ·  {description}");
            }
            if (rules.SummonDamageLimits.TryGetValue(id, out int limit))
            {
                before.Add($"{name}  ·  Standard summon damage limit");
                after.Add($"{name}  ·  Learned summon damage limit {limit:N0}");
            }
        }
        return CompareLines(before, after);
    }

    private static (string? Name, string? Description)[] ReadAbilityText(KernelData kernel) =>
    [
        .. kernel.JunctionAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.CommandAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.CharacterStatAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.CharacterAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.PartyAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.GuardianAbilities.Select(ability => (ability.Name, ability.Description)),
        .. kernel.MenuAbilities.Select(ability => (ability.Name, ability.Description))
    ];

    private static ModPreviewComparison CompareEncounterChanges(ModCatalogSnapshot catalog, ModFile file, ModPreviewResourcesProfile profile)
    {
        if (!EncounterMods.TryPrepare(catalog, out EncounterMods encounters, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        EncounterPatchLayer layer = encounters.Layers.Single(candidate => candidate.PayloadPath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase));
        string encounterKey = layer.Document.Encounter;
        int formation = EncounterBindingResolver.ResolveEncounterId(encounterKey);
        using IResourceSource source = OpenSource(profile);
        using BattleAssets assets = new(profile.GameDirectory, new(source, profile.Language));
        BattleEncounter encounter = assets.LoadEncounter(formation);
        BattleCombatantDefinition[] actors = encounter.Slots.Where(slot => slot.IsEnemyModel && slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0)).Select(slot => new BattleCombatantDefinition
        {
            Id = $"enemy-{slot.Slot}",
            Name = assets.ReadEnemyName(slot.ModelId),
            SceneSlot = slot.Slot,
            InitialActive = slot.Enabled,
            ModelId = slot.ModelId,
            NativeAi = assets.ReadEnemyAi(slot.ModelId, 1),
            NativeTemplate = assets.ReadEnemyTemplate(slot.ModelId, checked((byte)slot.Level))
        }).ToArray();
        string profileName = "Classic";
        BattleDefinition definition = new() { EncounterId = formation, StageId = encounter.StageId, Combatants = actors, NativeActions = assets.NativeActions.ToArray() };
        EncounterBindingResult binding = EncounterBindingResolver.Resolve(encounterKey, definition, new(profileName));
        ImmutableArray<string> baseline = actors.Where(actor => actor.InitialActive).Select(actor => actor.Id == binding.OriginalActorId ? "original" : $"scene-{actor.SceneSlot}").ToImmutableArray();
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(encounterKey, profileName, baseline), [layer]);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, binding);
        List<string> before = [$"Formation {formation} · Stage {encounter.StageId}", "", .. actors.Where(actor => actor.InitialActive).Select(actor => $"Slot {actor.SceneSlot}  ·  {actor.Name}")];
        List<string> after = [.. before];
        foreach (EncounterCompiledTemplate template in compiled.Templates.Values.OrderBy(template => template.Id, StringComparer.Ordinal))
        {
            EncounterCompiledPresentation arrival = compiled.Presentations[template.Arrival];
            after.Add($"Summon {template.Id}  ·  from {template.InheritFrom}  ·  clip {arrival.Clip}, frames {arrival.StartFrame}–{arrival.EndFrame}");
        }
        foreach ((string role, string policyId) in compiled.NormalActionPolicies.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            EncounterCompiledPolicy policy = compiled.Policies[policyId];
            after.Add($"{role}  ·  {string.Join(", ", policy.Rules.Select(rule => rule.Action.Kind == EncounterCompiledActionKind.SummonGroup ? "Summon " + rule.Action.Group : string.Join("/", rule.Action.Spells.Select(spell => spell.NativeAction.Name))))}");
        }
        if (compiled.Camera is { } camera)
        {
            before.Add("Camera elevation offset  0°");
            after.Add($"Camera elevation offset  {camera.ElevationDegrees:g}°");
        }
        foreach ((string property, double multiplier) in compiled.ActorProperties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            before.Add($"{property}  ×1");
            after.Add($"{property}  ×{multiplier:g}");
        }
        foreach (EncounterCompiledDamageRoute route in compiled.DamageRoutes.Values.OrderBy(route => route.Id, StringComparer.Ordinal))
        {
            after.Add($"Damage from {route.SourceActor}  →  {string.Join(", ", route.Recipients)}");
        }
        return CompareLines(before, after);
    }

    private static bool IsFf7EnemyDocument(string path) => path.StartsWith(Ff7EnemyExtensionMods.PayloadDirectory, StringComparison.OrdinalIgnoreCase) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static ModPreviewComparison CompareFf7EnemyChanges(ModCatalogSnapshot catalog, ModFile file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (!Ff7EnemyExtensionMods.TryPrepare(catalog, out Ff7EnemyExtensionCatalog extensions, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        Ff7EnemyExtensionDocument document = Ff7EnemyExtensionDocument.Parse(new UTF8Encoding(false, true).GetString(file.ReadBytes()).TrimStart('\uFEFF'));
        Dictionary<short, (int Formation, SceneBattleEnemy Enemy)> installed = [];
        if (!string.IsNullOrWhiteSpace(profile.Ff7GameDirectory))
        {
            Ff7Installation installation = Ff7Installation.Discover(profile.Ff7GameDirectory);
            var selected = document.Enemies.Select(enemy => enemy.EnemyId).ToHashSet();
            foreach (SceneFormation formation in SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!formation.Enemies.Any(enemy => selected.Contains(enemy.EnemyId) && !installed.ContainsKey(enemy.EnemyId)))
                {
                    continue;
                }
                foreach (SceneBattleEnemy enemy in SceneBattleData.Read(formation).Enemies.Where(enemy => selected.Contains(enemy.Placement.EnemyId)))
                {
                    installed.TryAdd(enemy.Placement.EnemyId, (formation.FormationId, enemy));
                }
                if (installed.Count == selected.Count)
                {
                    break;
                }
            }
        }
        List<string> before = [];
        List<string> after = [];
        foreach (short id in document.Enemies.Select(enemy => enemy.EnemyId).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Ff7EnemyExtensionDelta enemy = extensions.Find(id)!;
            installed.TryGetValue(id, out var sample);
            string name = sample.Enemy is { } found ? string.Concat(FieldText.Read(found.Rewards.Name.Span).Select(token => token.Text)) : $"Enemy {id}";
            string title = sample.Enemy is null ? name : $"{name} [{id}] · Formation {sample.Formation}";
            List<string> originals = [title];
            List<string> changes = [title];
            if (enemy.ScanDescription is { } scan)
            {
                originals.Add("No extension Scan description.");
                changes.Add(scan);
            }
            bool levelsScale = enemy.ScaleLevel ?? enemy.Growth is { Count: > 0 };
            if (levelsScale)
            {
                changes.Add("Scales with party level.");
            }
            if (sample.Enemy is { } baseline)
            {
                int level = levelsScale ? new Ff7EnemyLevelRules().MaximumEnemyLevel : baseline.Level;
                originals.Add($"Native level {baseline.Level}");
                changes.Add($"Example enemy level {level}");
                AddGrowth("HP", baseline.MaxHitPoints, enemy.Growth?.GetValueOrDefault("hp"), int.MaxValue);
                AddGrowth("MP", baseline.MaxMagicPoints, enemy.Growth?.GetValueOrDefault("mp"), ushort.MaxValue);
                AddGrowth("Attack", baseline.Strength, enemy.Growth?.GetValueOrDefault("attack"), byte.MaxValue);
                AddGrowth("Magic", baseline.Magic, enemy.Growth?.GetValueOrDefault("magic"), byte.MaxValue);
                AddGrowth("EXP", baseline.Rewards.Experience, enemy.RewardGrowth?.GetValueOrDefault("exp"), uint.MaxValue);
                AddGrowth("AP", baseline.Rewards.AbilityPoints, enemy.RewardGrowth?.GetValueOrDefault("ap"), ushort.MaxValue);
                AddGrowth("Gil", baseline.Rewards.Gil, enemy.RewardGrowth?.GetValueOrDefault("gil"), uint.MaxValue);

                void AddGrowth(string label, uint value, int[]? coefficients, uint maximum)
                {
                    if (coefficients is null)
                    {
                        return;
                    }
                    uint projected = Ff7EnemyExtensionCatalog.ScaleUnsigned(value, level, baseline.Level, coefficients, maximum);
                    originals.Add($"{label}  ·  {value:N0}");
                    changes.Add($"{label}  ·  {projected:N0}");
                }
            }
            else
            {
                originals.Add("No matching native formation is available.");
                foreach (var growth in (enemy.Growth ?? []).Concat(enemy.RewardGrowth ?? []))
                {
                    int percent = Ff7EnemyExtensionCatalog.Scale(100, 100, 20, growth.Value, int.MaxValue);
                    changes.Add($"{growth.Key.ToUpperInvariant()}  ·  {percent}% of base at Lv 100 for a Lv 20 enemy");
                }
            }
            foreach (Ff7EnemyDrawTier tier in enemy.DrawTiers ?? [])
            {
                changes.Add($"Draw at +{tier.MinimumDelta} levels  ·  {string.Join(", ", tier.Slots.Select(slot => $"Magic {slot.MagicId} ({slot.Resistance} resistance)"))}");
            }
            foreach (Ff7EnemyItemReplacement item in enemy.ItemReplacements ?? [])
            {
                changes.Add($"{item.Channel} at +{item.MinimumDelta} levels  ·  item {item.FromItemId} → {item.ToItemId}");
            }
            before.Add(string.Join("\n", originals));
            after.Add(string.Join("\n", changes));
        }
        return new(new(string.Join("\n\n", before)), new(string.Join("\n\n", after)));
    }

    private static IResourceSource OpenSource(ModPreviewResourcesProfile profile) => GameDataSourceFactory.Open(profile.GameDirectory, profile.ModelSourceDirectory, profile.Language);
    private static KernelData ReadKernel(ResourceService resources) => KernelData.ReadFromArray(resources.Read(new("main/kernel.bin")), NativeTextEncoding.Create(resources.Source.Language));
    private static InvalidDataException PreparationError(IEnumerable<ModDiagnostic> diagnostics) => new(string.Join(Environment.NewLine, diagnostics.Select(error => error.Message)));
    private static ModPreviewComparison CompareLines(IReadOnlyList<string> before, IReadOnlyList<string> after) => new(new(before.Count == 0 ? "No original entries." : string.Join("\n", before)), new(after.Count == 0 ? "No changes." : string.Join("\n", after)));
}
