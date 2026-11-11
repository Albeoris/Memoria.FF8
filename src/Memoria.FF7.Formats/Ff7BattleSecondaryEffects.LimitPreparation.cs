using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleLimitPreparationEntry(int Actor, int Bone) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleLimitPreparationEmitter(Ff7BattleBraverPoint Position) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleLimitPreparationMesh(Ff7BattleBraverPoint Position) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleLimitPreparationDraw(Ff7BattleBraverPoint Position, int Frame)
{
    public int TransverseScale => Frame * 0x600;
    public int VerticalScale => Frame < 8 ? Frame * 0xC00 : 0x6000;
    public int Transparency => Frame < 8 ? 0 : (Frame - 8) * 512;
}

public static partial class Ff7BattleSecondaryEffects
{
    public static Ff7BattleSecondaryState ScheduleE0(Ff7BattleSecondaryState state, int source, int resolvedBone)
    {
        ValidateActor(source);
        ArgumentOutOfRangeException.ThrowIfNegative(resolvedBone);
        return Schedule(state, new Ff7BattleLimitPreparationEntry(source, resolvedBone) { Age = 1 });
    }

    public static Ff7BattleSecondaryState ScheduleLimitPreparation(Ff7BattleSecondaryState state, Vector3 frozenSource)
    {
        return Schedule(state, new Ff7BattleLimitPreparationEmitter(Ff7BattleBraverPoint.At(frozenSource)));
    }

    private static Ff7BattleSecondaryCallback? ExecuteLimitPreparation(Ff7BattleSecondaryCallback callback, Action<Ff7BattleSecondaryCallback> allocate, Action<Ff7BattleLimitPreparationDraw> emit)
    {
        switch (callback)
        {
            case Ff7BattleLimitPreparationEmitter emitter:
                if ((emitter.Age & 3) == 0)
                {
                    allocate(new Ff7BattleLimitPreparationMesh(emitter.Position));
                }
                return emitter.Age == 12 ? null : emitter with { Age = emitter.Age + 1 };
            case Ff7BattleLimitPreparationMesh mesh:
                emit(new(mesh.Position, mesh.Age));
                return mesh.Age == 15 ? null : mesh with { Age = mesh.Age + 1 };
            default:
                throw new NotSupportedException("FFVII Limit preparation callback has no admitted executor.");
        }
    }
}
