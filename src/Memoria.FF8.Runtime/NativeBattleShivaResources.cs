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
    public required NativeBattleModuleResource ModelParticleSpriteD208B0 { get; init; }
    public required NativeBattleStaticMeshResource AnchorMeshD31A74 { get; init; }
    public required NativeBattleStaticMeshResource RingMeshD3048C { get; init; }
    public required NativeBattleStaticMeshResource RingMeshD309A4 { get; init; }
    public required NativeBattleStaticMeshResource ChildMeshD21B9C { get; init; }
    public required NativeBattleStaticMeshResource ScrollMeshD2A50C { get; init; }
    public required NativeBattleEffectHeapAllocation SpawnRowsD324F8 { get; init; }
    public required NativeBattleStaticMeshResource FinalMeshD257CC { get; init; }
    public required NativeBattleStaticMeshResource FinalMeshD27E6C { get; init; }
    public required NativeBattleModuleResource LateParticleSpriteD20E10 { get; init; }
    public required NativeBattleStaticMeshResource SecondShatterMeshD2BA3C { get; init; }
    public required NativeBattleStaticMeshResource SecondShatterMeshD2D600 { get; init; }

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
        if (ModelParticleSpriteD208B0 is not { Address: 0xD208B0 } || ModelParticleSpriteD208B0.Bytes.IsDefault || ModelParticleSpriteD208B0.Bytes.Length != 0x144 || SpawnRowsD324F8 is null || SpawnRowsD324F8.Data.IsDefault || SpawnRowsD324F8.Data.Length != 16)
        {
            throw new InvalidDataException("Native Shiva model particles require their original sprite and four spawn parameter rows.");
        }
        ModelParticleSpriteD208B0.Validate();
        RequireMesh(AnchorMeshD31A74, 0xD31A74, 0x8D0);
        RequireMesh(RingMeshD3048C, 0xD3048C, 0x518);
        RequireMesh(RingMeshD309A4, 0xD309A4, 0x9E8);
        RequireMesh(ChildMeshD21B9C, 0xD21B9C, 0x3C30);
        RequireMesh(ScrollMeshD2A50C, 0xD2A50C, 0x1530);
        RequireMesh(FinalMeshD257CC, 0xD257CC, 0x26A0);
        RequireMesh(FinalMeshD27E6C, 0xD27E6C, 0x26A0);
        RequireMesh(SecondShatterMeshD2BA3C, 0xD2BA3C, 0x1BC4);
        RequireMesh(SecondShatterMeshD2D600, 0xD2D600, 0x1BC4);
        if (LateParticleSpriteD20E10 is not { Address: 0xD20E10 } || LateParticleSpriteD20E10.Bytes.IsDefault || LateParticleSpriteD20E10.Bytes.Length != 0x1AC)
        {
            throw new InvalidDataException("Native Shiva late particles require their complete original sprite source.");
        }
        LateParticleSpriteD20E10.Validate();
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

    private static void RequireMesh(NativeBattleStaticMeshResource mesh, uint address, int length)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length)
        {
            throw new InvalidDataException("Native Shiva requires each complete original selected mesh source.");
        }
    }
}
