using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect70RingAnchorStep(NativeBattleEffectArena Output12, int Result);

public static class NativeBattleEffect70RingAnchor
{
    public static NativeBattleEffect70RingAnchorStep Resolve6DFC70(NativeBattleGraphicsModel model, NativeBattlePoseSource source, int selector, NativeBattleEffectArena retained12)
    {
        if ((uint)selector >= (uint)source.BoneCount)
        {
            throw new InvalidDataException("Effect70 ring anchor requires an actual current bone selector.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + selector * 48, 2));
        if (parent < 0)
        {
            return new(retained12, 1);
        }
        NativeBattleActionMatrix bone = NativeBattleBodyBoneMatrices.Require(model, source)[selector];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect70 ring anchor requires its current model world matrix.");
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(world, bone.X, bone.Y, bone.Z);
        NativeBattleEffectArena output = retained12.Write(0, BitConverter.GetBytes(unchecked(x / 2 + world.X))).Write(4, BitConverter.GetBytes(unchecked(y / 2 + world.Y))).Write(8, BitConverter.GetBytes(unchecked(z / 2 + world.Z)));
        return new(output, 0);
    }
}
