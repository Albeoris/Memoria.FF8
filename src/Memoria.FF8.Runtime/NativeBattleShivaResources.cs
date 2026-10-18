using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaResources(NativeBattleEffectHeapAllocation CameraProgram, NativeVibrationBank Vibration, uint InitialAudioSelector, ImmutableArray<uint> Sounds, ImmutableArray<NativeBattleEffectFileAsset> Files, NativeBattleEffectHeapAllocation InitialVertexPairs, NativeBattleStaticMeshResource InitialMesh, NativeBattleEffectHeapAllocation BurstRowsD324B8)
{
    public required NativeBattleStaticMeshResource BurstMeshD3138C { get; init; }
    public required NativeBattleStaticMeshResource ShardMeshD2F1C4 { get; init; }
    public required NativeBattleModuleResource FirstSpriteD209F4 { get; init; }
    public required NativeBattleModuleResource SecondSpriteD20C7C { get; init; }
    public required NativeBattleModuleResource ParticleSpriteD20AD0 { get; init; }
    public required NativeBattleEffectOwnedModelResource Model544 { get; init; }
    public required ImmutableArray<int> TextureValuesD3250C { get; init; }
    public required ImmutableArray<int> ClippingVertexSelectorsD32524 { get; init; }

    public int CameraRandomDivisor => 48;
    public uint VibrationAddress => 0xD32344;

    public void Validate()
    {
        if (CameraProgram is null || CameraProgram.Data.IsDefault || CameraProgram.Data.Length != 0x13E || BinaryPrimitives.ReadUInt16LittleEndian(CameraProgram.Data.AsSpan().Slice(0x13C)) != 0x8000 || Vibration is null || Sounds.IsDefault || Sounds.Length != 4 || Files.IsDefault || Files.Length != 6 || InitialVertexPairs is null || InitialVertexPairs.Data.IsDefault || InitialVertexPairs.Data.Length != 0x250 || InitialMesh is null || InitialMesh.Address != 0xD215C4 || InitialMesh.Bytes.IsDefault || InitialMesh.Bytes.Length != 0x5D8 || BurstRowsD324B8 is null || BurstRowsD324B8.Data.IsDefault || BurstRowsD324B8.Data.Length != 64)
        {
            throw new InvalidDataException("Native Shiva resources require the complete camera, vibration, audio, streamed files and initial geometry sources.");
        }
        Vibration.Validate();
        if (BurstMeshD3138C is not { Address: 0xD3138C } || BurstMeshD3138C.Bytes.IsDefault || BurstMeshD3138C.Bytes.Length != 0x6E8 || ShardMeshD2F1C4 is not { Address: 0xD2F1C4 } || ShardMeshD2F1C4.Bytes.IsDefault || ShardMeshD2F1C4.Bytes.Length != 0x12C8 || FirstSpriteD209F4 is not { Address: 0xD209F4 } || FirstSpriteD209F4.Bytes.IsDefault || FirstSpriteD209F4.Bytes.Length != 0xDC || SecondSpriteD20C7C is not { Address: 0xD20C7C } || SecondSpriteD20C7C.Bytes.IsDefault || SecondSpriteD20C7C.Bytes.Length != 0x194)
        {
            throw new InvalidDataException("Native Shiva resources require their complete original burst, shard and sprite sources.");
        }
        FirstSpriteD209F4.Validate();
        SecondSpriteD20C7C.Validate();
        if (ParticleSpriteD20AD0 is not { Address: 0xD20AD0 } || ParticleSpriteD20AD0.Bytes.IsDefault || ParticleSpriteD20AD0.Bytes.Length != 0x1AC || Model544 is null || Model544.Address != NativeBattleEffectNodeMemory.ArenaAddress || Model544.Bytes.IsDefaultOrEmpty || Model544.PoseSource is null || Model544.Geometry.IsDefaultOrEmpty || TextureValuesD3250C.IsDefault || TextureValuesD3250C.Length != 6 || ClippingVertexSelectorsD32524.IsDefault || ClippingVertexSelectorsD32524.Length != 56 || ClippingVertexSelectorsD32524.Any(selector => selector is < 0 or > 5))
        {
            throw new InvalidDataException("Native Shiva resources require their actual particle sprite and arena-loaded544 model.");
        }
        ParticleSpriteD20AD0.Validate();
        Model544.PoseSource.Validate();
        foreach (NativeBattleRawMesh mesh in Model544.Geometry)
        {
            mesh.Validate();
        }
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleEffectFileAsset file = Files[index];
            if (file is null || file.FileId != 543 + index || string.IsNullOrEmpty(file.Name) || file.Data.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native Shiva streaming requires its six ordered original543..548 battle-file owners.");
            }
        }
        if (!Model544.Bytes.AsSpan().SequenceEqual(Files[1].Data.AsSpan()))
        {
            throw new InvalidDataException("Native Shiva model source differs from its original544 stream owner.");
        }
    }
}
