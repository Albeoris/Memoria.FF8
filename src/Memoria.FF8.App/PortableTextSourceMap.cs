using SkiaSharp;

namespace Memoria.FF8;

internal sealed record PortableTextWrappedLine(string Text, IReadOnlyList<int> SourcePositions);
internal sealed record PortableShapedCluster(int Start, int Length, SKRect Bounds);
internal sealed record PortableTextCluster(IReadOnlyList<int> SourcePositions, SKRect Bounds, int LineIndex = 0);
internal sealed record PortableTextRasterResult(SKBitmap Bitmap, IReadOnlyList<PortableTextCluster> Clusters);
