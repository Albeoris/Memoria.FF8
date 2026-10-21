using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleActorDirectorProfile
{
    private NativeBattleActorDirectorProfile(ushort effect)
    {
        Effect = effect;
    }
    public ushort Effect { get; }
    public uint Entry => Callback(0x8AB140);
    public uint Initializer => Callback(0x8AB120);
    public bool UsesActorWaveMesh => Effect is 81 or 82;
    public uint InitializerGlobal => UsesActorWaveMesh ? WaveGlobal(0x25D21E0) : Global(0x27304A0);
    public uint SharedAddress => UsesActorWaveMesh ? WaveGlobal(0x25D48B0) : Global(0x2732B70);
    public uint CameraAddress => Effect switch { 16 => 0x162A48C, 61 => 0x1596ADC, 81 => 0x157B10C, _ => 0x157AEF4 };
    public int CameraLength => Effect switch { 16 => 0x8A0, 61 => 0x870, 81 => 0x1B8, _ => 0x1F4 };
    public string TextureName => Effect switch { 16 => "mag015.tim", 61 => "mag060.tim", 81 => "mag080.tim", _ => "mag081.tim" };
    public uint SourceAddress => Effect switch { 16 => 0x17F7F1C, 61 => 0x1795000, 81 => 0x1744A1C, _ => 0x1741028 };
    public int SourceLength => Effect switch { 16 => 0x4770, 61 => 0x2CC0, 81 => 0x5138, _ => 0x39F4 };
    public uint SoundAddress => Effect switch { 16 => 0x162A488, 61 => 0x1596AD8, 81 => 0x157B108, _ => 0x157AEF0 };
    public uint Sound => Effect switch { 16 => 0x494E7, 61 => 0x493F5, 81 => 0x668FE, _ => 0x66904 };
    public ushort GenericDelay => Effect == 82 ? (ushort)0 : (ushort)4;
    public short ContactAge => Effect == 81 ? (short)28 : (short)40;
    public short StageInitialAge => UsesActorWaveMesh ? (short)5 : (short)0;
    public short StageRamp => Effect == 82 ? (short)256 : (short)512;
    public short StageMaximum => Effect == 82 ? (short)1536 : (short)2048;
    public short StageHoldAge => Effect == 81 ? (short)35 : (short)40;
    public uint? OverlayAddress => Effect == 81 ? 0x157B2C4u : Effect == 82 ? 0x157B0E8u : null;
    public ushort? OverlayDelay => Effect == 81 ? (ushort)7 : Effect == 82 ? (ushort)24 : null;
    public uint? VibrationAddress => Effect == 81 ? 0x1749B54u : null;
    public int EmitterCapacity => 20;
    public int GeometryCapacity => UsesActorWaveMesh ? 60 : Effect == 16 ? 90 : 100;
    public int EmitterBytes => 0x870;
    public int GeometryBytes => GeometryCapacity * 672;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => UsesActorWaveMesh ?
    [
        new(WaveGlobal(0x25D23C0), WaveGlobal(0x25D22F8), 100, 2),
        new(WaveGlobal(0x25D4730), WaveGlobal(0x25D4740), 88, 4),
        new(WaveGlobal(0x25D22C8), WaveGlobal(0x25D21F0), 72, 3),
        new(WaveGlobal(0x25D4490), WaveGlobal(0x25D3F48), 676, 2),
        new(WaveGlobal(0x25D23D0), WaveGlobal(0x25D23E0), 140, 50),
        new(WaveGlobal(0x25D4720), WaveGlobal(0x25D44A0), 64, 10)
    ] :
    [
        new(Global(0x2730680), Global(0x27305B8), 100, 2),
        new(Global(0x27329F0), Global(0x2732A00), 88, 4),
        new(Global(0x2730588), Global(0x27304B0), 72, 3),
        new(Global(0x2732750), Global(0x2732208), 676, 2),
        new(Global(0x2730690), Global(0x27306A0), 140, 50),
        new(Global(0x27329E0), Global(0x2732760), 64, 10)
    ];

    public static NativeBattleActorDirectorProfile ForEffect(ushort effect) => effect is 16 or 61 or 81 or 82 ? new(effect) : throw new NotSupportedException("The actor director requires an implemented original profile.");

    public uint Callback(uint canonical16)
    {
        if (UsesActorWaveMesh)
        {
            uint actual81 = canonical16 switch { 0x8AB120 => 0x7A5190, 0x8AB140 => 0x7A51B0, 0x8AB290 => 0x7A5300, 0x8AB4C0 => 0x7A5530, 0x8AB600 => 0x7A5670, 0x8AB890 => 0x7A5940, 0x8B0660 => 0x7AB2B0, _ => throw new NotSupportedException("The actor wave callback requires an explicit original role.") };
            return Effect == 81 ? actual81 : actual81 - (canonical16 is 0x8AB890 or 0x8B0660 ? 0x63B0u : 0x6380u);
        }
        if (canonical16 is < 0x8AB120 or > 0x8B08E0)
        {
            throw new ArgumentOutOfRangeException(nameof(canonical16));
        }
        return Effect == 16 ? canonical16 : canonical16 - 0xC5880;
    }

    public uint CanonicalCallback(uint actual)
    {
        if (UsesActorWaveMesh)
        {
            foreach (uint candidate in new uint[] { 0x8AB120, 0x8AB140, 0x8AB290, 0x8AB4C0, 0x8AB600, 0x8AB890, 0x8B0660 })
            {
                if (Callback(candidate) == actual)
                {
                    return candidate;
                }
            }
            throw new NotSupportedException("The actor wave callback has no original module role.");
        }
        uint canonical = Effect == 16 ? actual : unchecked(actual + 0xC5880);
        _ = Callback(canonical);
        return canonical;
    }

    public uint Global(uint canonical16)
    {
        if (UsesActorWaveMesh)
        {
            throw new NotSupportedException("Actor wave globals require their own original identity.");
        }
        if (canonical16 is < 0x27304A0 or > 0x2732BEC)
        {
            throw new ArgumentOutOfRangeException(nameof(canonical16));
        }
        return Effect == 16 ? canonical16 : canonical16 - 0x12EB80;
    }

    private uint WaveGlobal(uint address81) => Effect == 81 ? address81 : Effect == 82 ? address81 - 0x2758 : throw new NotSupportedException("This actor director has no actor wave globals.");
}
