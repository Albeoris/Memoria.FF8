using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGravija214Profile
{
    public const ushort Effect = 214;
    public const uint Entry = 0x69D5B0;
    public const uint Initializer = 0x69D5C0;
    public const uint RootCallback = 0x69D670;
    public const uint RootDescriptor = 0x2510138;
    public const uint RootBank = 0x2510100;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x25089B0;
    public const uint CasterGlobal = 0x25089B0;
    public const uint InitializerGlobal = 0x25089B4;
    public const uint ArenaGlobal = 0x25089B8;
    public const uint ParametersGlobal = 0x25100FC;
    public const uint CursorGlobal = 0x2510114;
    public const uint MatrixAddress = 0x2510148;
    public const uint SourceMeshAddress = 0x1172D00;
    public const uint CopiedMeshAddress = 0x25089BC;
    public const int EntryCopyLength = 0x7740;
    public const int MeshLength = 7632;
    public const int VertexCount = 241;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int PacketBankBytes = 0x10000;
    public const int FirstPacketOffset = 0x1970;
    public const int SecondPacketOffset = 0x11970;
    public const int ScratchOffset = 0xA24;
    public const int ScratchLength = 1024;
    public const int ProjectedRowsOffset = 0xE24;
    public const int ProjectedRowsLength = 241 * 12;
    public const uint ProgrammeAddress = 0x1174AD0;
    public const int ProgrammeLength = 15976;
    public const int ProgrammeWorkspaceLength = 756;
    public const uint SpriteAddress = 0x1172B40;
    public const int SpriteLength = 448;
    public const uint CameraAddress = 0x1178938;
    public const int CameraLength = 408;
    public const uint SoundAddress = 0x1172B3C;
    public const uint TextureNameAddress = 0x10231C8;
    public const int TextureLength = 101920;
    public const uint SqrtTableAddress = 0x1178AD0;
    public const int SqrtTableLength = 384;
    public const uint SineTableAddress = 0x1178C50;
    public const int SineTableLength = 8192;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2510118, ArenaAddress, 28, 64), new(0x2510128, ArenaAddress + 0x700, 804, 1)];
}
