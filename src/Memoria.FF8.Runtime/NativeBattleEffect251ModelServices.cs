namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect251BoneWorldOutput(NativeBattleEffectArena Direction12, NativeBattleEffectArena Position8);

public static class NativeBattleEffect251ModelServices
{
    public static NativeBattleEffect251BoneWorldOutput BoneWorld5FC0(NativeBattleGraphicsModel model, NativeBattlePoseSource source, int selector, NativeBattleEffectArena direction12, NativeBattleEffectArena position8)
    {
        int bone = selector;
        if (selector >= 240)
        {
            int offset = checked(selector - 236);
            if ((uint)offset >= source.Skeleton.Length)
            {
                throw new InvalidDataException("Effect251 bone alias leaves its current skeleton owner.");
            }
            bone = source.Skeleton[offset];
        }
        var bones = NativeBattleBodyBoneMatrices.Require(model, source);
        if ((uint)bone >= bones.Length)
        {
            throw new InvalidDataException("Effect251 bone selector leaves its current physical rig.");
        }
        NativeBattleActionMatrix current = bones[bone];
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(current, 0, 0, -4096);
        (x, y, z) = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect251 requires its current model world matrix.");
        (x, y, z) = NativeBattleGfMatrices.RotateLong56C600(world, x, y, z);
        (x, y, z) = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        direction12 = direction12.Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z));
        NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(world, new(unchecked((short)current.X), unchecked((short)current.Y), unchecked((short)current.Z)));
        position8 = position8.Write(0, BitConverter.GetBytes(unchecked((short)(point.X + world.X)))).Write(2, BitConverter.GetBytes(unchecked((short)(point.Y + world.Y)))).Write(4, BitConverter.GetBytes(unchecked((short)(point.Z + world.Z))));
        return new(direction12, position8);
    }
}
