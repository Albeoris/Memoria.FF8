using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Profile
{
    public const string InitializerTextureName = "mag030.tim";
    public const uint InitializerGlobal = 0x26CDD8C;
    public const uint TextureNameAddress = 0x160D3DC;
    public const int TextureLength = 166432;
    public const uint CameraAddress = 0x160D28C;
    public const int CameraLength = 296;
    public const uint CustomDirectoryAddress = 0x160D188;
    public const uint FactorAddress = 0x160D3B4;
    public const uint YOffsetAddress = 0x160D3C8;
    public const uint SoundAddress = 0x15F8DF0;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x15F98F8, 0x1602658, 0x1608024, 0x160AF24];
    public static readonly ImmutableArray<int> ProgrammeLengths = [36192, 22988, 12032, 4932];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [256, 44, 124, 108];
    public static readonly ImmutableArray<int> ProgrammeDurations = [16, 14, 48, 15];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x15F990C, 0x15FF248, 0x1601334, 0x1602664, 0x1608034, 0x1608C08, 0x160AF34, 0x160B008];
    public static readonly ImmutableArray<int> MeshLengths = [22844, 8428, 3772, 22844, 3028, 7484, 212, 4348];
    public static readonly ImmutableArray<int> MeshVertexCounts = [193, 118, 118, 193, 97, 118, 9, 125];
    public static readonly ImmutableArray<int> MeshShapeCounts = [10, 2, 1, 10, 1, 1, 1, 2];
    public static readonly ImmutableArray<ImmutableArray<int>> MeshGroupCounts = [[0, 0, 0, 0, 368, 0, 0, 0], [0, 0, 0, 0, 0, 0, 232, 0], [232, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 368, 0, 0, 0], [0, 0, 0, 0, 24, 72, 0, 0], [0, 0, 0, 0, 0, 0, 232, 0], [0, 0, 0, 4, 0, 0, 0, 0], [0, 0, 0, 0, 0, 96, 0, 0]];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x15F8DF4, 0x15F8EEC, 0x15F97B4, 0x15F9174, 0x15F944C, 0x15F95E0, 0x15F96BC, 0x15F9250];
    public static readonly ImmutableArray<int> SpriteLengths = [248, 324, 324, 220, 404, 220, 248, 508];
    public static readonly ImmutableArray<int> SpriteFrameCounts = [9, 12, 12, 8, 15, 8, 9, 19];
    public static readonly ImmutableArray<uint> ControlAddresses = [0x160C978, 0x160CAD0, 0x160CC28, 0x160CD80, 0x160CED8, 0x160D030];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0x15F8DC8, 16), (0x15F8DD8, 24), (0x15F8DF0, 4), (0x160C268, 12), (0x160C290, 24), (0x160C2A8, 24), (0x160C2C0, 24), (0x160C314, 24), (0x160C33C, 48), (0x160C36C, 48), (0x160C39C, 48), (0x160C414, 24), (0x160C42C, 24), (0x160C444, 48), (0x160C474, 48), (0x160C4A4, 48), (0x160C4E0, 22), (0x160C508, 44), (0x160C534, 44), (0x160C560, 44), (0x160C5D4, 22), (0x160C5EC, 22), (0x160C604, 44), (0x160C630, 44), (0x160C65C, 44), (0x160C694, 10), (0x160C6BC, 20), (0x160C6D0, 20), (0x160C6E4, 20), (0x160C730, 26), (0x160C758, 52), (0x160C78C, 52), (0x160C7C0, 52), (0x160C864, 19), (0x160C88C, 38), (0x160C8B4, 38), (0x160C8DC, 38), (0x160C978, 328), (0x160CAC0, 6), (0x160CAC8, 6), (0x160CAD0, 328), (0x160CC18, 6), (0x160CC20, 6), (0x160CC28, 328), (0x160CD70, 6), (0x160CD78, 6), (0x160CD80, 328), (0x160CEC8, 6), (0x160CED0, 6), (0x160CED8, 328), (0x160D020, 6), (0x160D028, 6), (0x160D030, 328), (0x160D178, 6), (0x160D180, 6), (0x160D188, 260), (0x160D3B4, 18), (0x160D3C8, 18)];
    public static readonly ImmutableArray<(uint Address, int Length)> MutableStreamRanges = [(0x26D33E4, 16), (0x26D33F8, 16), (0x26D340C, 16), (0x26D35A4, 22), (0x26D35BC, 22), (0x26D35D4, 22), (0x26D3628, 24), (0x26D3640, 24), (0x26D3658, 48), (0x26D3688, 48), (0x26D36B8, 48), (0x26D36E8, 48), (0x26D3790, 22), (0x26D37A8, 22), (0x26D37C0, 22), (0x26D3814, 22), (0x26D382C, 22), (0x26D3844, 44), (0x26D3870, 44), (0x26D389C, 44), (0x26D38C8, 44), (0x26D396C, 14), (0x26D397C, 14), (0x26D398C, 14), (0x26D3AD0, 28), (0x26D3AF0, 28), (0x26D3B10, 28), (0x26D3DFC, 28), (0x26D3E1C, 28), (0x26D3E3C, 28)];
    public static readonly ImmutableArray<ImmutableArray<uint>> ControlPointerCells = [[0x160C268, 0x160C290, 0x160C2A8, 0x160C2C0, 0x26D33E4, 0x26D33F8, 0x26D340C, 0x160C2D8, 0x160C2EC, 0x160C300, 0x26D3420, 0x26D342C, 0x26D3438, 0x26D3444, 0x26D3458, 0x26D3464, 0x26D3470, 0x26D347C, 0x26D3488, 0x26D34A0, 0x26D34B8, 0x26D34D0, 0x26D34E8, 0x26D3500, 0x26D3518, 0x26D3530, 0x26D353C, 0x26D3548, 0x26D3554, 0x26D3568, 0x26D357C, 0x26D3590], [0x160C314, 0x160C33C, 0x160C36C, 0x160C39C, 0x26D35A4, 0x26D35BC, 0x26D35D4, 0x160C3CC, 0x160C3E4, 0x160C3FC, 0x26D35EC, 0x26D35F8, 0x26D3604, 0x26D3610, 0x26D3628, 0x26D3640, 0x160C414, 0x160C42C, 0x26D3658, 0x160C444, 0x26D3688, 0x26D36B8, 0x160C474, 0x26D36E8, 0x160C4A4, 0x160C4D4, 0x26D3718, 0x26D3724, 0x26D3730, 0x26D3748, 0x26D3760, 0x26D3778], [0x160C4E0, 0x160C508, 0x160C534, 0x160C560, 0x26D3790, 0x26D37A8, 0x26D37C0, 0x160C58C, 0x160C5A4, 0x160C5BC, 0x26D37D8, 0x26D37E4, 0x26D37F0, 0x26D37FC, 0x26D3814, 0x26D382C, 0x160C5D4, 0x160C5EC, 0x26D3844, 0x160C604, 0x26D3870, 0x26D389C, 0x160C630, 0x26D38C8, 0x160C65C, 0x160C688, 0x26D38F4, 0x26D3900, 0x26D390C, 0x26D3924, 0x26D393C, 0x26D3954], [0x160C694, 0x160C6BC, 0x160C6D0, 0x160C6E4, 0x26D396C, 0x26D397C, 0x26D398C, 0x160C6F8, 0x160C708, 0x160C718, 0x26D399C, 0x26D39A4, 0x26D39AC, 0x26D39B4, 0x26D39C4, 0x26D39D0, 0x26D39DC, 0x26D39E8, 0x26D39F4, 0x26D3A08, 0x26D3A1C, 0x26D3A30, 0x26D3A44, 0x26D3A58, 0x26D3A6C, 0x160C728, 0x26D3A80, 0x26D3A88, 0x26D3A90, 0x26D3AA0, 0x26D3AB0, 0x26D3AC0], [0x160C730, 0x160C758, 0x160C78C, 0x160C7C0, 0x26D3AD0, 0x26D3AF0, 0x26D3B10, 0x160C7F4, 0x160C814, 0x160C834, 0x26D3B30, 0x26D3B40, 0x26D3B50, 0x26D3B60, 0x26D3B80, 0x26D3B9C, 0x26D3BB8, 0x26D3BD4, 0x26D3BF0, 0x26D3C24, 0x26D3C58, 0x26D3C8C, 0x26D3CC0, 0x26D3CF4, 0x26D3D28, 0x160C854, 0x26D3D5C, 0x26D3D6C, 0x26D3D7C, 0x26D3D9C, 0x26D3DBC, 0x26D3DDC], [0x160C864, 0x160C88C, 0x160C8B4, 0x160C8DC, 0x26D3DFC, 0x26D3E1C, 0x26D3E3C, 0x160C904, 0x160C924, 0x160C944, 0x26D3E5C, 0x26D3E6C, 0x26D3E7C, 0x26D3E8C, 0x26D3EAC, 0x26D3EC0, 0x26D3ED4, 0x26D3EE8, 0x26D3EFC, 0x26D3F24, 0x26D3F4C, 0x26D3F74, 0x26D3F9C, 0x26D3FC4, 0x26D3FEC, 0x160C964, 0x26D4014, 0x26D4024, 0x26D4034, 0x26D4054, 0x26D4074, 0x26D4094]];
    public static readonly ImmutableArray<int> ControlHeaderOffsets = [17, 18, 19, 22, 27, 28, 29, 30, 31, 32, 33, 34, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 51, 52, 53, 54, 55];
    public static readonly ImmutableArray<ImmutableArray<byte>> ControlHeaderValues = [[0, 12, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 1, 0, 4, 0], [4, 24, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0], [4, 22, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0], [0, 10, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 4, 0], [0, 26, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 4, 0], [0, 19, 23, 0, 0, 0, 0, 2, 7, 12, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 4, 0]];
    public static readonly ImmutableArray<uint> ColorProgrammeAddresses = [0x15F8DC8, 0x15F8DD8];
    public static readonly ImmutableArray<int> ColorProgrammeLengths = [16, 24];
}
