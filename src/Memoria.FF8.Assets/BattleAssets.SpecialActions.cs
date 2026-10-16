using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public BattleNativeEnemySpecial ReadNativeEnemySpecial(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            throw new InvalidDataException("Card and Devour require the original monster information directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 256)
        {
            throw new InvalidDataException("Card and Devour operands exceed their monster information resource.");
        }
        return new(bytes[information + 249], bytes[information + 250], bytes.AsSpan(information + 251, 3).ToArray().ToImmutableArray(), bytes[information + 255]);
    }

    public BattleNativeDevourEffect[] ReadNativeDevourEffects()
    {
        return battleKernel.DevourEffects.Select(effect => new BattleNativeDevourEffect(effect.Effect, effect.Quantity, effect.Statuses0, effect.Statuses1, effect.StatFlags, effect.Hp, effect.Description)).ToArray();
    }
}
