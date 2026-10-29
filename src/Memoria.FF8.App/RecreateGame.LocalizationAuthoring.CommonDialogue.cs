using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    internal sealed record GeneratedAuthoringDialogue(LocalizationTextBinding Binding, Func<string, string> FormatTemplate)
    {
        public bool DynamicContribution { get; init; }
        public DialogueState Project(DialogueState native)
        {
            if (Binding.Template == Binding.Source && !DynamicContribution)
            {
                return native;
            }
            string text = FormatTemplate(Binding.Template);
            return text == native.Text ? native : native with { Text = text };
        }
    }

    private bool TryGeneratedAuthoringDialogue(DialogueState native, out GeneratedAuthoringDialogue? presentation)
    {
        presentation = null;
        EventEngine? engine = events;
        if (resourceHost is null || native.IsNamePrompt || native.MessageId != -1 || native.Channel != 6 || engine?.Scene.DrawPointInteraction is not NativeDrawPointInteraction interaction)
        {
            return false;
        }
        EventEngine owner = engine!;
        if (owner.FieldId != interaction.FieldId || native.ObjectId != interaction.ObjectId || native.ScriptId != interaction.ScriptId || native.Operation != interaction.Operation)
        {
            return false;
        }
        NativeDrawPointCatalog catalog = owner.ReadNativeDrawPointCatalog?.Invoke() ?? throw new NotSupportedException("Generated DRAWPOINT presentation requires its authenticated common-message provider.");
        int magic = interaction.PointByte & 63;
        NativeDrawPointCatalog visualNames = catalog;
        bool dynamicContribution = false;
        if (catalog.Message(interaction.Message).Contains("{Name 32}", StringComparison.Ordinal))
        {
            string magicKey = LocalizationSourceExporter.IndexedKey(resourceHost.Resources.Source.Language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), magic);
            string name = resourceHost.Resources.ResolveText(magicKey, catalog.MagicNames[magic]).Text;
            visualNames = catalog with { MagicNames = catalog.MagicNames.SetItem(magic, name) };
            dynamicContribution = name != catalog.MagicNames[magic];
        }
        presentation = CreateGeneratedDrawPointPresentation(interaction, visualNames, session.CharacterVitals, owner.Scene.Party.ToArray(), resourceHost.Resources.Source.Language, resourceHost.Resources.ResolveText, owner.MessageVariables?.At(7));
        presentation = presentation with
        {
            DynamicContribution = dynamicContribution,
            Binding = presentation.Binding with
            {
                Names = new Dictionary<int, string>(owner.Scene.Names),
                MessageVariables = Enumerable.Range(0, 8).ToDictionary(slot => slot, slot => owner.MessageVariables?.At(slot))
            }
        };
        return true;
    }

    /// <summary>Captures presentation inputs without replacing the engine's catalog, party, vitals or interaction owner.</summary>
    internal static GeneratedAuthoringDialogue CreateGeneratedDrawPointPresentation(NativeDrawPointInteraction interaction, NativeDrawPointCatalog catalog, NativeCharacterVitals? vitals, IReadOnlyList<int> party, string language, Func<string, string, BoundLocalizationText> resolve, int? quantity = null)
    {
        catalog.ValidateInteractionResources();
        if (interaction.Message is < 0 or > 7 || interaction.CommonMessageHash != catalog.CommonMessageHash)
        {
            throw new InvalidDataException("Generated DRAWPOINT presentation has a different common-message identity or source bank.");
        }
        int magic = interaction.PointByte & 63;
        if (magic >= catalog.MagicNames.Length)
        {
            throw new NotSupportedException("Generated DRAWPOINT presentation requires a supported native magic identity.");
        }
        int[] frozenParty = party.ToArray();
        NativeCharacterVitals? frozenVitals = interaction.Message == 4 ? vitals?.DeepCopy() ?? throw new NotSupportedException("Draw selection preview requires captured native character vitals.") : null;
        if (frozenVitals is not null && frozenVitals.KernelSha256 != catalog.KernelHash)
        {
            throw new InvalidDataException("Draw selection preview has different native kernel provenance.");
        }
        int character = 0;
        if (interaction.Selection is int selected)
        {
            if (selected < 0 || selected >= frozenParty.Length || frozenParty[selected] == 255)
            {
                throw new InvalidDataException("Generated DRAWPOINT presentation has no captured field-party selection identity.");
            }
            character = frozenParty[selected];
        }
        string source = catalog.Message(interaction.Message);
        string key = LocalizationSourceExporter.IndexedKey(language, "drawpoints_world", interaction.Message);
        BoundLocalizationText resolved = resolve(key, source);
        string Format(string candidate)
        {
            string template = string.IsNullOrEmpty(candidate) ? source : candidate;
            ValidateGeneratedDrawPointTemplate(interaction.Message, source, template, key);
            NativeDrawPointCatalog visual = catalog with { CommonMessages = catalog.CommonMessages.SetItem(interaction.Message, template), NoEligibleMessage = interaction.Message == 7 ? template : catalog.NoEligibleMessage };
            return interaction.Message == 4
                ? visual.FormatSelection(frozenParty, frozenVitals!, magic, interaction.EligibleCombatSlots)
                : visual.FormatMessage(interaction.Message, magic, character, quantity);
        }
        string formatted = Format(resolved.Binding.Template);
        LocalizationTextBinding binding = resolved.Binding with
        {
            Category = "Draw points",
            File = "drawpoints_world",
            Source = source,
            Template = resolved.Binding.Template,
            Text = formatted,
            BaseKey = key,
            Operation = interaction.Operation,
            ResourceFingerprint = catalog.MessageHash(interaction.Message),
            Arguments = [magic, catalog.MagicNames[magic], character, quantity]
        };
        return new(binding, Format);
    }

    internal static void ValidateGeneratedDrawPointTemplate(int message, string source, string target, string key)
    {
        LocalizationTextValidation.Validate(new() { Category = "Draw points", File = "drawpoints_world", Key = key, Source = source, Target = target });
        if (string.IsNullOrEmpty(target) || message != 4)
        {
            return;
        }
        string[] original = source.Split("{Line}", StringSplitOptions.None);
        string[] translated = target.Split("{Line}", StringSplitOptions.None);
        if (original.Length != 5 || translated.Length != 5)
        {
            throw new InvalidDataException("Draw selection Target requires its header, Cancel and three physical party rows in five lines.");
        }
        for (int row = 0; row < translated.Length; row++)
        {
            LocalizationTextValidation.Validate(new() { Key = key, Source = original[row], Target = translated[row] });
            if (row < 2)
            {
                continue;
            }
            int character = translated[row].IndexOf("{Char Squall}", StringComparison.Ordinal);
            int end = character < 0 ? -1 : character + "{Char Squall}".Length;
            if (end < 0 || translated[row].Length < end + 3 || translated[row][end + 1] != ' ' || translated[row][end + 2] != ' ' || original[row].Contains("{Color Disabled}", StringComparison.Ordinal) && !translated[row].Contains("{Color Disabled}", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Draw selection Target must retain each native character/capacity control and junction-marker padding.");
            }
        }
    }
}
