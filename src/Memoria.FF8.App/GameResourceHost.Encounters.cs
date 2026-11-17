using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private sealed record PreparedEncounterPolicy(EncounterPatchComposition Composition, EncounterNativeEventBinding NativeEvents);

    private ImmutableDictionary<int, PreparedEncounterPolicy> preparedEncounterPolicies = ImmutableDictionary<int, PreparedEncounterPolicy>.Empty;

    public void ConnectEncounterMods()
    {
        PrepareEncounterPublication = PrepareEncounterModPublication;
        Mods.Refresh();
        Pump(true);
    }

    private ModPreparationResult PrepareEncounterModPublication(EncounterMods mods, ModCatalogSnapshot catalog, IReadOnlyList<EncodedResource> encodedResources)
    {
        if (mods.Layers.IsEmpty)
        {
            return new(true, Commit: () => preparedEncounterPolicies = ImmutableDictionary<int, PreparedEncounterPolicy>.Empty);
        }
        List<ModDiagnostic> diagnostics = [];
        ImmutableDictionary<int, PreparedEncounterPolicy>.Builder candidate = ImmutableDictionary.CreateBuilder<int, PreparedEncounterPolicy>();
        ResourceService selected = new(source, resourceLanguage);
        selected.Prepare(null, encodedResources: encodedResources).Commit();
        try
        {
            using BattleAssets assets = new(gameDirectory, selected);
            foreach (IGrouping<string, EncounterPatchLayer> group in mods.Layers.GroupBy(layer => layer.Document.Encounter, StringComparer.Ordinal))
            {
                try
                {
                    int formation = EncounterBindingResolver.ResolveEncounterId(group.Key);
                    BattleEncounter encounter = assets.LoadEncounter(formation);
                    BattleCombatantDefinition[] actors = encounter.Slots.Where(slot => slot.IsEnemyModel && slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0)).Select(slot => new BattleCombatantDefinition
                    {
                        Id = $"enemy-{slot.Slot}",
                        SceneSlot = slot.Slot,
                        InitialActive = slot.Enabled,
                        ModelId = slot.ModelId,
                        NativeAi = assets.ReadEnemyAi(slot.ModelId, 1),
                        NativeTemplate = assets.ReadEnemyTemplate(slot.ModelId, checked((byte)slot.Level))
                    }).ToArray();
                    BattleDefinition definition = new() { EncounterId = formation, StageId = encounter.StageId, Combatants = actors, NativeActions = assets.NativeActions.ToArray() };
                    EncounterBindingResult binding = EncounterBindingResolver.Resolve(group.Key, definition, new("Classic"));
                    ImmutableArray<string> baselineActors = actors.Where(actor => actor.InitialActive).Select(actor => actor.Id == binding.OriginalActorId ? "original" : "scene-" + actor.SceneSlot).ToImmutableArray();
                    EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(group.Key, "Classic", baselineActors), group);
                    BattleCombatantDefinition original = actors.Single(actor => actor.Id == binding.OriginalActorId);
                    EncounterNativeEventBinding nativeEvents = EncounterNativeEventBinding.Bind(binding, original, assets.ReadEnemyNativeGraphicsResource(original.ModelId));
                    EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, binding) with { NativeEvents = nativeEvents };
                    _ = ApplyCompiledEncounterDefinition(definition, binding, compiled);
                    ValidateEncounterPresentationAssets(assets, definition, compiled);
                    candidate.Add(formation, new(composition, nativeEvents));
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException or ArgumentException or OverflowException)
                {
                    diagnostics.AddRange(group.Select(layer => new ModDiagnostic("Memoria.InvalidEncounterComposition", exception.Message, layer.PackageId, layer.PayloadPath)));
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException or ArgumentException or OverflowException)
        {
            diagnostics.AddRange(mods.Layers.Select(layer => new ModDiagnostic("Memoria.InvalidEncounterResources", exception.Message, layer.PackageId, layer.PayloadPath)));
        }
        if (diagnostics.Count != 0)
        {
            return new(false, Diagnostics: diagnostics);
        }
        ImmutableDictionary<int, PreparedEncounterPolicy> prepared = candidate.ToImmutable();
        return new(true, Commit: () => preparedEncounterPolicies = prepared);
    }

    private static void ValidateEncounterPresentationAssets(BattleAssets assets, BattleDefinition definition, EncounterCompiledDefinition compiled)
    {
        foreach (EncounterCompiledTemplate template in compiled.Templates.Values)
        {
            string role = EncounterPolicyRuntime.ResolveTemplateSourceRole(compiled, template.Id);
            string actorId = role == "original" ? compiled.OriginalActorId : role.StartsWith("scene-", StringComparison.Ordinal) ? "enemy-" + role[6..] : role;
            BattleCombatantDefinition actor = definition.Combatants.SingleOrDefault(model => model.Id == actorId)
                ?? throw new InvalidDataException($"Encounter template '{template.Id}' lacks its installed source role '{role}'.");
            BattleModel model = assets.LoadEnemy(actor.ModelId);
            EncounterCompiledPresentation presentation = compiled.Presentations[template.Arrival];
            if (!int.TryParse(presentation.Clip, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int clip) || (uint)clip >= model.Animations.Count || presentation.EndFrame >= model.Animations[clip].Frames.Count)
            {
                throw new InvalidDataException($"Encounter template '{template.Id}' has arrival clip '{presentation.Clip}' frames {presentation.StartFrame}–{presentation.EndFrame} outside source model {actor.ModelId}.");
            }
        }
    }

    public BattleDefinition ApplyEncounterMods(BattleDefinition definition, EncounterBindingContext context)
    {
        if (definition.EncounterId is not int formation || !preparedEncounterPolicies.TryGetValue(formation, out PreparedEncounterPolicy? published))
        {
            ModDiagnostic? unavailable = preparedEncounterPolicies.IsEmpty ? Mods.Diagnostics.FirstOrDefault(diagnostic => diagnostic.RelativePath?.StartsWith(EncounterMods.PayloadDirectory, StringComparison.OrdinalIgnoreCase) == true && Mods.InstalledPackages.Any(package => package.Id == diagnostic.PackageId && package.RequestedEnabled)) : null;
            if (unavailable is not null)
            {
                throw new InvalidDataException($"An enabled encounter package has no valid prepared definition: {unavailable.PackageId}:{unavailable.RelativePath}: {unavailable.Message} Fix the package or disable it in Mods before starting an encounter.");
            }
            return definition;
        }
        EncounterBindingResult binding = EncounterBindingResolver.Resolve(published.Composition.Encounter, definition, context);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(published.Composition, definition, binding) with { NativeEvents = published.NativeEvents };
        return ApplyCompiledEncounterDefinition(definition, binding, compiled);
    }

    internal static BattleDefinition ApplyCompiledEncounterDefinition(BattleDefinition definition, EncounterBindingResult binding, EncounterCompiledDefinition compiled)
    {
        BattleCombatantDefinition[] combatants = definition.Combatants.ToArray();
        foreach ((string property, double multiplier) in compiled.ActorProperties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int separator = property.LastIndexOf('/');
            string name = property[(separator + 1)..];
            if (separator < 0 || name is not ("stats.maximumHpMultiplier" or EncounterActorProperties.ReflectResistanceMultiplier))
            {
                throw new InvalidDataException("Unsupported compiled encounter actor property: " + property);
            }
            string role = property[..separator];
            string actorId = role == "original" ? binding.OriginalActorId : role.StartsWith("scene-", StringComparison.Ordinal) ? "enemy-" + role[6..] : role;
            int index = Array.FindIndex(combatants, actor => actor.Id == actorId);
            if (index < 0)
            {
                if (compiled.Templates.ContainsKey(role))
                {
                    continue;
                }
                throw new InvalidDataException("Encounter actor modifiers require a selected baseline actor or template: " + role);
            }
            BattleCombatantDefinition actor = combatants[index];
            if (name == EncounterActorProperties.ReflectResistanceMultiplier)
            {
                combatants[index] = EncounterActorProperties.ScaleReflectResistance(actor, multiplier);
                continue;
            }
            if (actor.MaximumHpMultiplier != 1)
            {
                throw new InvalidDataException("Encounter maximum-HP multiplier has already been applied to actor: " + role);
            }
            int maximumHp = EncounterActorProperties.ScaleMaximumHp(actor.MaximumHp, multiplier);
            combatants[index] = actor with { MaximumHpMultiplier = multiplier, MaximumHp = maximumHp, CurrentHp = maximumHp };
        }
        return definition with { EncounterMod = compiled, Combatants = combatants };
    }
}
