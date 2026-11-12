using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverSaveAdapter
{
    internal const string Ff8Codec = "memoria-ff8-gameplay-components-v1";

    internal static CombinedSessionSave? ReadEnvelope(string path)
    {
        using FileStream source = File.OpenRead(path);
        return CombinedSessionCodec.HasSignature(source) ? CombinedSessionCodec.Read(source) : null;
    }

    internal static GameplaySaveComponentDocument ReadFf8(CombinedSessionSave save)
    {
        if (save.Ff8PayloadFormat != Ff8Codec || save.Ff8Payload is not { Length: > 0 } payload)
        {
            throw new NotSupportedException("This crossover save does not contain a supported FFVIII continuation.");
        }
        using MemoryStream source = new(payload, false);
        return GameplaySaveCodec.Read(source);
    }

    internal static GameplaySaveComponentDocument ReadFf8ForResume(CombinedSessionSave save)
    {
        GameplaySaveComponentDocument document = ReadFf8(save);
        if (Ff7Scene.ReadBattleAtbMode(save.TurnBasedBattle) is { } mode && document.Progression is { Rules: var rules } progression)
        {
            GameplayPreferences? pending = rules.PendingPreferences is { } preferences ? preferences with { AtbMode = mode } : null;
            document = document with { Progression = progression with { Rules = rules with { Preferences = rules.Preferences with { AtbMode = mode }, PendingPreferences = pending } } };
        }
        return document;
    }

    internal static CombinedSessionSave RemoveDisabledExtendedRosterBindings(CombinedSessionSave save, bool? extendedRosterEnabled = null)
    {
        bool hasExtendedMappings = save.Mappings.Any(mapping => NativeCharacterVitals.IsExtendedRosterCharacter(mapping.AppearanceCharacter));
        IReadOnlyList<Ff7MixedCombatBinding> bindings = save.GetMixedCombatBindings();
        bool hasExtendedCombatBindings = bindings.Any(binding => NativeCharacterVitals.IsExtendedRosterCharacter(binding.OriginCharacter));
        if (extendedRosterEnabled == true || !hasExtendedMappings && !hasExtendedCombatBindings)
        {
            return save;
        }
        extendedRosterEnabled ??= save.Ff8Payload is { Length: > 0 } ? ReadFf8(save).Characters.ExtendedRosterEnabled : null;
        if (extendedRosterEnabled != false)
        {
            return save;
        }
        if (hasExtendedMappings)
        {
            save = save with { Mappings = WithoutExtendedRosterMappings(save.Mappings) };
        }
        if (hasExtendedCombatBindings)
        {
            // Native guest slots retain their explicit bindings for battle restoration and settlement.
            IReadOnlyList<Ff7BattleGuestSlotState> battleOwners = save.Ff7?.Battle?.GetGuestSlotStates() ?? [];
            Ff7MixedCombatBinding[] retained = bindings.Where(binding => !NativeCharacterVitals.IsExtendedRosterCharacter(binding.OriginCharacter)
                || battleOwners.Any(owner => owner.Binding.StoryCharacter == binding.StoryCharacter && owner.Binding.OriginCharacter == binding.OriginCharacter)).ToArray();
            if (retained.Length != bindings.Count)
            {
                save = save with { MixedCombat = retained.Length == 1 ? retained[0] : null, MixedCombatBindings = retained.Length > 1 ? retained : null };
            }
        }
        return save;
    }

    internal static CharacterAppearanceMapping[] WithoutExtendedRosterMappings(CharacterAppearanceMapping[] mappings)
    {
        return mappings.Where(mapping => !NativeCharacterVitals.IsExtendedRosterCharacter(mapping.AppearanceCharacter)).ToArray();
    }

    internal static void EnsureOriginalExportAllowed(string path)
    {
        if (ReadEnvelope(path) is not null)
        {
            throw new NotSupportedException("Combined world saves require Memoria format. Original FFVIII saves cannot preserve FFVII progress and appearance mappings.");
        }
    }

    internal static SaveSummary? ReadSummary(string path)
    {
        CombinedSessionSave? save = ReadEnvelope(path);
        if (save is null)
        {
            return GameSession.ReadSummary(path);
        }
        if (save.ActiveGame != SessionGame.Ff8)
        {
            throw new NotSupportedException("An active FFVII save requires its own game summary.");
        }
        using MemoryStream source = new(save.Ff8Payload ?? throw new InvalidDataException("A player slot requires an FFVIII continuation."), false);
        ReadFf8(save);
        return GameplaySaveCodec.ReadSummary(source);
    }
}
