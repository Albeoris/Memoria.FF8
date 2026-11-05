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

    public static NativeBattleResourcePointer ResolveRecord(NativeBattleResourcePointer skeleton, int boneCount, int bone)
    {
        RequireSkeleton(skeleton);
        if ((uint)bone >= (uint)boneCount || boneCount <= 0)
        {
            throw new NotSupportedException("The bone cache requires a bone within its current skeleton.");
        }
        int offset = checked(16 + bone * 48);
        if (skeleton.Length is int length && offset > length - 48)
        {
            throw new NotSupportedException("The bone cache record leaves its selected skeleton extent.");
        }
        // The record selects the current actor pose; it does not borrow immutable CPU bytes.
        return skeleton with
        {
            Cpu = skeleton.Cpu is { } cpu ? cpu with { Offset = checked(cpu.Offset + offset) } : null,
            StagingAddress = skeleton.StagingAddress is uint address ? checked(address + (uint)offset) : null,
            CpuPublication = null,
            Length = skeleton.Length is int available ? available - offset : null
        };
    }

    public static int ResolveIndex(NativeBattleResourcePointer skeleton, int boneCount, NativeBattleResourcePointer record)
    {
        RequireSkeleton(skeleton);
        RequireSkeleton(record);
        if (record.CpuPublication is not null)
        {
            throw new NotSupportedException("A current-pose bone cache cannot select an immutable CPU publication.");
        }
        long offset;
        if (skeleton.Cpu is { } owner && record.Cpu is { } selected && owner.Bank == selected.Bank && owner.ResourceOnly == selected.ResourceOnly && owner.EffectiveLayout == selected.EffectiveLayout)
        {
            offset = (long)selected.Offset - owner.Offset - 16;
        }
        else if (skeleton.StagingAddress is uint basis && record.StagingAddress is uint address)
        {
            offset = (long)address - basis - 16;
        }
        else
        {
            throw new NotSupportedException("The bone cache record belongs to another skeleton owner or address domain.");
        }
        if (offset < 0 || offset % 48 != 0 || offset / 48 >= boneCount || boneCount <= 0 || skeleton.Length is int length && offset + 16 > length - 48 || record.Length is < 48)
        {
            throw new NotSupportedException("The bone cache requires its actual current bone-record coordinate.");
        }
        return checked((int)(offset / 48));
    }

    private static void RequireSkeleton(NativeBattleResourcePointer pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        if (!pointer.HasValidOwner() || pointer.CpuRow is not null)
        {
            throw new NotSupportedException("The bone cache requires a selected skeleton section owner.");
        }
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
