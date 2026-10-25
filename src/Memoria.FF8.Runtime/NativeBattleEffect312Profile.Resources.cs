using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Profile
{
    public const string InitializerTextureName = "mag311.tim";
    public const uint InitializerGlobal = 0x2240EC0;
    public const uint TextureNameAddress = 0xCDF6C8;
    public const int TextureLength = 133664;
    public const uint CameraAddress = 0xCD8D90;
    public const int CameraLength = 300;
    public const uint SpriteAddress = 0xCD8EBC;
    public const int SpriteLength = 312;
    public const uint CenterOffsetAddress = 0xCDF658;
    public const uint AlignmentAddress = 0xCDF660;
    public const uint PaletteAddress = 0xCDF670;
    public const uint MeshTableAddress = 0xCDF6A0;
    public const uint CapturePacketAddress = 0xCDF6B0;
    public const int CapturePacketLength = 24;
    public static readonly ImmutableArray<uint> MeshAddresses = [0xCDDE9C, 0xCDDDCC, 0xCDE73C, 0xCDCCB4, 0xCD98F4, 0xCD8FF4, 0xCD978C, 0xCD9624, 0xCDAF9C, 0xCDEE24, 0xCDF0DC, 0xCDF394];
    public static readonly ImmutableArray<int> MeshLengths = [2208, 208, 1768, 4376, 5800, 1584, 360, 360, 7448, 696, 696, 696];
    public static readonly ImmutableArray<int> MeshVertexCounts = [73, 9, 72, 110, 192, 65, 16, 16, 176, 12, 12, 12];
    public static readonly ImmutableArray<ImmutableArray<int>> MeshGroupCounts = [[0, 0, 0, 0, 36, 36, 0, 0], [0, 0, 0, 4, 0, 0, 0, 0], [0, 0, 0, 0, 0, 48, 0, 0], [3, 0, 0, 0, 171, 0, 0, 0], [0, 0, 0, 128, 0, 0, 0, 32], [0, 0, 0, 0, 8, 36, 0, 0], [0, 0, 0, 5, 0, 0, 0, 2], [0, 0, 0, 5, 0, 0, 0, 2], [0, 0, 300, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 20, 0], [0, 0, 0, 0, 0, 0, 20, 0], [0, 0, 0, 0, 0, 0, 20, 0]];
    public static readonly ImmutableArray<uint> SpriteAddresses = [SpriteAddress];
    public static readonly ImmutableArray<int> SpriteLengths = [SpriteLength];
    public static readonly ImmutableArray<uint> SoundAddresses = [0xCDF64C, 0xCDF650];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0xCDF64C, 4), (0xCDF650, 4), (CenterOffsetAddress, 6), (AlignmentAddress, 16), (PaletteAddress, 48), (MeshTableAddress, 12), (CapturePacketAddress, CapturePacketLength)];
}
