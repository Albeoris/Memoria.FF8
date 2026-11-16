using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Guest model cells and CPU section addresses for party owners without original model slots.</summary>
internal sealed class NativeBattleHostedGuestModel
{
    private const uint FirstAddress = 0xF1000000;
    private const uint SlotSize = 0x100000;
    private const uint CpuOffset = 0x1000;
    private const int WorldOffset = 0x40;
    private const int MatrixSize = 32;
    private const int FirstBoneMatrix = 32;
    private const int BoneStride = 48;

    private readonly NativeBattleModelCpuReference skeleton;
    private readonly NativeBattleModelResourceView skeletonBytes;
    private readonly ImmutableArray<NativeBattleActionMatrix> bones;
    private readonly NativeBattleEffectArena modelBytes;
    private readonly uint cpuAddress;

    private NativeBattleHostedGuestModel(uint address, NativeBattleModelCpuReference skeleton, NativeBattleModelResourceView skeletonBytes,
        ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleEffectArena modelBytes)
    {
        ModelAddress = address;
        this.skeleton = skeleton;
        this.skeletonBytes = skeletonBytes;
        this.bones = bones;
        this.modelBytes = modelBytes;
        cpuAddress = checked(address + CpuOffset);
    }

    internal uint ModelAddress { get; }

    internal static bool TryResolveBank(uint address, out int bank)
    {
        if (address is >= FirstAddress and < NativeBattleModelResourceRowAddress.HostedBase)
        {
            bank = checked(NativeBattlePresentationCpuBanks.FirstResourceBank + (int)((address - FirstAddress) / SlotSize));
            return true;
        }
        bank = -1;
        return false;
    }

    internal static NativeBattleHostedGuestModel Open(NativeBattleModelCpuResources cpu, NativeBattleModelTextureCache cache, NativeBattleFileStaging staging,
        NativeBattleGraphicsState graphics, BattlePartyModelOwner owner, NativeBattlePoseSource pose, Func<NativeBattleGraphicsModel, NativeBattleEffectArena> readModel)
    {
        if (cpu.Cpu.PresentationRoster?.Contains(owner) != true || graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out _) ||
            !cpu.Cpu.PartyBankOwners.TryGetValue(owner, out int bank) || !NativeBattlePresentationCpuBanks.IsExtension(bank))
        {
            throw new InvalidDataException("Hosted guest placement requires an unaliased party actor and its assigned CPU bank.");
        }
        ulong address = FirstAddress + (ulong)(bank - NativeBattlePresentationCpuBanks.FirstResourceBank) * SlotSize;
        if (address + SlotSize > NativeBattleModelResourceRowAddress.HostedBase ||
            NativeBattlePresentationTaskStorage.IsHostedBank((uint)address, 16, 1) || NativeBattleGraphicsModelAddress.TryResolveOriginalField((uint)address, out _, out _))
        {
            throw new InvalidDataException("Hosted guest model and CPU bank must fit their reserved address range.");
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        if ((model.Flags0 & 2) == 0 || model.GeometryWorldMatrix is null || model.ActiveStagedResourceRow100 is not null || model.ActiveResourceRow100 is not int row ||
            (uint)row >= cpu.Rows.Length || (uint)row >= cache.Rows.Length || cache.Rows[row] is not { Kind: 1 } material || material.ModelId != model.ModelId4 ||
            cache.PresentationActors?.GetValueOrDefault(row) != owner)
        {
            throw new InvalidDataException("Hosted guest placement requires its current loaded model and occupied body resource row.");
        }
        NativeBattleModelCpuResourceRow body = cpu.Rows[row];
        NativeBattleModelCpuReference skeleton = body.Require(12);
        if (!skeleton.IsPresentationResource || skeleton.Bank != bank || !body.Lengths.TryGetValue(12, out int extent) ||
            extent < pose.Skeleton.Length || (ulong)CpuOffset + (uint)skeleton.Offset + (uint)extent > SlotSize)
        {
            throw new InvalidDataException("Hosted guest skeleton must belong to its actor's current CPU allocation and fit its address window.");
        }
        NativeBattleModelResourceView bytes = NativeBattleModelResourceView.Open(cpu, staging, skeleton, extent);
        return new((uint)address, skeleton, bytes, NativeBattleBodyBoneMatrices.Require(model, pose), readModel(model));
    }

    internal uint RequireWorldMatrixAddress()
    {
        uint address = checked(ModelAddress + WorldOffset);
        RequireMatrixKnown(Read(address, MatrixSize));
        return address;
    }

    internal uint RequireBoneMatrixAddress(NativeBattleResourcePointer matrix)
    {
        if (matrix.Cpu is not { } reference || matrix.CpuPublication is not null || matrix.CpuRow is not null || matrix.StagingAddress is not null || matrix.Length != MatrixSize)
        {
            throw new InvalidDataException("Hosted guest matrix requires its current CPU section reference.");
        }
        int relative = checked(reference.Offset - skeleton.Offset);
        int boneOffset = relative - FirstBoneMatrix;
        if (boneOffset < 0 || boneOffset % BoneStride != 0 || boneOffset / BoneStride >= bones.Length || reference != skeleton.Advance(relative))
        {
            throw new InvalidDataException("Hosted guest matrix is outside the current skeleton publication.");
        }
        uint address = checked(cpuAddress + (uint)reference.Offset);
        RequireMatrixKnown(Read(address, MatrixSize));
        return address;
    }

    internal NativeBattleEffectArena Read(uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        if (address >= ModelAddress && (ulong)address + (uint)length <= (ulong)ModelAddress + NativeBattleGraphicsModelAddress.RowSize && address - ModelAddress < NativeBattleGraphicsModelAddress.RowSize)
        {
            return NativeBattleEffectNodeContextMemory.Copy(modelBytes, checked((int)(address - ModelAddress)), result, 0, length);
        }
        uint first = checked(cpuAddress + (uint)skeleton.Offset);
        if (address < first || address - first >= skeletonBytes.Length || (ulong)address + (uint)length > (ulong)first + (uint)skeletonBytes.Length)
        {
            throw new InvalidDataException("Hosted guest read leaves its current model cell or skeleton section.");
        }
        int offset = checked((int)(address - first));
        (byte[] bytes, bool[] known) = skeletonBytes.ReadSnapshot(offset, length);
        for (int bone = Math.Max(0, (offset - FirstBoneMatrix) / BoneStride); bone < bones.Length; bone++)
        {
            int matrixStart = checked(FirstBoneMatrix + bone * BoneStride);
            if (matrixStart >= offset + length)
            {
                break;
            }
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(bones[bone], null);
            int begin = Math.Max(offset, matrixStart);
            int end = Math.Min(offset + length, matrixStart + MatrixSize);
            for (int source = begin; source < end; source++)
            {
                if (matrix.Known[source - matrixStart])
                {
                    bytes[source - offset] = matrix.Bytes[source - matrixStart];
                    known[source - offset] = true;
                }
            }
        }
        return result.WriteWithKnownness(0, bytes, known);
    }

    private static void RequireMatrixKnown(NativeBattleEffectArena matrix)
    {
        (_, bool[] known) = matrix.ReadSnapshot(0, MatrixSize);
        if (known.AsSpan(0, 18).Contains(false) || known.AsSpan(20, 12).Contains(false))
        {
            throw new InvalidDataException("Hosted guest placement requires current matrix rotation and translation bytes.");
        }
    }
}
