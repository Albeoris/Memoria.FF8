using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesFamilyResources(ushort Effect, NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource NeedleVertices, NativeBattleModuleResource NeedleFaces, NativeBattleModuleResource SparkSprite, ImmutableArray<int> SparkDepthOffsets, uint Sound)
{
    [JsonIgnore]
    public NativeBattleNeedlesFamilyProfile Profile => NativeBattleNeedlesFamilyProfile.ForEffect(Effect);
    public void Validate()
    {
        NativeBattleNeedlesFamilyProfile profile = Profile;
        if (Camera.Identity != $"FF8_EN:{profile.CameraAddress:X}" || Camera.Data.IsDefault || Camera.Data.Length != profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != profile.CameraLength || Sound != 0x641A9 || SparkDepthOffsets.IsDefault || SparkDepthOffsets.Length != 7 || !SparkDepthOffsets.SequenceEqual(new[] { 0, 0, -2, 0, -3, -8, -4 }))
        {
            throw new InvalidDataException("Needles requires its original camera, sound and spark depth offsets.");
        }
        Require(NeedleVertices, profile.NeedleVerticesAddress, 32);
        Require(NeedleFaces, profile.NeedleFacesAddress, 80);
        Require(SparkSprite, profile.SparkSpriteAddress, 40);
        _ = SparkSprite.ProjectedFrame571C80(0);
    }
    private static void Require(NativeBattleModuleResource source, uint address, int length)
    {
        if (source.Address != address || source.Bytes.IsDefault || source.Bytes.Length != length)
        {
            throw new InvalidDataException("Needles requires its complete addressed drawing source.");
        }
    }
}
