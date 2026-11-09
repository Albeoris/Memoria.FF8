namespace Memoria.FF7.Formats;

public sealed record PcRenderState(uint Options, uint Features, uint ShadeMode, uint AmbientLight, uint SourceBlend, uint DestinationBlend, uint AlphaReference, uint BlendMode, uint ZSort, uint VertexAlpha, ReadOnlyMemory<byte> Raw)
{
    public static PcRenderState Read(ReadOnlySpan<byte> source)
    {
        if (source.Length != 100)
        {
            throw new InvalidDataException("FF7 P render state requires exactly 100 bytes.");
        }
        BinaryCursor cursor = new(source);
        cursor.Seek(8);
        uint options = cursor.U32();
        uint features = cursor.U32();
        cursor.Seek(36);
        uint shade = cursor.U32();
        uint ambient = cursor.U32();
        cursor.Seek(52);
        uint sourceBlend = cursor.U32();
        uint destinationBlend = cursor.U32();
        cursor.Seek(64);
        uint alpha = cursor.U32();
        uint blend = cursor.U32();
        uint z = cursor.U32();
        cursor.Seek(92);
        return new PcRenderState(options, features, shade, ambient, sourceBlend, destinationBlend, alpha, blend, z, cursor.U32(), source.ToArray());
    }
}
