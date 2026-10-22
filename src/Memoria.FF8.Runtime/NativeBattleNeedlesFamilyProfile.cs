namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesFamilyProfile
{
    private NativeBattleNeedlesFamilyProfile(ushort effect) => Effect = effect;
    public ushort Effect { get; }
    private uint Global(uint address) => Effect == 177 ? address : address - 0x26C38;
    public uint BaseAddress => Global(0x2341948);
    public uint RootBank => Global(0x2341950);
    public uint RootDescriptor => Global(0x2341960);
    public uint NeedleBank => Global(0x2341970);
    public uint SparkBank => Global(0x2345B10);
    public uint ChildDescriptor => Global(0x2347730);
    public uint ChildBank => Global(0x2347740);
    public uint ParametersGlobal => Global(0x2348550);
    public uint CasterGlobal => Global(0x2348554);
    public uint InitializerGlobal => Global(0x2348558);
    public uint PacketBase => Global(0x234855C);
    public uint CursorGlobal => Global(0x236855C);
    public uint MatrixAddress => Global(0x2368560);
    public uint EndAddress => Global(0x2368580);
    public int Length => checked((int)(EndAddress - BaseAddress));
    public const int PacketBytes = 0x20000;
    public const int PacketStep = 0x10000;
    public const int RecordCount = 300;
    public uint Entry => Effect == 177 ? 0x5CF760u : 0x5CEDD0u;
    public uint Initializer => Effect == 177 ? 0x5CF740u : 0x5CEDB0u;
    public uint SchedulerCallback => Effect == 177 ? 0x5CF820u : 0x5CEE90u;
    public uint MainCallback => Effect == 177 ? 0x5CF8E0u : 0x5CEF50u;
    public uint RootCallback => Effect == 177 ? 0x5D0090u : 0x5CF700u;
    public uint CameraAddress => Effect == 177 ? 0xD44C5Cu : 0xD4499Cu;
    public int CameraLength => Effect == 177 ? 356 : 464;
    public uint SoundAddress => Effect == 177 ? 0xD44E10u : 0xD44BBCu;
    public uint NeedleVerticesAddress => Effect == 177 ? 0xD44E14u : 0xD44BC0u;
    public uint NeedleFacesAddress => Effect == 177 ? 0xD44E34u : 0xD44BE0u;
    public uint SparkSpriteAddress => Effect == 177 ? 0xD44DC0u : 0xD44B94u;
    public uint SparkDepthOffsetsAddress => Effect == 177 ? 0xD44E88u : 0xD44C34u;
    public uint TextureNameAddress => Effect == 177 ? 0xD44EA4u : 0xD44C50u;
    public static NativeBattleNeedlesFamilyProfile ForEffect(int effect) => effect switch
    {
        177 => new(177),
        178 => new(178),
        _ => throw new NotSupportedException("The needles family supports only its two original modules.")
    };
}
