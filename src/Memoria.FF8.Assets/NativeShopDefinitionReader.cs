using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeShopDefinitionReader
{
    public static NativeShopHistory ReadNewGameHistory(byte[] initial)
    {
        if (initial.Length != 2812)
        {
            throw new InvalidDataException("Native shop history requires the complete original init.out.");
        }
        NativeShopHistory result = new(Convert.ToHexString(SHA256.HashData(initial)), ImmutableArray.CreateRange(initial.AsSpan(2304, 400).ToArray()));
        result.Validate();
        return result;
    }

    public static NativeShopDefinitions Read(byte[] executable, byte[] shops, byte[] prices, byte[] kernel)
    {
        return Read(new NativeExecutableMemory(executable), shops, prices, kernel);
    }

    public static NativeShopDefinitions Read(IGameNativeMemorySource memory, byte[] shops, byte[] prices, byte[] kernel)
    {
        string executableHash = memory.SourceHash;
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernel));
        if (!NativeFieldRuntimeProfile.IsSupportedImage(executableHash) || shops.Length != 640 || prices.Length != 796 || kernelHash is not ("E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6" or "6A9E9AA4B99B98AEC966D42537E9E119BF1BC1AF668CE8B791E7EAD00934397B"))
        {
            throw new NotSupportedException("Native shop definitions require the authenticated English executable/kernel and complete menu catalogs/prices.");
        }
        ImmutableArray<ImmutableArray<NativeShopCatalogEntry>>.Builder catalogs = ImmutableArray.CreateBuilder<ImmutableArray<NativeShopCatalogEntry>>(20);
        for (int row = 0; row < 20; row++)
        {
            catalogs.Add(Enumerable.Range(0, 16).Select(slot => new NativeShopCatalogEntry(shops[row * 32 + slot * 2], shops[row * 32 + slot * 2 + 1])).ToImmutableArray());
        }
        ImmutableArray<NativeShopPrice>.Builder definitions = ImmutableArray.CreateBuilder<NativeShopPrice>(199);
        for (int item = 0; item < 199; item++)
        {
            definitions.Add(new NativeShopPrice(BinaryPrimitives.ReadUInt16LittleEndian(prices.AsSpan(item * 4)), prices[item * 4 + 2], prices[item * 4 + 3], ReadItemName(kernel, item)));
        }
        //4EBBA0 maps the BYTE argument throughB88918. Retain the complete
        //physical BYTE-index read domain, not an inferred '&31' logical table.
        NativeShopDefinitions result = new(executableHash, Convert.ToHexString(SHA256.HashData(shops)), Convert.ToHexString(SHA256.HashData(prices)), kernelHash, ImmutableArray.CreateRange(memory.Read(0xB88918, 256)), catalogs.MoveToImmutable(), definitions.MoveToImmutable());
        result.Validate();
        return result;
    }

    private static string? ReadItemName(byte[] kernel, int item)
    {
        //Original47EA30: item<33 uses WORD1CF7778[12*id]/kernel38;
        //others use WORD1CF7A0C[2*id]/kernel39.47EA90 is description,
        //not the adjacent name pointer.
        int recordSection = item < 33 ? 7 : 8;
        int recordSize = item < 33 ? 24 : 4;
        int recordIndex = item < 33 ? item : item - 33;
        int recordStart = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + recordSection * 4));
        int recordEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8 + recordSection * 4));
        if (recordStart < 228 || recordEnd > kernel.Length || recordEnd - recordStart != recordSize * (item < 33 ? 33 : 166))
        {
            throw new InvalidDataException("Native shop item rows lie outside their kernel section.");
        }
        int pointer = checked(recordStart + recordSize * recordIndex);
        ushort relative = BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(pointer));
        if (relative == ushort.MaxValue)
        {
            return null;
        }
        int section = item < 33 ? 38 : 39;
        int start = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + section * 4));
        int end = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8 + section * 4));
        if (start < 228 || end <= start || end > kernel.Length)
        {
            throw new InvalidDataException("Native shop item text section bounds are invalid.");
        }
        int position = checked(start + relative);
        int zero = position < end ? Array.IndexOf(kernel, (byte)0, position, end - position) : -1;
        if (zero < 0)
        {
            throw new InvalidDataException("Native shop item name lies outside its original kernel text section.");
        }
        return FF8Encoding.CreateEuropean().GetString(kernel[position..zero]);
    }
}
