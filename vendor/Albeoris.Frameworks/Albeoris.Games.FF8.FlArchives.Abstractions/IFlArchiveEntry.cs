namespace Albeoris.Games.FF8.FlArchives.Abstractions;

public interface IFlArchiveEntry
{
    String RelativePath { get; }
    UInt32 Offset { get; }
    UInt32 Size { get; }

    Stream OpenForRead();
    Stream OpenForWrite(UInt32 desiredSize);
}