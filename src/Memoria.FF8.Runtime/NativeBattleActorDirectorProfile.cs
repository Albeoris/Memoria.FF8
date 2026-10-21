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
    public uint InitializerGlobal => Global(0x27304A0);
    public uint SharedAddress => Global(0x2732B70);
    public uint CameraAddress => Effect == 16 ? 0x162A48Cu : 0x1596ADCu;
    public int CameraLength => Effect == 16 ? 0x8A0 : 0x870;
    public string TextureName => Effect == 16 ? "mag015.tim" : "mag060.tim";
    public uint SourceAddress => Effect == 16 ? 0x17F7F1Cu : 0x1795000u;
    public int SourceLength => Effect == 16 ? 0x4770 : 0x2CC0;
    public uint SoundAddress => Effect == 16 ? 0x162A488u : 0x1596AD8u;
    public uint Sound => Effect == 16 ? 0x494E7u : 0x493F5u;
    public int EmitterCapacity => 20;
    public int GeometryCapacity => Effect == 16 ? 90 : 100;
    public int EmitterBytes => 0x870;
    public int GeometryBytes => GeometryCapacity * 672;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools =>
    [
        new(Global(0x2730680), Global(0x27305B8), 100, 2),
        new(Global(0x27329F0), Global(0x2732A00), 88, 4),
        new(Global(0x2730588), Global(0x27304B0), 72, 3),
        new(Global(0x2732750), Global(0x2732208), 676, 2),
        new(Global(0x2730690), Global(0x27306A0), 140, 50),
        new(Global(0x27329E0), Global(0x2732760), 64, 10)
    ];

    public static NativeBattleActorDirectorProfile ForEffect(ushort effect) => effect is 16 or 61 ? new(effect) : throw new NotSupportedException("The actor director requires an implemented original profile.");

    public uint Callback(uint canonical16)
    {
        if (canonical16 is < 0x8AB120 or > 0x8B08E0)
        {
            throw new ArgumentOutOfRangeException(nameof(canonical16));
        }
        return Effect == 16 ? canonical16 : canonical16 - 0xC5880;
    }

    public uint CanonicalCallback(uint actual)
    {
        uint canonical = Effect == 16 ? actual : unchecked(actual + 0xC5880);
        _ = Callback(canonical);
        return canonical;
    }

    public uint Global(uint canonical16)
    {
        if (canonical16 is < 0x27304A0 or > 0x2732BEC)
        {
            throw new ArgumentOutOfRangeException(nameof(canonical16));
        }
        return Effect == 16 ? canonical16 : canonical16 - 0x12EB80;
    }
}
