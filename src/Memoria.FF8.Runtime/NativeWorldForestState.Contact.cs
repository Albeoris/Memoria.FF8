namespace Memoria.FF8.Runtime;

public sealed record NativeWorldContactSpark(NativeWorldPathPosition Body, short DifferenceX, short DifferenceZ);

public readonly record struct NativeWorldContactSparkResult(NativeWorldProcess Process, NativeWorldContactSpark? Spark);

public static class NativeWorldContactSparkEmitter
{
    public static NativeWorldContactSparkResult Select(NativeWorldProcess process, int primaryContact, int denseCount, NativeWorldContactSpark? candidate)
    {
        process.Validate();
        if (denseCount < 0 || primaryContact < -1)
        {
            throw new InvalidDataException("Native contact spark requires its retained dense contact result.");
        }
        bool valid = primaryContact >= 0 && primaryContact < denseCount;
        if (valid && candidate is not null && process.PreviousContactC76784 is null)
        {
            throw new NotSupportedException("Native contact spark requires its preceding process contact; migrate this save explicitly.");
        }
        NativeWorldContactSpark? spark = valid && primaryContact != process.PreviousContactC76784 ? candidate : null;
        return new(process with { PreviousContactC76784 = primaryContact }, spark);
    }
}

public sealed partial record NativeWorldForestState
{
    private void EmitContactSpark(List<NativeWorldForestParticle> particles, NativeWorldContactSpark spark, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects, ref byte cursor, ref byte offset)
    {
        NativeWorldForestEffect effect = effects.SingleOrDefault(effect => effect.Type == 21) ?? throw new NotSupportedException("Native contact spark requires its complete original type21 effect record.");
        short yaw = unchecked((short)NativePrecision53.RoundInteger(Math.Atan2(spark.DifferenceX, spark.DifferenceZ) * (double)BitConverter.Int32BitsToSingle(0x4422F983)));
        NativeWorldPathPosition position = spark.Body with { Y = unchecked(spark.Body.Y - 200) };
        EmitParticle(particles, effect, position, 0, yaw, 0, 0, random, ref cursor, ref offset);
    }
}
