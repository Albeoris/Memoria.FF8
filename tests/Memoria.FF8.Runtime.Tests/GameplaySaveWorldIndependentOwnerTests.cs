using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveWorldIndependentOwnerTests
{
    [Fact]
    public void Dormant_positions_retain_all_native_widths_without_claiming_yaw_or_active_transport()
    {
        byte[] original = new byte[NativePcSaveData.BlockSize];
        original[0] = (byte)'S';
        original[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(386), 0x08FF);
        int[] offsets = [5088, 5100, 5160, 5172];
        for (int index = 0; index < offsets.Length; index++)
        {
            int offset = offsets[index];
            BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(offset), int.MinValue + index);
            BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(offset + 4), int.MaxValue - index);
            BinaryPrimitives.WriteInt16LittleEndian(original.AsSpan(offset + 8), (short)(short.MinValue + index));
            BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(offset + 10), (ushort)(0xF000 + index));
        }
        NativeSaveInteroperability saved = GameplaySaveCodec.Detach(NativeSaveInteroperability.Capture(new(original)));
        byte[] exported = Enumerable.Repeat((byte)0xA5, NativePcSaveData.BlockSize).ToArray();

        saved.ApplyTo(exported);

        foreach (int offset in offsets)
        {
            Assert.Equal(original.AsSpan(offset, 10).ToArray(), exported.AsSpan(offset, 10).ToArray());
            Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(exported.AsSpan(offset + 10)));
        }
        Assert.Equal(new NativeSaveDormantWorldPosition(int.MinValue, int.MaxValue, short.MinValue), saved.DormantFootPosition);
        Assert.Equal(new NativeSaveDormantWorldPosition(int.MinValue + 1, int.MaxValue - 1, short.MinValue + 1), saved.AlternateWorldPosition);
        Assert.Equal(new NativeSaveDormantWorldPosition(int.MinValue + 2, int.MaxValue - 2, short.MinValue + 2), saved.ReservedWorldPosition6);
        Assert.Equal(new NativeSaveDormantWorldPosition(int.MinValue + 3, int.MaxValue - 3, short.MinValue + 3), saved.ReservedWorldPosition7);
    }

    [Theory]
    [InlineData(GameplaySaveWorldReturnKind.Foot, 0)]
    [InlineData(GameplaySaveWorldReturnKind.Car, 33)]
    [InlineData(GameplaySaveWorldReturnKind.Garden, 48)]
    [InlineData(GameplaySaveWorldReturnKind.Ragnarok, 50)]
    public void Supported_world_returns_preserve_original_angle_words_without_an_original_image(GameplaySaveWorldReturnKind kind, byte entity)
    {
        NativeSavedWorldAngleWords angles = new(ImmutableArray.Create<ushort>(0xA123, 0xB234, 0xC345, 0xD456, 0xE567, 0xF678, 0x8789, 0x989A), 0xF9C5);
        NativeSavedWorldPose pose = new(1234, -5678, 90, 123);
        GameplaySaveWorldReturn savedReturn = new(kind, 106, 42, pose, 321, entity, 255, 255, 0, 2, 7)
        {
            FootMode = kind == GameplaySaveWorldReturnKind.Foot ? 0 : null,
            RetireSelectedVehicle = kind == GameplaySaveWorldReturnKind.Car ? false : null
        };
        GameplaySaveWorldData saved = GameplaySaveWorldData.Capture(NativeSavedWorldState.CreateNewGame()) with
        {
            OriginalAngles = angles,
            Return = savedReturn,
            FuelDistance = 0x12345678,
            EventVariable0 = 17,
            EventVariable1 = 29
        };
        saved = GameplaySaveCodec.Detach(saved);

        NativeSavedWorldState restored = GameplaySaveWorldResume.RestoreSavedWorld(saved, NativeSavedWorldState.CreateNewGame(), 42);
        GameplaySaveWorldData recaptured = GameplaySaveWorldData.Capture(restored);

        Assert.Equal(NativeSavedWorldOriginKind.GameplaySave, restored.Origin.Kind);
        Assert.Equal(angles.BodyWords, recaptured.OriginalAngles!.BodyWords);
        Assert.Equal(angles.CameraWord, recaptured.OriginalAngles.CameraWord);
        Assert.Equal(savedReturn, recaptured.Return);
        Assert.Equal(saved.Slot5, recaptured.Slot5);
        Assert.Equal(saved.FuelDistance, recaptured.FuelDistance);
        Assert.Equal(saved.EventVariable0, recaptured.EventVariable0);
        Assert.Equal(saved.EventVariable1, recaptured.EventVariable1);
        Assert.Equal((ushort)0x8789, recaptured.OriginalAngles.ResolveBodyWord(6, 0x789));
        Assert.Equal((ushort)0x989A, recaptured.OriginalAngles.ResolveBodyWord(7, 0x89A));
        Assert.Equal((ushort)0xF9C5, recaptured.OriginalAngles.ResolveCameraWord(0x9C5));
    }
}
