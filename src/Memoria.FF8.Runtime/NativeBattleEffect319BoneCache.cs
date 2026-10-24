namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect319BoneCache
{
    public static uint ResolveRecord(uint skeletonAddress, int boneCount, int bone)
    {
        if ((uint)bone >= (uint)boneCount || boneCount <= 0)
        {
            throw new NotSupportedException("Effect319 requires a bone within its current skeleton.");
        }
        return checked(skeletonAddress + 0x10u + (uint)(bone * 48));
    }

    public static int ResolveIndex(uint skeletonAddress, int boneCount, uint recordAddress)
    {
        long offset = (long)recordAddress - skeletonAddress - 0x10;
        if (offset < 0 || offset % 48 != 0 || offset / 48 >= boneCount || boneCount <= 0)
        {
            throw new NotSupportedException("Effect319 requires its actual current bone-record pointer.");
        }
        return checked((int)(offset / 48));
    }

    public static NativeBattleGraphicsModel ScaleCurrent56BEF0(NativeBattleGraphicsModel model, int bone, short xy, short z)
    {
        NativeBattleBodyBoneMatrices current = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect319 requires its current mutable bone publication.");
        current.Validate();
        if ((uint)bone >= (uint)current.Matrices.Length)
        {
            throw new NotSupportedException("Effect319 selected a matrix outside its current bone publication.");
        }
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(current.Matrices[bone], xy, xy, z);
        return model with { CurrentBodyBoneMatrices = current with { Matrices = current.Matrices.SetItem(bone, scaled) } };
    }
}
