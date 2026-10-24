using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect287Profile
{
    public const ushort Effect = 287;
    public const uint Entry = 0x6F2E80;
    public const uint Initializer = 0x5755B0;
    public const uint RootCallback = 0x6F2F20;
    public const uint MainCallback = 0x6F31A0;
    public const uint BaseAddress = 0x25562F8;
    public const uint ParametersGlobal = BaseAddress;
    public const uint CasterGlobal = 0x25562FC;
    public const uint SpriteDescriptor = 0x2556300;
    public const uint SmallDescriptor = 0x2556310;
    public const uint TargetDescriptor = 0x2556320;
    public const uint MeshDescriptor = 0x2556330;
    public const uint MainDescriptor = 0x2556340;
    public const uint RootDescriptor = 0x2556350;
    public const uint RootBank = 0x2556360;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2556374;
    public const uint RandomGlobal = 0x2556378;
    public const uint IntensityGlobal = 0x255637C;
    public const uint ArenaGlobal = 0x2556380;
    public const uint MeshQueueAddress = MeshDescriptor;
    public const int MeshQueueLength = 8;
    public const uint FirstTimAddress = 0x140016C;
    public const uint SecondTimAddress = 0x142938C;
    public const uint CameraAddress = 0x13FABFC;
    public const uint SoundAddress = 0x13FAE98;
    public const uint FirstProgrammeAddress = 0x13EF924;
    public const uint SecondProgrammeAddress = 0x13F87E8;
    public const uint TargetProgrammeAddress = 0x13F8F5C;
    public const uint FirstSpriteAddress = 0x13EF448;
    public const uint SecondSpriteAddress = 0x13EF5E8;
    public const uint RawMeshAddress = 0x13EF72C;
    public const uint SourcePointAddress = 0x13FAEA0;
    public const uint TargetOrderAddress = 0x13FAEA8;
    public const uint TriangleDescriptorsAddress = 0x13FAEB0;
    public const int TriangleDescriptorStride = 24;
    public const int TriangleDescriptorCount = 13;
    public const uint DistanceAddress = 0x13FAFE8;
    public const int DistanceLength = 16384;
    public const uint NormalizationAddress = 0x13FEFE8;
    public const int NormalizationLength = 384;
    public const int MorphOffset = 0x1A18;
    public const int MorphLength = 0x580;
    public const int TaperOffset = 0x1F98;
    public const int TaperCount = 8;
    public const int IoOffset = 0x1FA8;
    public const int FirstPacketOffset = 0x1FF0;
    public const int SecondPacketOffset = 0x11FF0;
    public const int File360Offset = 0x21FF0;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x1070, 0x9A8, 1), new(MeshDescriptor, 0x21FF4, 0x40, 64), new(TargetDescriptor, 0xB00, 0x1D0, 3), new(SmallDescriptor, 0x900, 0x20, 16), new(SpriteDescriptor, 0, 0x24, 64)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 8), (CursorGlobal, 16)];
}
