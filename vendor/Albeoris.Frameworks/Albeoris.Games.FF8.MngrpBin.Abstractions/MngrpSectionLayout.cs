namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>The binary layout of a section stored in a <c>mngrp.bin</c> slot.</summary>
public enum MngrpSectionLayout
{
    /// <summary>Layout not (yet) reverse-engineered; the section is an opaque byte blob.</summary>
    Opaque,

    /// <summary>A 16-bit count followed by 16-bit offsets, each addressing a NUL-terminated string.</summary>
    StringTable,

    /// <summary>A 16-bit count followed by 16-bit offsets, each addressing a nested string table.</summary>
    StringTableGroup,

    /// <summary>A sequence of length-prefixed blocks, each carrying link ids and inline NUL-terminated texts.</summary>
    TextBlockList,

    /// <summary>A 32-bit count followed by (block offset, list number) pairs referencing text blocks.</summary>
    TextBlockMap,

    /// <summary>Fixed eight-byte records whose texts live in a companion slot.</summary>
    TextRecordList,
}
