using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.Core.NsResources;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// The embedded catalog describing every slot with a reverse-engineered layout. Slots absent
/// from the catalog are treated as <see cref="MngrpSectionLayout.Opaque"/> and preserved verbatim.
/// </summary>
internal static class MngrpSectionCatalog
{
    private static readonly Lazy<FrozenDictionary<Int32, MngrpSectionDescriptor>> DescriptorsBySlot = new(Load);

    /// <summary>Slots that store the texts of a <see cref="MngrpSectionLayout.TextRecordList"/> section.</summary>
    private static readonly Lazy<FrozenDictionary<Int32, Int32>> RecordSlotsByTextSlot = new(
        () => DescriptorsBySlot.Value.Values
            .Where(descriptor => descriptor.TextSlot is not null)
            .ToFrozenDictionary(descriptor => descriptor.TextSlot!.Value, descriptor => descriptor.Slot));

    public static MngrpSectionDescriptor Get(Int32 slotIndex)
    {
        return DescriptorsBySlot.Value.TryGetValue(slotIndex, out MngrpSectionDescriptor? descriptor)
            ? descriptor
            : new MngrpSectionDescriptor { Slot = slotIndex, Layout = MngrpSectionLayout.Opaque };
    }

    /// <summary>
    /// Returns the record slot that owns <paramref name="slotIndex"/> as its text slot,
    /// or -1 when the slot is not a companion text slot.
    /// </summary>
    public static Int32 GetOwningRecordSlot(Int32 slotIndex)
    {
        return RecordSlotsByTextSlot.Value.TryGetValue(slotIndex, out Int32 recordSlot) ? recordSlot : -1;
    }

    private static FrozenDictionary<Int32, MngrpSectionDescriptor> Load()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        using Stream stream = EmbeddedResources.Open(typeof(MngrpSectionCatalog).Assembly, "/Resources/mngrp.sections.json");
        MngrpSectionDescriptor[] descriptors = JsonSerializer.Deserialize<MngrpSectionDescriptor[]>(stream, options)
            ?? throw new InvalidOperationException("Embedded resource 'mngrp.sections.json' could not be parsed.");

        return descriptors.ToFrozenDictionary(descriptor => descriptor.Slot);
    }
}

/// <summary>One catalog entry: a slot's layout and, for record sections, its companion text slot.</summary>
internal sealed class MngrpSectionDescriptor
{
    public Int32 Slot { get; init; }

    public MngrpSectionLayout Layout { get; init; } = MngrpSectionLayout.Opaque;

    public Int32? TextSlot { get; init; }
}
