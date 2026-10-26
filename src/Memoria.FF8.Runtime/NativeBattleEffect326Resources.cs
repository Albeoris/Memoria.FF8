using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect326Resources(NativeBattleEffectHeapAllocation Texture, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableArray<uint> Sounds, uint StreamNumber)
{
    public required NativeBattleEffectOwnedModelResource MainModel { get; init; }
    public required NativeBattleEffectOwnedModelResource EmbeddedModel { get; init; }
    public required NativeBattleEffectOwnedModelResource LateModel { get; init; }
    public required ImmutableArray<byte> ScrollPixels { get; init; }
    public required ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts { get; init; }

    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? resource) ? resource : throw new NotSupportedException("Effect326 selected an unsupported mesh owner.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? resource) ? resource : throw new NotSupportedException("Effect326 selected an unsupported projected source.");
    public NativeBattleEffectFileAsset File(short id) => Files.SingleOrDefault(file => file.FileId == id) ?? throw new NotSupportedException("Effect326 selected an unsupported file.");

    public byte[] ReadStatic(uint address, int length)
    {
        if (length >= 0 && address >= PixelSource && (ulong)address + (uint)length <= PixelSource + 0x4000u)
        {
            return ScrollPixels.AsSpan(checked((int)(address - PixelSource)), length).ToArray();
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        foreach (NativeBattleStaticMeshResource mesh in Meshes.Values)
        {
            if (length >= 0 && address >= mesh.Address && (ulong)address + (uint)length <= (ulong)mesh.Address + (uint)mesh.Bytes.Length)
            {
                return mesh.Bytes.AsSpan(checked((int)(address - mesh.Address)), length).ToArray();
            }
        }
        throw new NotSupportedException("Effect326 static read leaves its selected source owners.");
    }

    public void Validate()
    {
        if (Texture is null || Texture.Data.IsDefault || Texture.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Sounds.IsDefault || Sounds.Length != 6 || Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length)
        {
            throw new InvalidDataException("Effect326 requires its selected texture, camera, vibration, sound and geometry owners.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(Texture.Data.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(Texture.Data.AsSpan().Slice(4)) != 9)
        {
            throw new InvalidDataException("Effect326 selected resource headers differ from their current consumers.");
        }
        if (Files.IsDefault || Files.Length != 8 || Files.Select(file => file.FileId).Distinct().Count() != Files.Length)
        {
            throw new InvalidDataException("Effect326 requires its ordered async file owners.");
        }
        for (int index = 0; index < Files.Length; index++)
        {
            NativeBattleEffectFileAsset file = Files[index];
            if (file.FileId != 737 + index || file.Data.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(file.Name))
            {
                throw new InvalidDataException("Effect326 file leaves its actual selected catalog entry.");
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = Mesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index])
            {
                throw new InvalidDataException("Effect326 mesh leaves its selected source owner.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != SpriteFrames[index])
            {
                throw new InvalidDataException("Effect326 projected source leaves its selected frame directory.");
            }
            for (int frame = 0; frame < SpriteFrames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (MainModel is null || MainModel.Address != ArenaAddress || !MainModel.Bytes.AsSpan().SequenceEqual(Files[1].Data.AsSpan()) || EmbeddedModel is null || EmbeddedModel.Address != EmbeddedModelAddress || EmbeddedModel.Bytes.Length != EmbeddedModelLength || LateModel is null || LateModel.Address != PixelSource || !LateModel.Bytes.AsSpan().SequenceEqual(Files[7].Data.AsSpan()) || ScrollPixels.IsDefault || ScrollPixels.Length != 0x4000 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect326 requires its distinct main, embedded and later loaded model owners and raw pixels.");
        }
        foreach (NativeBattleEffectOwnedModelResource model in new[] { MainModel, EmbeddedModel, LateModel })
        {
            model.PoseSource.Validate();
            if (model.Geometry.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Effect326 model requires its actual geometry objects.");
            }
            foreach (NativeBattleRawMesh mesh in model.Geometry)
            {
                mesh.Validate();
            }
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }
}
