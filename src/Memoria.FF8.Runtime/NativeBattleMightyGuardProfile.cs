using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleMightyGuardProfile
{
    private NativeBattleMightyGuardProfile(ushort effect)
    {
        Effect = effect;
    }

    public ushort Effect { get; }
    public bool ScalesToTarget => Effect == 78;
    public uint Initializer => Effect == 78 ? 0x7BE2B0u : 0x7CBD90u;
    public uint Entry => Initializer + 0x20;
    public uint InitializerGlobal => Effect == 78 ? 0x25EB530u : 0x25F3F60u;
    public uint MainCallback => Effect == 78 ? 0x7BE400u : 0x7CBEE0u;
    public uint TargetCallback => Effect == 78 ? 0x7BE710u : 0x7CC220u;
    public uint GenericCallback => Effect == 78 ? 0x7BE820u : 0x7CC330u;
    public uint FadeInCallback => Effect == 78 ? 0x7BE5F0u : 0x7CC100u;
    public uint FadeOutCallback => Effect == 78 ? 0x7C4310u : 0x7D1D30u;
    public uint SourceAddress => Effect == 78 ? 0x176E194u : 0x178557Cu;
    public int SourceLength => 0x3DC8;
    public int DrawBankBytes => 0x9000;
    public int EmitterBytes => 0xEC4;
    public int GeometryBytes => 0x10680;
    public int EmitterCapacity => 35;
    public int GeometryCapacity => 100;
    public ushort GenericDelay => Effect == 78 ? (ushort)0 : (ushort)12;
    public short SoundAge => Effect == 78 ? (short)10 : (short)16;
    public short ContactAge => Effect == 78 ? (short)40 : (short)45;
    public ushort RootWait => Effect == 78 ? (ushort)30 : (ushort)35;
    public uint CameraAddress => Effect == 78 ? 0x1581F98u : 0x1589AC0u;
    public int CameraLength => Effect == 78 ? 0xBC : 0x158;
    public uint FilenameAddress => CameraAddress + (uint)CameraLength;
    public string TextureName => "mag072-077.tim";
    public uint SoundAddress => Effect == 78 ? 0x1581F94u : 0x1587F48u;
    public uint Sound => Effect == 78 ? 0x642B3u : 0x557B4u;
    public uint? CasterDatAddress => Effect == 73 ? 0x1587F4Cu : null;
    public int CasterDatLength => Effect == 73 ? 0x1B74 : 0;
    public uint? SavedCasterBank => Effect == 73 ? 0x25F4F38u : null;
    public ImmutableArray<NativeBattleCureFamilyPool> Pools => Effect == 78
        ? [new(0x25EB620, 0x25EB558, 100, 2), new(0x25EC370, 0x25EC380, 88, 4), new(0x25EC0D0, 0x25EB640, 676, 4), new(0x25EC360, 0x25EC0E0, 64, 10)]
        : [new(0x25F4050, 0x25F3F88, 100, 2), new(0x25F4DA0, 0x25F4DB0, 88, 4), new(0x25F4B00, 0x25F4070, 676, 4), new(0x25F4D90, 0x25F4B10, 64, 10)];

    public static NativeBattleMightyGuardProfile ForEffect(ushort effect) => effect is 73 or 78 ? new(effect) : throw new NotSupportedException("Mighty Guard requires its original Blue Magic or enemy module.");
}
