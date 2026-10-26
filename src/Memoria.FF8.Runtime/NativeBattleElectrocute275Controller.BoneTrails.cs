using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    private static NativeBattleCpuTrailFrame CpuTrailFrame(NativeBattleElectrocute275Frame frame) => new(frame.Memory, frame.Effect) { Staging = frame.Staging, BoneDirectionWord = frame.BoneDirectionWord };
    private static NativeBattleElectrocute275Frame WithCpuTrail(NativeBattleElectrocute275Frame frame, NativeBattleCpuTrailFrame current) => frame with { Memory = current.Memory, Effect = current.Effect, Staging = current.Staging, BoneDirectionWord = current.BoneDirectionWord };
    private static NativeBattleCpuTrailLayout CpuTrailLayout(NativeBattleElectrocute275Frame frame) => new(Working(frame) + WorldPointsOffset, Working(frame) + WorldFreePointerOffset, Working(frame) + BonePointsOffset, Working(frame) + BoneFreePointerOffset, PointCount, 2, 0x6887B0, 3, 0x689280);

    public static (NativeBattleElectrocute275Frame Frame, uint Address) CreateBoneTrail(NativeBattleElectrocute275Frame frame, uint bindingAddress, uint pathDirectoryAddress, int pathIndex, short steps, short scale, NativeBattleElectrocute275Resources resources)
    {
        NativeBattleCpuTrailAddressStep step = NativeBattleCpuTrails.CreateBoneTrail(CpuTrailFrame(frame), CpuTrailLayout(frame), address => BonePathWord(resources, address), new(null, bindingAddress), pathDirectoryAddress, pathIndex, steps, scale);
        return (WithCpuTrail(frame, step.Frame), step.Address);
    }

    public static (NativeBattleElectrocute275Frame Frame, uint Address) AllocateBonePoint(NativeBattleElectrocute275Frame frame, short bone, short vertex, short width, bool jitter, short intensity, short fade)
    {
        NativeBattleCpuTrailAddressStep step = NativeBattleCpuTrails.AllocateBonePoint(CpuTrailFrame(frame), CpuTrailLayout(frame), bone, vertex, width, jitter, intensity, fade);
        return (WithCpuTrail(frame, step.Frame), step.Address);
    }

    public static (NativeBattleElectrocute275Frame Frame, int Result) AdvanceBoneTrail(NativeBattleElectrocute275Frame frame, uint address, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        NativeBattleElectrocute275Frame current = frame;
        NativeBattleCpuTrailTaskStep step = NativeBattleCpuTrails.AdvanceBoneTrail(CpuTrailFrame(frame), address, CpuTrailLayout(frame), pointer => BonePathWord(resources, pointer), (owners, binding, head, count) =>
        {
            current = WithCpuTrail(current, owners);
            uint stagingAddress = binding.StagingAddress ?? throw new NotSupportedException("Electrocute requires its actual staged binding address.");
            current = inputs.Execute(current, new NativeBattleElectrocute275DrawBoneTrail(stagingAddress, head, count));
            return CpuTrailFrame(current);
        }, frame.BoneDirectionWord);
        return (WithCpuTrail(current, step.Frame), step.Result);
    }

    private static short BonePathWord(NativeBattleElectrocute275Resources resources, uint address)
    {
        uint offset = unchecked(address - BonePathsAddress);
        if (resources.BonePaths.IsDefault || offset > resources.BonePaths.Length - 2)
        {
            throw new InvalidDataException("Electrocute bone path leaves its original addressed source.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(resources.BonePaths.AsSpan().Slice(checked((int)offset), 2));
    }

}
