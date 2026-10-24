using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect66Profile
{
    public const ushort Effect = 66;
    public const uint Entry = 0x622610;
    public const uint Initializer = 0x6225F0;
    public const uint RootCallback = 0x627DF0;
    public const uint MainCallback = 0x6226E0;
    public const uint BaseAddress = 0x24C66B0;
    public const uint TargetGlobal = 0x24C66B4;
    public const uint InitializerGlobal = 0x24C9178;
    public const uint ParametersGlobal = 0x24C9148;
    public const uint CasterGlobal = 0x24C914C;
    public const uint ArenaGlobal = 0x24C91A0;
    public const uint CursorGlobal = 0x24C91A4;
    public const uint VibrationGlobal = 0x24C91A8;
    public const uint CapturedWorldAddress = 0x24C9128;
    public const uint ViewAddress = 0x24C9150;
    public const uint CenterAddress = 0x24C9170;
    public const uint PartialMatrixAddress = 0x24C91B0;
    public const uint InitializedBase = 0xDEA3D4;
    public const uint InitializedAddress = InitializedBase;
    public const int InitializedLength = 8;
    public const uint RootDescriptor = 0x24C66D8;
    public const uint RootBank = 0x24C66B8;
    public const int RootBankLength = 32;
    public const uint ChildDescriptor = 0x24C8308;
    public const uint ChildBank = 0x24C8318;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint FirstPointAddress = 0x24C66E8;
    public const int FirstPointCount = 100;
    public const uint SecondPointAddress = 0x24C7048;
    public const int SecondPointCount = 200;
    public const int PointStride = 24;
    public const uint TextureNameAddress = 0xDEA5B8;
    public const int TextureLength = 134176;
    public const uint EmbeddedTimAddress = 0xECC40C;
    public const int EmbeddedTimLength = 133152;
    public const uint CameraAddress = 0xDEA484;
    public const int CameraLength = 308;
    public const uint VibrationAddress = 0xDEA20C;
    public const int VibrationLength = 312;
    public static readonly ImmutableArray<uint> MeshAddresses = [0xDE4A54, 0xDE562C, 0xDE7F54, 0xDE87FC, 0xDE90CC, 0xDE97A4, 0xDE98E4];
    public static readonly ImmutableArray<int> MeshLengths = [3032, 10536, 2216, 2256, 1752, 320, 2344];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xDE423C, 0xDE43E8, 0xDE45D0, 0xDE477C, 0xDE4910];
    public static readonly ImmutableArray<int> SpriteLengths = [428, 488, 428, 404, 324];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 8), (FirstPointAddress, FirstPointCount * PointStride), (SecondPointAddress, SecondPointCount * PointStride), (CapturedWorldAddress, 32), (ParametersGlobal, 8), (ViewAddress, 32), (CenterAddress, 8), (InitializerGlobal, 4), (0x24C917C, 8), (0x24C9190, 4), (ArenaGlobal, 12), (PartialMatrixAddress, 32)];
}
