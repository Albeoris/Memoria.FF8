using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleAquaProfile
{
    private NativeBattleAquaProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public uint Entry => Callback(0x808820);
    public uint Initializer => Callback(0x808800);
    public uint InitializerGlobal => Global(0x2610FA8);
    public uint SharedAddress => Effect == 55 ? 0x26163B0u : 0x25FA370u;
    public uint MeshAddress => Effect == 55 ? 0x15A4168u : 0x158A4BCu;
    public uint WaveVerticesAddress => Effect == 55 ? 0x15A6AD0u : 0x158CE24u;
    public uint AnimationAddress => Effect == 55 ? 0x15A7498u : 0x158D7ECu;
    public uint SoundAddress => Effect == 55 ? 0x15A38D8u : 0x1589C2Cu;
    public short ContactThreshold => Effect == 55 ? (short)50 : (short)35;
    public byte AnchorSelector => Effect == 55 ? (byte)24 : (byte)240;
    public short AnchorFraction => Effect == 55 ? (short)4096 : (short)0;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools =>
    [
        new(Global(0x2611180), Global(0x26110B8), 100, 2),
        new(Global(0x2615838), Global(0x2615848), 88, 4),
        new(Global(0x2611088), Global(0x2610FB0), 72, 3),
        new(Global(0x2611190), Global(0x26111A0), 340, 50),
        new(Global(0x2615410), Global(0x2615420), 96, 4),
        new(Global(0x2615828), Global(0x26155A8), 64, 10)
    ];

    public static NativeBattleAquaProfile ForEffect(ushort effect)
    {
        return effect is 55 or 72 ? new(effect) : throw new NotSupportedException("The Aqua family requires its original enemy or Blue Magic module.");
    }

    public uint Callback(uint canonical55)
    {
        if (canonical55 < 0x808800 || canonical55 > 0x80CA80)
        {
            throw new ArgumentOutOfRangeException(nameof(canonical55));
        }
        return Effect == 55 ? canonical55 : canonical55 - (canonical55 < 0x808ED0 ? 0x369A0u : 0x36960u);
    }

    public uint CanonicalCallback(uint actual)
    {
        uint canonical = Effect == 55 ? actual : actual + (actual < 0x7D2570 ? 0x369A0u : 0x36960u);
        if (Callback(canonical) != actual)
        {
            throw new InvalidDataException("The Aqua callback belongs to another module.");
        }
        return canonical;
    }

    public uint SpriteAddress(uint canonical55)
    {
        if (canonical55 is not (0x15A38DC or 0x15A3A3C or 0x15A3B9C or 0x15A3CFC or 0x15A3E5C or 0x15A4008))
        {
            throw new ArgumentOutOfRangeException(nameof(canonical55));
        }
        return Effect == 55 ? canonical55 : canonical55 - 0x19CAC;
    }

    public uint Global(uint canonical55)
    {
        if (canonical55 is < 0x2610FA8 or > 0x2616410)
        {
            return canonical55 switch
            {
                0x2616414 => Effect == 55 ? canonical55 : 0x25FA3F8,
                0x2616418 => Effect == 55 ? canonical55 : 0x25FA3FC,
                _ => throw new ArgumentOutOfRangeException(nameof(canonical55))
            };
        }
        return Effect == 55 ? canonical55 : canonical55 - 0x1C040;
    }
}
