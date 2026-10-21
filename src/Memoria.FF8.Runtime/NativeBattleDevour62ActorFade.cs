namespace Memoria.FF8.Runtime;

public static class NativeBattleDevour62ActorFade
{
    public static NativeBattleGraphicsModel Apply(NativeBattleGraphicsModel model, short value, uint processColor40)
    {
        if ((model.Flags0 & 2) == 0)
        {
            return model;
        }
        ushort flags = (ushort)(model.Flags0 | 0x800);
        if (value == 0)
        {
            return model with { Flags0 = flags, Color40 = processColor40 & 0xFFFFFF, Alpha7 = 0 };
        }
        uint previous = model.Color40 ?? throw new NotSupportedException("Devour requires the retained actor color code byte.");
        uint color = previous & 0xFF000000;
        for (int shift = 0; shift < 24; shift += 8)
        {
            int channel = (byte)(processColor40 >> shift);
            color |= (uint)unchecked((byte)(channel - channel * value / 4096)) << shift;
        }
        return model with { Flags0 = flags, Color40 = color };
    }

    public static ushort RestoreFlags(ushort current, ushort saved) => (ushort)(current ^ ((saved ^ current) & 0x804));
}
