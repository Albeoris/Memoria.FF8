using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Selected original model sources and initializer TIM, separate from mutable model and stream owners.</summary>
public sealed record NativeBattleGilgameshModelResources([property: JsonRequired] NativeBattleEffectHeapAllocation InitializerTim, [property: JsonRequired] NativeBattleEffectOwnedModelResource StreamModel746, [property: JsonRequired] NativeBattleEffectOwnedModelResource StreamModel751, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelResource> Stream749Models, [property: JsonRequired] ImmutableArray<NativeBattleEffectOwnedModelResource> StaticModels)
{
    public void Validate()
    {
        if (InitializerTim is null || InitializerTim.Identity != "mag326-329.tim" || InitializerTim.Data.IsDefault || InitializerTim.Data.Length != 167136)
        {
            throw new InvalidDataException("Native Gilgamesh initializer requires its complete original named TIM.");
        }
        ReadOnlySpan<byte> tim = InitializerTim.Data.AsSpan();
        if (U32(tim, 0) != 0x10 || U32(tim, 4) != 9 || U32(tim, 8) != 3276 || U16(tim, 12) != 320 || U16(tim, 14) != 250 || U16(tim, 16) != 272 || U16(tim, 18) != 6 || U32(tim, 3284) != 163852 || U16(tim, 3288) != 384 || U16(tim, 3290) != 256 || U16(tim, 3292) != 320 || U16(tim, 3294) != 256)
        {
            throw new InvalidDataException("Native Gilgamesh initializer TIM contradicts its original CLUT and image block extents.");
        }
        Model(StreamModel746, 0x20DFAB8, [0x18, 0x478, 0x4E34, 0x77E0, 0x77E0], 23, [70]);
        Model(StreamModel751, 0x20DFAB8, [0x18, 0xD18, 0xAE9C, 0xC5A0, 0xC5A0], 69, [50]);
        if (Stream749Models.IsDefault || Stream749Models.Length != 5 || StaticModels.IsDefault || StaticModels.Length != 4)
        {
            throw new InvalidDataException("Native Gilgamesh requires all five streamed submodels and four selected static models.");
        }
        int[] offsets = [0, 6044, 12284, 18524, 24764];
        for (int index = 0; index < Stream749Models.Length; index++)
        {
            Model(Stream749Models[index], checked(0x2101AB8u + (uint)offsets[index]), index == 0 ? [0x18, 0xB8, 0x1648, 0x179C, 0x179C] : [0x18, 0x418, 0xD78, 0x1860, 0x1860], index == 0 ? 3 : 21, index == 0 ? [60] : [60, 60, 60, 60]);
        }
        uint[] addresses = [0xE808D0, 0xE81934, 0xE82808, 0xE839F4];
        int[] geometryEnds = [0x73C, 0x5AC, 0x8C4, 0x8C4];
        int[] lengths = [0x1064, 0xED4, 0x11EC, 0x11EC];
        for (int index = 0; index < StaticModels.Length; index++)
        {
            Model(StaticModels[index], addresses[index], [0x18, 0x88, geometryEnds[index], lengths[index], lengths[index]], 2, [70, 70, 70, 70, 50, 50, 50, 50]);
        }
    }

    private static void Model(NativeBattleEffectOwnedModelResource model, uint address, int[] sections, int bones, byte[] frameCounts)
    {
        if (model is null || model.Address != address || model.Bytes.IsDefault || model.Bytes.Length != sections[^1] || U32(model.Bytes.AsSpan(), 0) != 4 || model.PoseSource is null || model.Geometry.IsDefault || model.Geometry.Length != 1)
        {
            throw new InvalidDataException("Native Gilgamesh model requires its complete selected four-section DAT and one packed geometry object.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (model.Section(index) != sections[index])
            {
                throw new InvalidDataException("Native Gilgamesh model directory contradicts its complete original source extent.");
            }
        }
        model.PoseSource.Validate();
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(model.PoseSource.Animations.AsSpan());
        if (model.PoseSource.BoneCount != bones || !counts.AsSpan().SequenceEqual(frameCounts) || !model.PoseSource.Skeleton.AsSpan().SequenceEqual(model.Bytes.AsSpan().Slice(sections[0], sections[1] - sections[0])) || !model.PoseSource.Animations.AsSpan().SequenceEqual(model.Bytes.AsSpan().Slice(sections[2], sections[3] - sections[2])))
        {
            throw new InvalidDataException("Native Gilgamesh model pose owners contradict their actual DAT sections.");
        }
        model.Geometry[0].Validate();
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset));

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset));
}
