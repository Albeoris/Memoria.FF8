using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleGraphicsResource ReadNativeGraphicsResource(BattleCombatantDefinition actor)
    {
        if (actor.IsPlayer)
        {
            BattleModel body = LoadPlayer(actor.ModelId, actor.Costume);
            return LoadPlayerSequences(actor.ModelId, actor.WeaponId, body).NativeResource;
        }
        return ReadEnemyNativeGraphicsResource(actor.ModelId);
    }

    public NativeBattleGraphicsResource ReadEnemyNativeGraphicsResource(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections != 11)
        {
            throw new NotSupportedException("A native graphics model requires its original eleven-section DAT.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16));
        int end = Enumerable.Range(0, sections).Select(index => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + 4 * index))).Where(offset => offset > start).DefaultIfEmpty(bytes.Length).Min();
        NativeBattleGraphicsResource resource = new(bytes.AsSpan(start, end - start).ToArray().ToImmutableArray());
        resource.Validate();
        return resource;
    }
}
