using System.Collections.Immutable;
using System.Globalization;

namespace Memoria.FF8.Runtime;

/// <summary>Ephemeral presentation identity and arguments; never a saved gameplay owner.</summary>
public sealed record ChocoboPresentationText
{
    public string SemanticId { get; }
    public string Template { get; }
    public ImmutableArray<object?> Arguments { get; }

    public ChocoboPresentationText(string semanticId, string template, params object?[] arguments)
    {
        SemanticId = semanticId;
        Template = template;
        Arguments = arguments.ToImmutableArray();
    }

    private const string ExceptionDataKey = "Memoria.Chocobo.Presentation";
    public string Fallback => Format(null);

    public string Format(Func<string, string, string>? localize)
    {
        string template = localize?.Invoke(SemanticId, Template) ?? Template;
        object?[] values = Arguments.Select(argument => argument switch
        {
            ChocoboPresentationText nested => nested.Format(localize),
            ChocoboPresentationList list => string.Join(localize?.Invoke("chocobo.list.separator", ", ") ?? ", ", list.Items.Select(item => item.Format(localize))),
            Func<ChocoboPresentationText> current => current().Format(localize),
            _ => argument
        }).ToArray();
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, values);
        }
        catch (FormatException) when (!StringComparer.Ordinal.Equals(template, Template))
        {
            return string.Format(CultureInfo.CurrentCulture, Template, values);
        }
    }

    public InvalidDataException InvalidData() => Attach(new InvalidDataException(Fallback));
    public InvalidOperationException InvalidOperation() => Attach(new InvalidOperationException(Fallback));
    public NotSupportedException NotSupported() => Attach(new NotSupportedException(Fallback));

    private T Attach<T>(T exception) where T : Exception
    {
        exception.Data[ExceptionDataKey] = this;
        return exception;
    }

    public static ChocoboPresentationText FromException(Exception exception) => exception.Data[ExceptionDataKey] as ChocoboPresentationText ?? ChocoboText.Create("chocobo.literal", exception.Message);
}

public sealed record ChocoboPresentationList(IReadOnlyList<ChocoboPresentationText> Items);

public static class ChocoboText
{
    public static IReadOnlyDictionary<string, string> Templates { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["chocobo.literal"] = "{0}",
        ["chocobo.list.separator"] = ", ",
        ["chocobo.header.separator"] = " · ",
        ["chocobo.source.pc"] = "Classic / PC · {0} · {1}",
        ["chocobo.header.container"] = "{0} · {1}",
        ["chocobo.header.name"] = "Saved name: {0} · ID {1:D4} · Level {2} · {3}",
        ["chocobo.header.bank"] = "Selected bank {0} · Revision {1} · Backup {2}",
        ["chocobo.header.world"] = "Source world {0:X8} / Current world {1:X8} · {2}",
        ["chocobo.header.level"] = "Level {0} → {1}",
        ["chocobo.header.attack"] = "Attack tier {0} → {1}",
        ["chocobo.header.transfer-rule"] = "Transfers use the installed item table. Source files remain unchanged.",
        ["chocobo.header.counter"] = "{0} {1} / previously imported {2}",
        ["chocobo.header.counters"] = "{0} · {1} · {2} · {3}",
        ["chocobo.backup.none"] = "none",
        ["chocobo.name.default"] = "Boko",
        ["chocobo.association.reviewed"] = "Association reviewed",
        ["chocobo.association.required"] = "Association review required",
        ["chocobo.association.compatible"] = "Compatible / first association",
        ["chocobo.attack.0"] = "ChocoFire",
        ["chocobo.attack.1"] = "ChocoFlare",
        ["chocobo.attack.2"] = "ChocoMeteor",
        ["chocobo.attack.3"] = "ChocoBocle",
        ["chocobo.item.fallback"] = "Item {0}",
        ["chocobo.item.ordinary"] = "Ordinary",
        ["chocobo.item.exclusive"] = "Exclusive",
        ["chocobo.item.class"] = "Class {0}",
        ["chocobo.item.minimog"] = "MiniMog",
        ["chocobo.item.acquired"] = "Already acquired",
        ["chocobo.item.incoming"] = "Incoming",
        ["chocobo.item.deferred"] = "Deferred",
        ["chocobo.item.summary"] = "{0} · {1} · {2} · {3}",
        ["chocobo.item.capacity"] = "{0} · Grant {1} · Overflow {2}",
        ["chocobo.claim.capacity-review"] = "Claim blocked: the {0} remaining items below need inventory space; {1} slots are empty. Free space for the complete claim.",
        ["chocobo.import.mode-required"] = "Select Native Import mode before transferring rewards.",
        ["chocobo.import.battle-active"] = "Import is unavailable until the active or pending battle ends.",
        ["chocobo.import.boko-locked"] = "Unlock Boko in a Chocobo Forest before transferring rewards.",
        ["chocobo.import.state-required"] = "The saved inventory and menu random state are required for a transfer.",
        ["chocobo.import.association-required"] = "This source belongs to another Chocobo World. Review the association before importing it.",
        ["chocobo.import.counters-stale"] = "These counters precede an imported transfer or were reset. A new transfer cycle cannot be established safely; no repeatable loot was imported.",
        ["chocobo.import.revision-stale"] = "The source counters changed without a new native save revision. Refresh with a newer save.",
        ["chocobo.import.capacity"] = "Deferred due to capacity. The complete transfer must fit; inventory, random state and import counters remain unchanged.",
        ["chocobo.import.no-rewards"] = "No new rewards.",
        ["chocobo.import.progression"] = "Boko progression and transfer checkpoint updated. No new items.",
        ["chocobo.import.items-added"] = "Added {0} item(s). Boko progression and transfer checkpoint updated.",
        ["chocobo.import.source-changed"] = "The source or game state changed. Refresh the import preview.",
        ["chocobo.import.refresh-required"] = "Refresh the import preview first.",
        ["chocobo.claim.mode-required"] = "Select Memoria Chocobo World to claim these rewards.",
        ["chocobo.claim.battle-active"] = "Rewards cannot be claimed during a battle.",
        ["chocobo.claim.inventory-required"] = "Inventory is not initialized.",
        ["chocobo.claim.complete"] = "All exclusive items have already been obtained.",
        ["chocobo.claim.capacity"] = "No room for {0}; no rewards were claimed.",
        ["chocobo.claim.received"] = "Received the remaining exclusive items.",
        ["chocobo.reward.ribbon.name"] = "Ribbon",
        ["chocobo.reward.ribbon.description"] = "Teaches a Guardian Force Ribbon.",
        ["chocobo.reward.mogs_amulet.name"] = "Mog's Amulet",
        ["chocobo.reward.mogs_amulet.description"] = "Teaches a Guardian Force MiniMog.",
        ["chocobo.reward.friendship.name"] = "Friendship",
        ["chocobo.reward.friendship.description"] = "Calls a Moomba in battle.",
        ["chocobo.reward.chocobos_tag.name"] = "Chocobo's Tag",
        ["chocobo.reward.chocobos_tag.description"] = "Renames Boko.",
        ["chocobo.boundary.battle"] = "Chocobo World changes are unavailable until the active or pending battle ends.",
        ["chocobo.boundary.save"] = "Close the save screen before changing Chocobo World mode.",
        ["chocobo.boundary.settings"] = "Resolve the pending gameplay settings review before changing Chocobo World mode.",
        ["chocobo.format.pc-uncompressed"] = "PC uncompressed",
        ["chocobo.format.pc-lzss"] = "PC LZSS",
        ["chocobo.format.mcs"] = "PlayStation MCS",
        ["chocobo.format.raw"] = "PocketStation raw application",
        ["chocobo.error.file-too-large"] = "The selected file is too large for a supported standalone Chocobo World save.",
        ["chocobo.error.save-header"] = "Chocobo World save header is invalid or its write was interrupted.",
        ["chocobo.error.container"] = "This Chocobo World container has not been identified. Use a native PC save or a Chocobo World MCS export.",
        ["chocobo.error.application-header"] = "Chocobo World MCS application header is invalid.",
        ["chocobo.error.no-valid-bank"] = "No structurally valid Chocobo World save bank is available.",
        ["chocobo.error.literal-truncated"] = "Chocobo World LZSS literal is truncated.",
        ["chocobo.error.reference-truncated"] = "Chocobo World LZSS reference is truncated.",
        ["chocobo.error.reference-exceeds"] = "Chocobo World LZSS reference exceeds the world image.",
        ["chocobo.error.decoded-size"] = "Chocobo World LZSS must contain exactly one complete world image.",
        ["chocobo.error.table-invalid"] = "Chocobo World transfer table requires 128 complete native item rows.",
        ["chocobo.error.state-incomplete"] = "The decoded Chocobo World state is incomplete.",
        ["chocobo.error.ledger-unsupported"] = "Unsupported Chocobo reward ledger.",
        ["chocobo.error.receipt-invalid"] = "Invalid Chocobo World transfer receipt.",
        ["chocobo.error.inventory-slots"] = "A reward grant requires the native inventory slots.",
        ["chocobo.error.grant-quantity"] = "A reward grant requires a nonempty native item and a quantity from one to one hundred.",
        ["chocobo.error.ledger-invalid"] = "The saved Chocobo reward ledger is invalid.",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static ChocoboPresentationText Create(string semanticId, params object?[] arguments) => new(semanticId, Templates[semanticId], arguments);
}
