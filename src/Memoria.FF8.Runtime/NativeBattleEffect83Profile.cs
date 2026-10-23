using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect83Profile
{
    public const ushort Effect = 83;
    public const uint Entry = 0x7976C0;
    public const uint Initializer = 0x7976A0;
    public const uint RootCallback = 0x797840;
    public const uint BaseAddress = 0x25C9F70;
    public const uint InitializerGlobal = BaseAddress;
    public const uint RootDescriptor = 0x25CA150;
    public const uint RootBank = 0x25CA088;
    public const uint TargetAddress = 0x25CF880;
    public const int TargetLength = 84;
    public const uint ViewAddress = 0x2793E58;
    public const int ViewLength = 32;
    public const uint ProjectedAddress = 0x25CF3F8;
    public const int ProjectedLength = 1128;
    public const uint TransformedAddress = 0x25CA868;
    public const int TransformedLength = 7648;
    public const uint DescriptorAddress = 0x25CFA58;
    public const int DescriptorLength = 38;
    public const uint NodeSourceAddress = 0x172C818;
    public const int NodeSourceLength = 50160;
    public const uint ModelAddress = 0x15786D4;
    public const int ModelLength = 10252;
    public const uint CameraAddress = 0x1578444;
    public const int CameraLength = 656;
    public const uint EmbeddedTimAddress = 0x1738C08;
    public const int EmbeddedTimLength = 33824;
    public const uint SoundAddress = 0x1578440;
    public const uint TargetPointerAddress = 0x157843C;
    public const uint TextureNameAddress = 0x157AEE0;
    public const int TextureLength = 164896;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 0x64, 2), new(0x25CF278, 0x25CF288, 0x58, 4), new(0x25CA058, 0x25C9F80, 0x48, 3), new(0x25CEFD8, 0x25CED30, 0x2A4, 1), new(0x25CA160, 0x25CA170, 0xB0, 10), new(0x25CA858, 0x25CE6E8, 0xC8, 8), new(0x25CF268, 0x25CEFE8, 0x40, 10), new(0x25CFA18, 0x25CF8D8, 0x140, 1)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(0x25C9F70, 10), (0x25CA07C, 8), (0x25CA850, 2), (0x25CA854, 4), (0x25CED28, 8), (0x25CF3E8, 12), (0x25CF878, 2), (0x25CFA28, 2), (0x25CFA2C, 4), (0x25CFA38, 4), (0x25CFA40, 20), (TargetAddress, TargetLength), (ProjectedAddress, ProjectedLength), (TransformedAddress, TransformedLength), (DescriptorAddress, DescriptorLength)];
}
