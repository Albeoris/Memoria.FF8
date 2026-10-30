using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>Read-only projection of the saved shop and input bytes addressed by signed native item IDs128..198.</summary>
public sealed class NativeItemOrderAliases
{
    private readonly byte?[] values;

    private NativeItemOrderAliases(NativeShopHistory? shops, NativeInputConfiguration? input)
    {
        values = new byte?[71];
        if (shops is not null)
        {
            shops.Validate();
            for (int index = 0; index < 61; index++)
            {
                values[index] = shops.Bytes[339 + index];
            }
        }
        if (input is not null)
        {
            input.Validate();
            values[61] = input.NativeBattleSpeedByte;
            values[62] = input.BattleMessageSpeed;
            values[63] = input.FieldMessageSpeedByte;
            values[64] = input.SoundVolume;
            byte[] options = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(options, input.Options);
            for (int index = 0; index < 4; index++)
            {
                values[65 + index] = options[index];
            }
            values[69] = input.ButtonMap[0];
            values[70] = input.ButtonMap[1];
        }
    }

    public static NativeItemOrderAliases From(GameState state) => new(state.ShopHistory, state.InputConfiguration);
    public static NativeItemOrderAliases From(IEventServices services) => new(services.ShopHistory, services.InputConfiguration);

    public byte Read(int itemId)
    {
        if (itemId is < 128 or > NativeEconomy.ItemSlotCount)
        {
            throw new NotSupportedException("Native item-order aliases require a valid original item ID128..198.");
        }
        return values[itemId - 128] ?? throw new NotSupportedException("Native item ordering requires the current canonical shop or input byte reached by its signed item ID.");
    }
}
