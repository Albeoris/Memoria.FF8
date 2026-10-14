using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeEconomyInitialization
{
    public static NativeEconomy Read(byte[] initial)
    {
        if (initial.Length != 2812)
        {
            throw new InvalidDataException("Native economy initialization requires the complete original init.out layout.");
        }
        NativeItemStock[] items = new NativeItemStock[NativeEconomy.ItemSlotCount];
        for (int slot = 0; slot < items.Length; slot++)
        {
            int offset = 2804 + 2 * slot;
            // Executed56D9F0 clears the entire save before56DA10 reads2812 template bytes.
            items[slot] = offset < initial.Length ? new NativeItemStock(initial[offset], initial[offset + 1]) : new NativeItemStock(0, 0);
        }
        NativeEconomy state = new()
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = Convert.ToHexString(SHA256.HashData(initial)),
            Items = items,
            Gil = BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(2748)),
            PastGil = BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(2752)),
            // Executed52CA90(1) clears248 bytes at the separate native event-bank base1CFE9B8.
            EventBankGil72 = 0,
            EventBankPastGil76 = 0
        };
        state.Validate();
        return state;
    }
}
