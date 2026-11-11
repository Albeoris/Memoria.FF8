namespace Memoria.FF7.Formats;

public sealed record Ff7BattleWaveSpark(Ff7BattleBraverPoint Position) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleWaveSparkDraw(Ff7BattleBraverPoint Position, int Frame);

public static partial class Ff7BattleSecondaryEffects
{
    public static Ff7BattleSecondaryState ScheduleWaveSpark(Ff7BattleSecondaryState state, Ff7BattleBraverPoint position)
    {
        return Schedule(state, new Ff7BattleWaveSpark(position));
    }

    private static Ff7BattleSecondaryCallback? ExecuteWaveSpark(Ff7BattleWaveSpark spark, Action<Ff7BattleWaveSparkDraw> emit)
    {
        emit(new(spark.Position, spark.Age));
        return spark.Age == 7 ? null : spark with { Age = spark.Age + 1 };
    }
}
