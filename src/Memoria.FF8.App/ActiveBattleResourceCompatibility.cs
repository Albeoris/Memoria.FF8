using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Keeps the resource owners of an already allocated battle while comparing its gameplay definition against another installed edition.</summary>
internal static class ActiveBattleResourceCompatibility
{
    internal static string AllocationImage(NativeFieldRuntimeProfile? fieldProfile, NativeBattleGraphicsRules? graphicsRules)
    {
        graphicsRules?.Validate();
        if (fieldProfile is not null && (!fieldProfile.IsVerified || graphicsRules is not null && fieldProfile.ExecutableHash != graphicsRules.ExecutableSha256))
        {
            throw new InvalidDataException("Restored battle allocation profiles disagree with their authenticated graphics owner.");
        }
        return fieldProfile?.ExecutableHash ?? graphicsRules?.ExecutableSha256 ?? throw new InvalidDataException("Restored battle entry requires its authenticated allocation image identity.");
    }

    internal static BattleDefinition RetainAllocatedResources(BattleDefinition current, BattleDefinition saved, string allocationImage, string selectedImage)
    {
        if (allocationImage == selectedImage)
        {
            return current;
        }
        if (!NativeFieldRuntimeProfile.IsSupportedImage(allocationImage) || !NativeFieldRuntimeProfile.IsSupportedImage(selectedImage) || saved.NativeGraphics is not { } savedGraphics || current.NativeGraphics is not { } currentGraphics || savedGraphics.Rules.ExecutableSha256 != allocationImage || currentGraphics.Rules.ExecutableSha256 != selectedImage)
        {
            throw new InvalidDataException("Active battle resource compatibility requires its authenticated allocation and selected image identities.");
        }
        savedGraphics.Rules.Validate();
        currentGraphics.Rules.Validate();
        if (savedGraphics.Bindings.Length != currentGraphics.Bindings.Length || current.GuardianFormResources.Length != saved.GuardianFormResources.Length || current.Combatants.Length != saved.Combatants.Length || current.ActionDefinitions.Length != saved.ActionDefinitions.Length || current.NativeActions.Length != saved.NativeActions.Length || current.NativeGlobalMessages.Length != saved.NativeGlobalMessages.Length)
        {
            throw new InvalidDataException("Active battle resource compatibility cannot change participant, action or presentation ownership.");
        }
        ImmutableArray<NativeBattleGraphicsBinding> bindings = currentGraphics.Bindings.Select((binding, index) =>
        {
            NativeBattleGraphicsBinding prior = savedGraphics.Bindings[index];
            if (binding.ActorId != prior.ActorId || binding.ModelId != prior.ModelId)
            {
                throw new InvalidDataException("Active battle model binding differs from its allocated actor identity.");
            }
            return binding with { Resource = RetainModel(binding.Resource, prior.Resource) };
        }).ToImmutableArray();
        ImmutableArray<BattleGuardianFormResources> forms = current.GuardianFormResources.Select((form, index) =>
        {
            BattleGuardianFormResources prior = saved.GuardianFormResources[index];
            if (form.GuardianForceId != prior.GuardianForceId)
            {
                throw new InvalidDataException("Active guardian form differs from its allocated resource identity.");
            }
            return form with { VisualResource = RetainModel(form.VisualResource, prior.VisualResource), PhysicalAction = NormalizeAction(form.PhysicalAction, prior.PhysicalAction)! };
        }).ToImmutableArray();
        NativeBattleActionCameraRules? camera = current.NativeActionCameraRules;
        if (camera is not null && saved.NativeActionCameraRules is { } priorCamera)
        {
            camera.Validate();
            priorCamera.Validate();
            if (camera.ExecutableSha256 != selectedImage || priorCamera.ExecutableSha256 != allocationImage)
            {
                throw new InvalidDataException("Active battle camera rules lost their source image identities.");
            }
            camera = camera with { ExecutableSha256 = allocationImage };
        }
        return current with
        {
            NativeGraphics = currentGraphics with { Rules = currentGraphics.Rules with { ExecutableSha256 = allocationImage }, Bindings = bindings },
            GuardianFormResources = forms,
            NativeActionCameraRules = camera,
            Combatants = current.Combatants.Select((actor, index) => NormalizeCombatant(actor, saved.Combatants[index])).ToArray(),
            ActionDefinitions = current.ActionDefinitions.Select((action, index) => action with { NativeAction = NormalizeAction(action.NativeAction, saved.ActionDefinitions[index].NativeAction) }).ToArray(),
            NativeActions = current.NativeActions.Select((action, index) => NormalizeAction(action, saved.NativeActions[index])!).ToArray(),
            NativeGlobalMessages = current.NativeGlobalMessages.Select((message, index) => NormalizeText(message, saved.NativeGlobalMessages[index])).ToArray()
        };
    }

    private static NativeBattleGraphicsResource RetainModel(NativeBattleGraphicsResource current, NativeBattleGraphicsResource saved)
    {
        saved.Validate();
        if (current.BodyFileSelector129 != saved.BodyFileSelector129 || current.LinkedFileSelector128 != saved.LinkedFileSelector128 || current.ModelLoadSources is not { } selectedSources || saved.ModelLoadSources is not { } sources || selectedSources.Length != sources.Length)
        {
            throw new InvalidDataException("Active model resources differ from their equipped body or linked-file selection.");
        }
        for (int index = 0; index < sources.Length; index++)
        {
            NativeBattleModelDatSource selected = selectedSources[index];
            NativeBattleModelDatSource source = sources[index];
            if (selected.ModelId != source.ModelId || selected.Linked != source.Linked || selected.Selector != source.Selector || selected.FileId != source.FileId || selected.Name != source.Name || source.Bytes.IsDefault || source.Bytes.Length < 4)
            {
                throw new InvalidDataException("Active model resource lost its original DAT binding identity.");
            }
            int sections = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan());
            if (sections is < 1 or > 16)
            {
                throw new InvalidDataException("Active model DAT has an unbounded section directory.");
            }
            for (int section = 0; section < sections; section++)
            {
                _ = source.Section(section);
            }
        }
        // The source DAT, decoded pose and geometry stay pinned to the existing allocation.
        // Selecting another edition does not replace a running model's byte-backed views.
        return saved;
    }

    private static BattleCombatantDefinition NormalizeCombatant(BattleCombatantDefinition current, BattleCombatantDefinition saved)
    {
        BattleNativePlayerStatRules? rules = current.NativePlayerStatRules;
        if (rules is not null && saved.NativePlayerStatRules is { } prior)
        {
            rules.Validate();
            prior.Validate();
            rules = rules with
            {
                CharacterRecord = NormalizeRecord(rules.CharacterRecord, prior.CharacterRecord, 2),
                WeaponRecord = NormalizeRecord(rules.WeaponRecord, prior.WeaponRecord, 2),
                MagicRecords = NormalizeRecords(rules.MagicRecords, prior.MagicRecords, 4),
                PassiveRecords = NormalizeRecords(rules.PassiveRecords, prior.PassiveRecords, 4)
            };
        }
        return current with { NativeAttack = NormalizeAction(current.NativeAttack, saved.NativeAttack), NativePlayerStatRules = rules };
    }

    private static BattleNativeAction? NormalizeAction(BattleNativeAction? current, BattleNativeAction? saved)
    {
        if (current is null || saved is null)
        {
            return current;
        }
        // These widths are the leading text-offset WORDs in each typed kernel family.
        // Section10 starts with MagicId; section20 Blue Magic supplements start with statuses.
        int textBytes = current.KernelSection switch
        {
            1 or 2 or 5 or 7 or 18 or 19 or 21 or 22 => 4,
            3 or 4 or 9 or 25 => 2,
            _ => 0
        };
        int supplementalTextBytes = current.SupplementalKernelIndex is not null && (current.KernelSection == 10 || current.KernelSection == 4 && current.Command == 1) ? 4 : 0;
        return current with
        {
            Name = NormalizeText(current.Name, saved.Name),
            RawRecord = NormalizeRecord(current.RawRecord, saved.RawRecord, textBytes),
            SupplementalRecord = NormalizeRecord(current.SupplementalRecord, saved.SupplementalRecord, supplementalTextBytes)
        };
    }

    private static ImmutableArray<ImmutableArray<byte>> NormalizeRecords(ImmutableArray<ImmutableArray<byte>> current, ImmutableArray<ImmutableArray<byte>> saved, int textBytes)
    {
        if (current.IsDefault || saved.IsDefault || current.Length != saved.Length)
        {
            return current;
        }
        return current.Select((record, index) => NormalizeRecord(record, saved[index], textBytes)).ToImmutableArray();
    }

    private static ImmutableArray<byte> NormalizeRecord(ImmutableArray<byte> current, ImmutableArray<byte> saved, int textBytes)
    {
        if (textBytes == 0 || current.IsDefault || saved.IsDefault || current.Length != saved.Length || current.Length < textBytes)
        {
            return current;
        }
        byte[] result = current.ToArray();
        saved.AsSpan(0, textBytes).CopyTo(result);
        return result.ToImmutableArray();
    }

    private static string? NormalizeText(string? current, string? saved) => current is not null && saved is not null && current.Trim() == saved.Trim() ? saved : current;
}
