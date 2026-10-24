using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect318Profile
{
    public const ushort Effect = 318;
    public const uint Entry = 0x65E510;
    public const uint Initializer = 0x65E520;
    public const uint RootCallback = 0x65E5C0;
    public const uint MainCallback = 0x65E790;
    public const uint BaseAddress = 0x2505418;
    public const uint InitializerGlobal = 0x2505418;
    public const uint CursorGlobal = 0x250541C;
    public const uint ParametersGlobal = 0x2505420;
    public const uint CasterGlobal = 0x2505424;
    public const uint MeshDescriptor = 0x2505428;
    public const uint BoneDescriptor = 0x2505438;
    public const uint MainDescriptor = 0x2505448;
    public const uint RootDescriptor = 0x2505458;
    public const uint RootBank = 0x2505468;
    public const int RootBankLength = 20;
    public const uint PointAddress = 0x2505480;
    public const uint ArenaGlobal = 0x2505488;
    public const uint TextureNameAddress = 0xFFE6A0;
    public const uint CameraAddress = 0xFF77AC;
    public const uint SoundAddress = 0xFF77A8;
    public const uint FirstProgrammeAddress = 0xFFD9B8;
    public const uint SecondProgrammeAddress = 0xFF8DB8;
    public const uint ThirdProgrammeAddress = 0xFFB4CC;
    public const int FirstWorkspaceOffset = 0xE4;
    public const int FirstWorkspaceLength = 0xEC;
    public const int SecondWorkspaceOffset = 0x1D0;
    public const int SecondWorkspaceLength = 0x5E8;
    public const int ThirdWorkspaceOffset = 0x7B8;
    public const int ThirdWorkspaceLength = 0x348;
    public const int RecordOffset = 0xB00;
    public const int RecordStride = 16;
    public const int RecordCount = 128;
    public const int RecordCursorOffset = 0x1300;
    public const int IoOffset = 0x1990;
    public const int MorphOffset = 0x19D8;
    public const int MorphLength = 1032;
    public const int FirstPacketOffset = 0x1DE0;
    public const int SecondPacketOffset = 0x11DE0;
    public const int ThirdPacketOffset = 0x21DE0;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, 0x1304, 1), new(BoneDescriptor, 0x1304, 0x1C, 5), new(MeshDescriptor, 0x1390, 0x30, 32)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 16), (PointAddress, 12)];
}
