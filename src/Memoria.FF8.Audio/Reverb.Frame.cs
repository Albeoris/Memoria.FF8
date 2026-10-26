// Sample adapters for the MIT MeltySynth Freeverb implementation. See ThirdParty/MeltySynth/LICENSE.txt.
namespace MeltySynth;

internal sealed partial class Reverb
{
    internal void ProcessFrame(float input, out float left, out float right)
    {
        input *= InputGain;
        left = 0;
        right = 0;
        foreach (CombFilter filter in cfsL)
        {
            left += filter.ProcessSample(input);
        }
        foreach (CombFilter filter in cfsR)
        {
            right += filter.ProcessSample(input);
        }
        foreach (AllPassFilter filter in apfsL)
        {
            left = filter.ProcessSample(left);
        }
        foreach (AllPassFilter filter in apfsR)
        {
            right = filter.ProcessSample(right);
        }
        float first = left;
        left = first * wet1 + right * wet2;
        right = right * wet1 + first * wet2;
    }

    internal sealed partial class CombFilter
    {
        internal float ProcessSample(float input)
        {
            if (bufferIndex == buffer.Length)
            {
                bufferIndex = 0;
            }
            float output = buffer[bufferIndex];
            if (MathF.Abs(output) < 1.0E-6F)
            {
                output = 0;
            }
            filterStore = output * damp2 + filterStore * damp1;
            if (MathF.Abs(filterStore) < 1.0E-6F)
            {
                filterStore = 0;
            }
            buffer[bufferIndex++] = input + filterStore * feedback;
            return output;
        }
    }

    internal sealed partial class AllPassFilter
    {
        internal float ProcessSample(float input)
        {
            if (bufferIndex == buffer.Length)
            {
                bufferIndex = 0;
            }
            float output = buffer[bufferIndex];
            if (MathF.Abs(output) < 1.0E-6F)
            {
                output = 0;
            }
            buffer[bufferIndex++] = input + output * feedback;
            return output - input;
        }
    }
}
