using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect324Profile
{
    public const ushort Effect = 324;
    public const uint Entry = 0x596BC0;
    public const uint Initializer = 0x596BA0;
    public const uint RootCallback = 0x5979A0;
    public const uint MainCallback = 0x596C70;
    public const uint BaseAddress = 0x2201080;
    public const uint SavedWorldAddress = 0x2201080;
    public const uint TargetGlobal = 0x22010A0;
    public const uint OriginalBonesAddress = 0x22010A8;
    public const uint RootBank = 0x2201AA8;
    public const int RootBankLength = 16;
    public const uint RootDescriptor = 0x2201AB8;
    public const uint RecordsAddress = 0x2201AC8;
    public const int RecordStride = 24;
    public const int RecordCount = 100;
    public const uint CurrentBonesAddress = 0x2202428;
    public const int BonesLength = 28 * 32;
    public const uint ChildDescriptor = 0x2202E28;
    public const uint ChildBank = 0x2202E38;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint ParametersGlobal = 0x2203C48;
    public const uint CasterGlobal = 0x2203C4C;
    public const uint InitializerGlobal = 0x2203C50;
    public const uint PointAddress = 0x2203C58;
    public const uint FirstPacketAddress = 0x220BC60;
    public const uint SecondPacketAddress = 0x2213C60;
    public const int PacketBankLength = 0x8000;
    public const uint CursorGlobal = 0x221BC60;
    public const uint VibrationGlobal = 0x221BC64;
    public const uint TextureNameAddress = 0xCD1278;
    public const uint CameraAddress = 0xCD09F0;
    public const uint VibrationAddress = 0xCD1104;
    public const uint SoundAddress = 0xCD1210;
    public const uint PaletteAddress = 0xCD1218;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(SavedWorldAddress, 32), (TargetGlobal, 4), (OriginalBonesAddress, BonesLength), (RecordsAddress, RecordStride * RecordCount), (CurrentBonesAddress, BonesLength), (ParametersGlobal, 12), (PointAddress, 8), (FirstPacketAddress, PacketBankLength), (SecondPacketAddress, PacketBankLength), (CursorGlobal, 8)];
}
