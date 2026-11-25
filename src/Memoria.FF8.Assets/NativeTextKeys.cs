using Albeoris.Games.FF8.TextEncoding.Tags;

namespace Memoria.FF8.Assets;

public static class NativeTextKeys
{
    public static bool IsSupported(TextTagKey key) => HostButtonMask(key) != 0 || Icon(key).HasValue;

    public static ushort HostButtonMask(TextTagKey key)
    {
        byte code = (byte)key;
        ushort buttons = code is >= 0x20 and <= 0x3F ? (ushort)(1 << (code & 0x0F)) : (ushort)0;
        return (buttons & 0x0600) == 0 ? buttons : (ushort)0;
    }

    public static int? Icon(TextTagKey key)
    {
        // The original text tag 5 resolves these operands through its ICON.SP1 table.
        return key switch
        {
            TextTagKey.DefaultKey => 136,
            (TextTagKey)64 => 7,
            TextTagKey.JunctionMarker => 192,
            (TextTagKey)66 => 6,
            (TextTagKey)67 => 175,
            >= TextTagKey.JunctionAbility and <= TextTagKey.MenuAbility => 216 + (int)key - (int)TextTagKey.JunctionAbility,
            >= TextTagKey.FireElement and <= TextTagKey.HolyElement => 288 + (int)key - (int)TextTagKey.FireElement,
            >= TextTagKey.DeathStatus and <= TextTagKey.DrainStatus => 272 + (int)key - (int)TextTagKey.DeathStatus,
            _ => null
        };
    }
}
