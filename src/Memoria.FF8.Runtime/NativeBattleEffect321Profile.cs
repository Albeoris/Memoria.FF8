using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Profile
{
    public const ushort Effect = 321;
    public const uint Entry = 0x597A00;
    public const uint Initializer = 0x5979E0;
    public const uint RootCallback = 0x598D00;
    public const uint MainCallback = 0x597AB0;
    public const uint CleanupCallback = 0x598D60;
    public const uint BaseAddress = 0x221BC68;
    public const uint TargetGlobal = BaseAddress;
    public const uint RootBank = 0x221BC70;
    public const int RootBankLength = 32;
    public const uint RootDescriptor = 0x221BC90;
    public const uint RecordsAddress = 0x221BCA0;
    public const int RecordStride = 24;
    public const int RecordCount = 100;
    public const uint ChildDescriptor = 0x221C600;
    public const uint ChildBank = 0x221C610;
    public const int ChildBankLength = 3600;
    public const uint SavedActorsAddress = 0x221D420;
    public const int SavedActorStride = 36;
    public const uint ParametersGlobal = 0x221D51C;
    public const uint CasterGlobal = 0x221D520;
    public const uint InitializerGlobal = 0x221D524;
    public const uint FirstPacketAddress = 0x221D528;
    public const uint SecondPacketAddress = 0x222B528;
    public const int PacketLength = 0xE000;
    public const uint CursorGlobal = 0x2239528;
    public const uint VibrationGlobal = 0x223952C;
    public const uint TextureNameAddress = 0xCD2AFC;
    public const uint CameraAddress = 0xCD1284;
    public const uint VibrationAddress = 0xCD29DC;
    public const uint FirstSoundAddress = 0xCD2AF0;
    public const uint SecondSoundAddress = 0xCD2AF4;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, 36, 100)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = BuildRanges();

    private static ImmutableArray<(uint Address, int Length)> BuildRanges()
    {
        var ranges = ImmutableArray.CreateBuilder<(uint Address, int Length)>();
        ranges.Add((TargetGlobal, 4));
        ranges.Add((RecordsAddress, RecordStride * RecordCount));
        for (int actor = 0; actor < 7; actor++)
        {
            uint address = SavedActorsAddress + (uint)(actor * SavedActorStride);
            ranges.Add((address, 2));
            ranges.Add((address + 4, 16));
        }
        ranges.Add((ParametersGlobal, 12));
        ranges.Add((FirstPacketAddress, PacketLength));
        ranges.Add((SecondPacketAddress, PacketLength));
        ranges.Add((CursorGlobal, 8));
        return ranges.ToImmutable();
    }
}
