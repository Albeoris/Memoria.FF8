namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// The fixed number of sections in the kernel.bin format. The format always defines the same
/// 56 sections in the same order: 30 fixed-size record arrays, a raw offset table used by the
/// miscellaneous text section, and 25 text blobs (one per record array that has display text,
/// plus one for the miscellaneous offset table).
/// </summary>
internal static class KernelSections
{
    public const Int32 Count = 56;
}
