using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect79Profile
{
    public const ushort Effect = 79;
    public const uint Entry = 0x7B4BD0;
    public const uint Initializer = 0x7B4BB0;
    public const uint RootCallback = 0x7B4D80;
    public const uint BaseAddress = 0x25DF338;
    public const uint InitializerGlobal = BaseAddress;
    public const uint CursorGlobal = 0x25DF33C;
    public const uint RootDescriptor = 0x25E50F0;
    public const uint RootBank = 0x25E5028;
    public const uint TargetAddress = 0x25EAD88;
    public const int TargetLength = 84;
    public const uint ControlAddress = 0x25EB3C0;
    public const int ControlLength = 0x16C;
    public const uint ScriptAddress = 0x1581F18;
    public const int ScriptLength = 112;
    public const uint ViewAddress = 0x2793E58;
    public const uint BodyContextAddress = 0x25DF348;
    public const int BodyContextLength = 0xD4;
    public const uint TransformedAddress = 0x25DF508;
    public const int TransformedLength = 0x57E0;
    public const uint DescriptorAddress = 0x25EA350;
    public const int DescriptorLength = 0x26;
    public const uint MorphAddress = 0x25EAFA8;
    public const int MorphLength = 1000;
    public const uint CameraAddress = 0x1581BCC;
    public const int CameraLength = 804;
    public const uint VibrationAddress = 0x176E064;
    public const int VibrationLength = 304;
    public const uint TargetPointerAddress = 0x1580B20;
    public const uint PointSelectorsAddress = 0x1581EF0;
    public const uint TextureNameAddress = 0x1581F88;
    public const int TextureLength = 168992;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x19F1018, 0x19F24C4, 0x19F36C0, 0x19F4880];
    public static readonly ImmutableArray<int> ProgrammeLengths = [5292, 4604, 4544, 6092];
    public static readonly ImmutableDictionary<uint, int> NodeSourceLengths = new Dictionary<uint, int> { [0x1762E40] = 31512, [0x176A958] = 6916, [0x176C45C] = 7176 }.ToImmutableDictionary();
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 0x64, 2), new(0x25EA1D0, 0x25EA1E0, 0x58, 4), new(0x25DF4F8, 0x25DF420, 0x48, 3), new(0x25E9F30, 0x25E9740, 0x2A4, 3), new(0x25E9728, 0x25E8C28, 0x2C0, 4), new(0x25E5100, 0x25E5110, 0xB4, 0x50), new(0x25E8958, 0x25E8968, 0x58, 8), new(0x25EA1C0, 0x25E9F40, 0x40, 10), new(0x25EAF20, 0x25EADE0, 0x140, 1), new(0x25EB398, 0x25EAF48, 0x30, 2)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = new (uint Address, int Length)[] { (0x25DF338, 4), (0x25DF33C, 4), (0x25DF340, 1), (0x25DF342, 2), (0x25E5020, 4), (0x25E5024, 4), (0x25E8950, 2), (0x25E8954, 4), (0x25E9738, 4), (0x25E973C, 4), (0x25EA340, 4), (0x25EA344, 4), (0x25EA348, 4), (0x25EAD80, 2), (0x25EAD84, 4), (0x25EAF30, 2), (0x25EAF34, 4), (0x25EAF40, 2), (0x25EB390, 2), (0x25EB3AC, 4), (0x25EB3B0, 4), (0x25EB3B4, 4), (0x25EB3B8, 4), (0x25EB3BC, 4), (TargetAddress, TargetLength), (ControlAddress, ControlLength), (BodyContextAddress, BodyContextLength), (TransformedAddress, TransformedLength), (DescriptorAddress, DescriptorLength), (MorphAddress, MorphLength) }.OrderBy(range => range.Address).ToImmutableArray();
}
