using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordParticleResources([property: JsonRequired] NativeBattleStaticMeshResource Mesh, [property: JsonRequired] NativeBattleModuleResource Sprite, [property: JsonRequired] ImmutableArray<int> SwordOrder)
{
    public void Validate()
    {
        if (Mesh is null || Mesh.Bytes.IsDefault || Mesh.Bytes.Length != 0xF8 || BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan()) != 56 || BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan().Slice(4)) != 6 || Sprite is null || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0x194 || Sprite.Kind != 10 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 15 || SwordOrder.IsDefault || !SwordOrder.AsSpan().SequenceEqual(new int[] { 0, 3, 1, 2 }))
        {
            throw new InvalidDataException("Native Gilgamesh sword particles require their complete mesh, fifteen-frame sprite and original sword ordering.");
        }
        int cursor = 56;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Mesh.Bytes.AsSpan().Slice(cursor));
            if (count != (group == 4 ? 8 : 0))
            {
                throw new InvalidDataException("Native Gilgamesh sword particle mesh requires its eight original triangle faces.");
            }
            cursor += 4 + count * 20;
        }
        for (int phase = 0; phase < 15; phase++)
        {
            _ = Sprite.ProjectedFrame571C80(phase);
        }
    }
}
