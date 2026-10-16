using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCameraMath
{
    /// <summary>56D130/56D100 with authenticated B6B980/B69540/B693D8 constants and the original biased-double conversion.</summary>
    public static int Sine(int angle) => FixedTrig(Math.Sin(angle * (double)0.0015339808f));
    public static int Cosine(int angle) => FixedTrig(Math.Cos(angle * (double)0.0015339808f));

    private static int FixedTrig(double value)
    {
        double biased = value * (double)4096f + 4503601774854144d;
        return unchecked((int)((uint)BitConverter.DoubleToInt64Bits(biased) + 0x80000000u));
    }

    /// <summary>503FE0 repeats fixed-point sine/cosine according to the signed low-five-bit easing exponent.</summary>
    public static int Ease(int fraction, ushort flags)
    {
        int exponent = unchecked((short)(flags << 10)) >> 11;
        if ((flags & 0x20) == 0)
        {
            for (int iteration = 0; iteration < exponent; iteration++)
            {
                fraction = Sine(fraction / 4);
            }
        }
        else
        {
            for (int iteration = 0; iteration < -exponent; iteration++)
            {
                fraction = 4096 - Cosine(fraction / 4);
            }
        }
        return fraction;
    }

    /// <summary>50D060 natural-cubic coefficients; each stored work/result element has native single precision.</summary>
    public static ImmutableArray<float> SplineCoefficients(IReadOnlyList<short> times, IReadOnlyList<short> values)
    {
        if (times.Count is < 3 or > 32 || times.Count != values.Count)
        {
            throw new InvalidDataException("Native camera spline requires three to thirty-two matching time/coordinate WORDs.");
        }
        int count = times.Count;
        float[] widths = new float[count - 1];
        float[] slopes = new float[count - 1];
        float[] diagonal = new float[count];
        float[] coefficients = new float[count];
        for (int index = 0; index < count - 1; index++)
        {
            widths[index] = times[index + 1] - times[index];
            slopes[index] = (float)((double)(values[index + 1] - values[index]) / widths[index]);
        }
        coefficients[1] = slopes[1] - slopes[0];
        diagonal[1] = 2 * (times[2] - times[0]);
        for (int index = 2; index < count - 1; index++)
        {
            double ratio = (double)widths[index - 1] / diagonal[index - 1];
            coefficients[index] = (float)((double)slopes[index] - slopes[index - 1] - ratio * coefficients[index - 1]);
            diagonal[index] = (float)(2 * (times[index + 1] - times[index - 1]) - ratio * widths[index - 1]);
        }
        for (int index = count - 2; index > 0; index--)
        {
            coefficients[index] = (float)(((double)coefficients[index] - widths[index] * (double)coefficients[index + 1]) / diagonal[index]);
        }
        return coefficients.ToImmutableArray();
    }

    /// <summary>50D240 selects the lower interval, evaluates its cubic and truncates through55AD90 before the WORD store.</summary>
    public static short SampleSpline(short time, IReadOnlyList<short> times, IReadOnlyList<short> values, IReadOnlyList<float> coefficients)
    {
        if (times.Count is < 3 or > 32 || times.Count != values.Count || times.Count != coefficients.Count)
        {
            throw new InvalidDataException("Native camera spline lacks its matching coefficient owner.");
        }
        int high = times.Count - 1;
        int low = 0;
        while (low < high)
        {
            int middle = (high + low) / 2;
            if (times[middle] >= time)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        int index = Math.Max(0, low - 1);
        double width = times[index + 1] - times[index];
        double elapsed = time - times[index];
        double start = coefficients[index];
        double end = coefficients[index + 1];
        double slope = (values[index + 1] - values[index]) / width - (2 * start + end) * width;
        double value = (((end - start) * elapsed / width + 3 * start) * elapsed + slope) * elapsed + values[index];
        return unchecked((short)(long)value);
    }
}
