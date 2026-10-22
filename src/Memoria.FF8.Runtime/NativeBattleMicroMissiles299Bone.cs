using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMicroMissiles299BoneStep(NativeBattleMicroMissiles299Frame Frame, int Result, NativeBattleVector? Point);

public static class NativeBattleMicroMissiles299Bone
{
    public static NativeBattleMicroMissiles299BoneStep Query67E550(NativeBattleMicroMissiles299Frame frame, NativeBattleGraphicsModel model, NativeBattlePoseSource source, int selector)
    {
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Micro Missiles bone selection requires its actual physical temporary memory.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 20);
        source.Validate();
        int bone = selector;
        if (selector >= 240)
        {
            int alias = checked(selector - 236);
            if ((uint)alias >= 16)
            {
                throw new InvalidDataException("Micro Missiles bone alias leaves its original skeleton header.");
            }
            bone = source.Skeleton[alias];
        }
        if ((uint)bone >= source.BoneCount)
        {
            throw new InvalidDataException("Micro Missiles bone selector leaves its current physical skeleton.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        if (parent < 0)
        {
            return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(allocation.Staging, 20) }, 1, null);
        }
        NativeBattleActionMatrix current = NativeBattleBodyBoneMatrices.Require(model, source)[bone];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Micro Missiles bone selection requires its retained actor world matrix.");
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(world, current.X, current.Y, current.Z);
        byte[] xyz = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(xyz, x);
        BinaryPrimitives.WriteInt32LittleEndian(xyz.AsSpan(4), y);
        BinaryPrimitives.WriteInt32LittleEndian(xyz.AsSpan(8), z);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 20), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 20), 0, 20).Write(0, xyz);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 20);
        NativeBattleVector point = new(unchecked((short)(world.X + x)), unchecked((short)(world.Y + y)), unchecked((short)(world.Z + z)));
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 20) }, 0, point);
    }
}
