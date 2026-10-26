using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    private static NativeBattleCpuTrailFrame CpuFrame(NativeBattleElectricDischarge126Frame frame) => new(frame.Memory, frame.Effect) { Staging = frame.Staging, BoneDirectionWord = frame.BoneDirectionWord };
    private static NativeBattleElectricDischarge126Frame WithCpuFrame(NativeBattleElectricDischarge126Frame frame, NativeBattleCpuTrailFrame owners) => frame with { Memory = owners.Memory, Effect = owners.Effect, Staging = owners.Staging, BoneDirectionWord = owners.BoneDirectionWord };
    private static NativeBattleCpuTrailLayout Layout(NativeBattleElectricDischarge126Frame frame) => new(Working(frame) + WorldPointsOffset, Working(frame) + WorldFreePointerOffset, Working(frame) + BonePointsOffset, Working(frame) + BoneFreePointerOffset, PointCount, 2, WorldTrailCallback, 3, BoneTrailCallback);

    public static (NativeBattleElectricDischarge126Frame Frame, uint Address) CreateBoneTrail(NativeBattleElectricDischarge126Frame frame, NativeBattleResourceRowReference binding, uint directory, int pathIndex, short steps, short scale, NativeBattleElectricDischarge126Resources resources)
    {
        NativeBattleCpuTrailAddressStep result = NativeBattleCpuTrails.CreateBoneTrail(CpuFrame(frame), Layout(frame), resources.ReadPathWord, binding, directory, pathIndex, steps, scale);
        return (WithCpuFrame(frame, result.Frame), result.Address);
    }

    public static (NativeBattleElectricDischarge126Frame Frame, uint Address) StartWorld(NativeBattleElectricDischarge126Frame frame, NativeBattleVector velocity, NativeBattleVector source, NativeBattleVector target, NativeBattleVector? drift, short actor, short progress, short increment, short scale, short textureV, short branches)
    {
        NativeBattleCpuTrailAddressStep result = NativeBattleCpuTrails.StartWorld(CpuFrame(frame), Layout(frame), velocity, source, target, drift, actor, progress, increment, scale, textureV, branches);
        return (WithCpuFrame(frame, result.Frame), result.Address);
    }

    private static (NativeBattleElectricDischarge126Frame Frame, uint Address) AllocateWorldPoint(NativeBattleElectricDischarge126Frame frame, NativeBattleVector position, short scale)
    {
        NativeBattleCpuTrailAddressStep result = NativeBattleCpuTrails.AllocateWorldPoint(CpuFrame(frame), Layout(frame), position, scale);
        return (WithCpuFrame(frame, result.Frame), result.Address);
    }

    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceBoneTrail(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        NativeBattleElectricDischarge126Frame current = frame;
        NativeBattleCpuTrailTaskStep result = NativeBattleCpuTrails.AdvanceBoneTrail(CpuFrame(frame), address, Layout(frame), resources.ReadPathWord, (owners, binding, head, count) =>
        {
            current = WithCpuFrame(current, owners);
            current = inputs.Execute(current, new NativeBattleElectricDischarge126DrawBoneTrail(binding, head, count));
            return CpuFrame(current);
        });
        return (WithCpuFrame(current, result.Frame), result.Result);
    }

    private static NativeBattleElectricDischarge126Frame ClearHeadVelocity(NativeBattleElectricDischarge126Frame frame, uint head)
    {
        if (head == 0)
        {
            return frame;
        }
        NativeBattleEffectArena source = frame.State.ReadView(TargetPointGlobal, 8);
        return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, Arena(frame), Offset(head + 8, 8), 8) } };
    }
}
