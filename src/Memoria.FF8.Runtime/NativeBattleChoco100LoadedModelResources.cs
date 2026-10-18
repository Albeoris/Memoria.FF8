using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100ModelYProgram(uint Address, ImmutableArray<short> Values);
public sealed record NativeBattleChoco100LoadedModelResources(NativeBattleEffectOwnedModelResource Model, uint DefaultColor44, uint DefaultFadeColor58, NativeBattleMoogle96TextureProgram TextureProgram, NativeBattleMoogle96TextureTable TextureTable, NativeBattleChoco100ModelYProgram YProgram)
{
    public void Validate()
    {
        Model.PoseSource.Validate();
        if (Model.Address != 0x19E8678 || Model.Bytes.IsDefault || Model.Bytes.Length != 0x5090 || Model.PoseSource.BoneCount != 17 || Model.Geometry.IsDefault || Model.Geometry.Length != 1 || Model.Geometry[0].Vertices.Length != 508 || !NativeBattlePoseDecoder.ReadFrameCounts(Model.PoseSource.Animations.AsSpan()).SequenceEqual(new byte[] { 1, 28, 1 }))
        {
            throw new InvalidDataException("Native Choco100 loaded model requires its complete actual file and skeleton/geometry/pose owners.");
        }
        Model.Geometry[0].Validate();
        if (TextureProgram.Address != 0x1529F38 || TextureProgram.Data.IsDefault || TextureProgram.Data.Length != 0x2C || TextureTable.Address != Model.Address + 0x5084 || TextureTable.Data.IsDefault || TextureTable.Data.Length != 12 || YProgram.Address != 0x1529F64 || YProgram.Values.IsDefault || YProgram.Values.Length != 6)
        {
            throw new InvalidDataException("Native Choco100 loaded model requires its actual texture and Y program extents.");
        }
    }
}
