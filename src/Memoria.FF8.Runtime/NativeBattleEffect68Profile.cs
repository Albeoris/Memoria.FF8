using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect68Profile
{
    public const ushort Effect = 68;
    public const uint Entry = 0x7DC1A0;
    public const uint Initializer = 0x7DC180;
    public const uint RootCallback = 0x7DC280;
    public const uint DriverCallback = 0x7DC3A0;
    public const uint MainCallback = 0x7DC600;
    public const uint BaseAddress = 0x25FCDD8;
    public const uint InitializerGlobal = BaseAddress;
    public const uint CameraPointerAddress = 0x1591C40;
    public const uint CameraAddress = 0x25FCDE0;
    public const int CameraLength = 80;
    public const uint CursorGlobal = 0x25FCE30;
    public const uint MembershipAddress = 0x25FCE34;
    public const uint MainFlagGlobal = 0x25FD708;
    public const uint ContactFlagGlobal = 0x25FFAA4;
    public const uint TargetModelGlobal = 0x25FFAA8;
    public const uint SecondPacketGlobal = 0x25FFC38;
    public const uint FirstPacketGlobal = 0x25FFC3C;
    public const uint ControlFlagGlobal = 0x26005B0;
    public const uint ResultCompleteGlobal = 0x26005B2;
    public const uint HeaderBase = 0x279CC30;
    public const uint ResultPagesAddress = HeaderBase;
    public const uint ResultCountAddress = 0x279CC50;
    public const uint ResultElementAddress = 0x279CC58;
    public const uint ResultEmptyAddress = 0x279CC60;
    public const uint RootDescriptor = 0x25FD820;
    public const uint RootBank = 0x25FD758;
    public const uint DriverDescriptor = 0x25FFAC8;
    public const uint DriverBank = 0x25FFAD8;
    public const uint MainDescriptor = 0x25FFC40;
    public const uint MainBank = 0x25FFC50;
    public const uint ChildDescriptor = 0x25FFA90;
    public const uint ChildBank = 0x25FD830;
    public const uint DecorationDescriptor = 0x25FD6E0;
    public const uint DecorationBank = 0x25FCE70;
    public const uint TextureNameAddress = 0x1595744;
    public const int TextureLength = 167968;
    public const uint SoundAddress = 0x1594994;
    public const uint ProfileAddress = 0x1594998;
    public const int ProfileCount = 166;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 0x64, 2), new(DriverDescriptor, DriverBank, 0x58, 4), new(MainDescriptor, MainBank, 0x78, 20), new(ChildDescriptor, ChildBank, 0x58, 100), new(DecorationDescriptor, DecorationBank, 0x6C, 20)];
    public static readonly ImmutableArray<(uint Address, int Length)> CoreRanges = [(InitializerGlobal, 4), (CameraAddress, CameraLength), (CursorGlobal, 4), (MembershipAddress, 28), (MainFlagGlobal, 2), (ContactFlagGlobal, 2), (TargetModelGlobal, 4), (SecondPacketGlobal, 8), (ControlFlagGlobal, 4)];
    public static readonly ImmutableArray<(uint Address, int Length)> HeaderRanges = [(HeaderBase, 0x24), (ResultElementAddress, 9)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = CoreRanges.AddRange(HeaderRanges);
    public static readonly ImmutableArray<uint> MeshAddresses = [0x1591C44, 0x159202C, 0x1592794, 0x1592810, 0x159346C, 0x15934CC, 0x1593554, 0x15935DC, 0x1593664, 0x15936EC, 0x1593774, 0x15937FC, 0x1593884];
    public static readonly ImmutableArray<int> MeshLengths = [1000, 1896, 124, 124, 96, 136, 136, 136, 136, 136, 136, 136, 136];
    public static readonly ImmutableArray<uint> MutableMeshAddresses = [0x1591C44, 0x159202C];
    public static readonly ImmutableArray<uint> VertexBankAddresses = [0x159390C, 0x1593A94, 0x1593C1C, 0x1594124];
    public static readonly ImmutableArray<int> VertexBankLengths = [392, 392, 1288, 1288];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x159462C, 0x1594950];
    public static readonly ImmutableArray<int> SpriteLengths = [804, 68];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(CameraPointerAddress, 4), (SoundAddress, 4), (ProfileAddress, 0xA60), (0x15953F8, 0x24), (0x159541C, 0x24), (0x1595440, 0x48), (0x1595488, 0x48), (0x15954D0, 80), (0x1595520, 80), (0x1595570, 34), (0x15955A0, 34), (0x15955D0, 34), (0x1595600, 34), (0x1595630, 34), (0x1595660, 34), (0x1595690, 34), (0x15956C0, 34), (0x15956F0, 84)];
}
