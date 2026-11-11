namespace Memoria.FF7.Formats;

public abstract record Ff7BattleTailLaserSecondary : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleTailLaserSecondarySprite(int Resource, Ff7BattleBraverPoint Position) : Ff7BattleTailLaserSecondary;
public sealed record Ff7BattleTailLaserSecondarySpark(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, short Yaw, short Pitch) : Ff7BattleTailLaserSecondary;

public static partial class Ff7BattleSecondaryEffects
{
    public static Ff7BattleSecondaryState ScheduleTailLaser(Ff7BattleSecondaryState state, Ff7BattleTailLaserSecondary callback) => Schedule(state, callback);

    private static Ff7BattleSecondaryCallback? ExecuteTailLaser(Ff7BattleTailLaserSecondary callback, Action<Ff7BattleTailLaserSprite> emit)
    {
        switch (callback)
        {
            case Ff7BattleTailLaserSecondarySprite sprite:
                emit(new(sprite.Resource, sprite.Position, sprite.Age, 8192));
                return sprite.Age == 7 ? null : sprite with { Age = sprite.Age + 1 };
            case Ff7BattleTailLaserSecondarySpark spark:
                Ff7BattleSearchScopeBasis basis = Ff7BattleEffectMatrix.RotateXyz(new(0, spark.Yaw, (short)(spark.Pitch + 1024)));
                emit(new(11, spark.Position, 0, 24576, (byte)(128 - (spark.Age << 4)), Basis: basis));
                return spark.Age == 7 ? null : spark with { Age = spark.Age + 1, Position = Ff7BattleEffectMatrix.Add(spark.Position, spark.Velocity) };
            default:
                throw new InvalidDataException("Unknown FFVII Tail Laser secondary callback.");
        }
    }
}
