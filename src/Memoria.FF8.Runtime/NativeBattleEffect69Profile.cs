using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect69Profile
{
    public const ushort Effect = 69;
    public const uint Entry = 0x6FE040;
    public const uint Initializer = 0x5755F0;
    public const uint RootCallback = 0x6FE0D0;
    public const uint MainCallback = 0x6FE5D0;
    public const uint BaseAddress = 0x2556628;
    public const uint SparkCursorGlobal = BaseAddress;
    public const uint DebrisCursorGlobal = 0x255662C;
    public const uint CursorGlobal = 0x2556630;
    public const uint ParametersGlobal = 0x2556634;
    public const uint CasterGlobal = 0x2556638;
    public const uint RandomGlobal = 0x2556F70;
    public const uint ArenaGlobal = 0x2556F74;
    public const uint ViewAddress = 0x2556F78;
    public const int ViewLength = 32;
    public const uint RootDescriptor = 0x2556660;
    public const uint RootBank = 0x2556670;
    public const uint MainDescriptor = 0x2556650;
    public const uint MainBank = 0x2556680;
    public const uint ProgrammeAddress = 0x14D0C2C;
    public const int ProgrammeLength = 26036;
    public const int ProgrammeWorkspaceLength = 0x8D4;
    public const uint SelectorsAddress = 0x14D71E0;
    public const uint CameraAddress = 0x14D0858;
    public const int CameraLength = 496;
    public const uint VibrationAddress = 0x14D0740;
    public const int VibrationLength = 280;
    public const uint SoundAddress = 0x14D0A48;
    public const uint SpriteAddress = 0x14D0A4C;
    public const int SpriteLength = 480;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 0x10, 1), new(MainDescriptor, MainBank, 0x8F0, 1)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 0x14), (RandomGlobal, 4), (ArenaGlobal, 4), (ViewAddress, ViewLength)];
}
