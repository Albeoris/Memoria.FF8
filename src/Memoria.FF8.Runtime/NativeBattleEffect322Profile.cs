using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect322Profile
{
    public const ushort Effect = 322;
    public const uint Entry = 0x65AB70;
    public const uint Initializer = 0x65AB80;
    public const uint RootCallback = 0x65AC20;
    public const uint MainCallback = 0x65AE00;
    public const uint BaseAddress = 0x25052B0;
    public const uint InitializerGlobal = 0x25052B0;
    public const uint ParametersGlobal = 0x25052B4;
    public const uint CasterGlobal = 0x25052B8;
    public const uint PuffDescriptor = 0x25052C0;
    public const uint BurstDescriptor = 0x25052D0;
    public const uint ProgrammeDescriptor = 0x25052E0;
    public const uint MeshDescriptor = 0x25052F0;
    public const uint MainDescriptor = 0x2505300;
    public const uint RootDescriptor = 0x2505310;
    public const uint RootBank = 0x2505320;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2505334;
    public const uint PointAddress = 0x2505338;
    public const uint ArenaGlobal = 0x2505340;
    public const uint TextureNameAddress = 0xFE7288;
    public const uint CameraAddress = 0xFE7004;
    public const uint ProgrammeAddress = 0xFE5CC0;
    public const int WorkspaceOffset = 0x18;
    public const int WorkspaceLength = 0x210;
    public const int IoOffset = 0xCA4;
    public const int FirstPacketOffset = 0xCEC;
    public const int SecondPacketOffset = 0xECEC;
    public const int ThirdPacketOffset = 0x1CCEC;
    public const int MorphOffset = 0x1CCF0;
    public const int MorphLength = 200;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, 0x7C, 1), new(MeshDescriptor, 0x7C, 0x30, 8), new(ProgrammeDescriptor, 0x1FC, 0x228, 1), new(BurstDescriptor, 0x424, 0x2C, 32), new(PuffDescriptor, 0x9A4, 0x18, 32)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 16)];
}
