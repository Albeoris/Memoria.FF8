namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>One section of a <c>mngrp.bin</c> file, addressed by its fixed header slot.</summary>
public interface IMngrpSection
{
    /// <summary>The section's fixed position in the 256-slot <c>mngrphd.bin</c> table.</summary>
    Int32 SlotIndex { get; }

    /// <summary>The binary layout this section is parsed and serialized with.</summary>
    MngrpSectionLayout Layout { get; }

    /// <summary>
    /// The sector-aligned size the section's slot occupied when the archive was read. Sections
    /// are never written smaller than this, so a file whose packer over-allocated a slot still
    /// round-trips byte-for-byte; a section that outgrows it simply takes the sectors it needs.
    /// Zero means "no reservation".
    /// </summary>
    Int32 ReservedSize { get; set; }
}
