using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect187Resources(NativeBattleEffectHeapAllocation Texture, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableArray<uint> Sounds, uint StreamNumber)
{
    public required NativeBattleEffectOwnedModelResource OwnedModel { get; init; }
    public required ImmutableArray<byte> ScrollPixels { get; init; }
    public required ImmutableArray<byte> ScrollRectangle { get; init; }

    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? resource) ? resource : throw new NotSupportedException("Effect187 selected an unsupported original mesh.");

    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? resource) ? resource : throw new NotSupportedException("Effect187 selected an unsupported projected source.");

    public NativeBattleEffectFileAsset File(short id) => Files.SingleOrDefault(file => file.FileId == id) ?? throw new NotSupportedException("Effect187 selected an unsupported original file.");

    public void Validate()
    {
        if (Texture is null || Texture.Data.IsDefault || Texture.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Sounds.IsDefault || Sounds.Length != 3 || Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length)
        {
            throw new InvalidDataException("Effect187 requires its complete selected texture, camera, vibration, sound and geometry resources.");
        }
        if (Dword(Texture.Data, 0) != 16 || Dword(Texture.Data, 4) != 9 || Dword(Vibration.Data, 0) != 0 || Dword(Vibration.Data, 4) != 256 || Word(Vibration.Data, 256) != 78 || Word(Vibration.Data, 258) != 4)
        {
            throw new InvalidDataException("Effect187 resource headers differ from their current consumers.");
        }
        if (Files.IsDefault || Files.Length != 6 || Files.Select(file => file.FileId).Distinct().Count() != Files.Length || Files.Any(file => file.Data.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(file.Name)))
        {
            throw new InvalidDataException("Effect187 requires its actual ordered async file resources.");
        }
        for (int index = 0; index < 6; index++)
        {
            if (Files[index].FileId != 0x225 + index)
            {
                throw new InvalidDataException("Effect187 async file identifiers changed.");
            }
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = Mesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index])
            {
                throw new InvalidDataException("Effect187 mesh leaves its selected source owner.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrames[index])
            {
                throw new InvalidDataException("Effect187 projected source leaves its selected frame domain.");
            }
            for (int frame = 0; frame < SpriteFrames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        if (OwnedModel is null || OwnedModel.Address != NativeBattleEffectNodeMemory.ArenaAddress || OwnedModel.Bytes.IsDefault || !OwnedModel.Bytes.AsSpan().SequenceEqual(Files[1].Data.AsSpan()) || OwnedModel.PoseSource is null || OwnedModel.Geometry.IsDefaultOrEmpty || ScrollPixels.IsDefault || ScrollPixels.Length != 0x4000 || ScrollRectangle.IsDefault || ScrollRectangle.Length != 8)
        {
            throw new InvalidDataException("Effect187 requires its current effect-owned model and scrolling texture source.");
        }
        OwnedModel.PoseSource.Validate();
        foreach (NativeBattleRawMesh mesh in OwnedModel.Geometry)
        {
            mesh.Validate();
        }
        _ = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Camera.Data, 0);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
