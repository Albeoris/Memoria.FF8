using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private ImmutableDictionary<int, EncounterPatchComposition> encounterCompositions = ImmutableDictionary<int, EncounterPatchComposition>.Empty;

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
            return new(true, Commit: () => encounterCompositions = ImmutableDictionary<int, EncounterPatchComposition>.Empty);
        }
        List<ModDiagnostic> diagnostics = [];
        ImmutableDictionary<int, EncounterPatchComposition>.Builder candidate = ImmutableDictionary.CreateBuilder<int, EncounterPatchComposition>();
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
                    EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, binding) with { NativeEvents = EncounterNativeEventResourceReader.Bind(assets.ReadEnemyNativeGraphicsResource(actors.Single(actor => actor.Id == binding.OriginalActorId).ModelId)) };
                    _ = ApplyCompiledEncounterDefinition(definition, binding, compiled);
                    ValidateEncounterPresentationAssets(assets, definition, binding, composition);
                    candidate.Add(formation, composition);
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
        ImmutableDictionary<int, EncounterPatchComposition> prepared = candidate.ToImmutable();
        return new(true, Commit: () => encounterCompositions = prepared);
    }

    private static void ValidateEncounterPresentationAssets(BattleAssets assets, BattleDefinition definition, EncounterBindingResult binding, EncounterPatchComposition composition)
    {
        if (composition.Presentations.IsEmpty)
        {
            return;
        }
        int modelId = definition.Combatants.Single(actor => actor.Id == binding.OriginalActorId).ModelId;
        BattleModel model = assets.LoadEnemy(modelId);
        foreach ((string id, EncounterPatchOperation presentation) in composition.Presentations)
        {
            if (!int.TryParse(presentation.Clip, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int clip) || (uint)clip >= model.Animations.Count || presentation.EndFrame >= model.Animations[clip].Frames.Count)
            {
                throw new InvalidDataException($"Encounter '{composition.Encounter}', presentation '{id}' clip '{presentation.Clip}' frames {presentation.StartFrame}–{presentation.EndFrame} do not fit the selected original model animation.");
            }
        }
    }

    public BattleDefinition ApplyEncounterMods(BattleDefinition definition, EncounterBindingContext context)
    {
        if (definition.EncounterId is not int formation || !encounterCompositions.TryGetValue(formation, out EncounterPatchComposition? composition))
        {
            ModDiagnostic? unavailable = encounterCompositions.IsEmpty ? Mods.Diagnostics.FirstOrDefault(diagnostic => diagnostic.RelativePath?.StartsWith(EncounterMods.PayloadDirectory, StringComparison.OrdinalIgnoreCase) == true && Mods.InstalledPackages.Any(package => package.Id == diagnostic.PackageId && package.RequestedEnabled)) : null;
            if (unavailable is not null)
            {
                throw new InvalidDataException($"An enabled encounter package has no valid prepared definition: {unavailable.PackageId}:{unavailable.RelativePath}: {unavailable.Message} Fix the package or disable it in Mods before starting an encounter.");
            }
            return definition;
        }
        EncounterBindingResult binding = EncounterBindingResolver.Resolve(composition.Encounter, definition, context);
        NativeBattleGraphicsResource resource = definition.NativeGraphics?.Bindings.SingleOrDefault(candidate => candidate.ActorId == binding.OriginalActorId)?.Resource ?? throw new InvalidDataException("Encounter policy preparation requires the selected original native model resource.");
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, binding) with { NativeEvents = EncounterNativeEventResourceReader.Bind(resource) };
        return ApplyCompiledEncounterDefinition(definition, binding, compiled);
    }

    private static BattleDefinition ApplyCompiledEncounterDefinition(BattleDefinition definition, EncounterBindingResult binding, EncounterCompiledDefinition compiled)
    {
        BattleCombatantDefinition[] combatants = definition.Combatants.ToArray();
        foreach ((string property, double multiplier) in compiled.ActorProperties)
        {
            const string suffix = "/stats.maximumHpMultiplier";
            if (!property.EndsWith(suffix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unsupported compiled encounter actor property: " + property);
            }
            string role = property[..^suffix.Length];
            string actorId = role == "original" ? binding.OriginalActorId : role.StartsWith("scene-", StringComparison.Ordinal) ? "enemy-" + role[6..] : role;
            int index = Array.FindIndex(combatants, actor => actor.Id == actorId);
            if (index < 0)
            {
                if (compiled.Templates.ContainsKey(role))
                {
                    continue;
                }
                throw new InvalidDataException("Maximum-HP modifiers currently require a selected baseline actor: " + role);
            }
            BattleCombatantDefinition actor = combatants[index];
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
