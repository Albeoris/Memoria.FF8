using System.Buffers.Binary;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Assets.Tests;

public sealed class GuardianForceSourceInitializationTests
{
    [Fact]
    public void SourceIncludesKernelRulesAndIndependentInitOutDefaults()
    {
        int[] lengths = new int[56];
        lengths[1] = 60 * 57;
        lengths[2] = 132 * 16;
        lengths[6] = 36 * 11;
        int[] counts = [20, 19, 19, 20, 5, 9, 24];
        for (int index = 0; index < counts.Length; index++)
        {
            lengths[11 + index] = counts[index] * 8;
        }
        byte[] kernel = new byte[228 + lengths.Sum()];
        BinaryPrimitives.WriteUInt32LittleEndian(kernel, 56);
        int offset = 228;
        int[] offsets = new int[56];
        for (int index = 0; index < lengths.Length; index++)
        {
            offsets[index] = offset;
            BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + index * 4), (uint)offset);
            offset += lengths[index];
        }
        BinaryPrimitives.WriteUInt32LittleEndian(kernel.AsSpan(4 + 56 * 4), (uint)offset);
        for (int id = 0; id < 11; id++)
        {
            kernel[offsets[6] + id * 36 + 9] = 1;
        }
        for (int id = 0; id < 16; id++)
        {
            kernel[offsets[2] + id * 132 + 21] = 1;
            kernel[offsets[2] + id * 132 + 28] = 1;
            kernel[offsets[2] + id * 132 + 29] = 255;
            kernel[offsets[2] + id * 132 + 30] = 1;
        }
        byte[] initial = new byte[2812];
        for (int id = 0; id < 8; id++)
        {
            initial[1088 + id * 152 + 8] = (byte)id;
            initial[1088 + id * 152 + 148] = 1;
        }
        initial[2724] = 0;
        initial[2725] = 255;
        initial[2726] = 255;
        initial[20] = 4;
        initial[36] = 9;
        initial[64] = 1;
        initial[65] = 2;
        NativeCharacterVitals source = NativeCharacterInitialization.Read(initial, kernel);
        Assert.Contains(source.MenuDefinitions.SourceTemplates[0], entry => entry.Ability == 1 && !entry.InitialLearned);
        Assert.Contains(source.MenuDefinitions.SourceTemplates[0], entry => entry.Ability == 2 && entry.InitialLearned);
        NativeGuardianForceInitialState defaults = source.MenuDefinitions.InitialStates[0];
        Assert.True(defaults.LearnedBank.Has(2));
        Assert.Equal((byte)9, defaults.AbilityProgress[0]);
        Assert.Equal((byte)1, defaults.Learning);
        Assert.Equal(2u, defaults.ForgottenAbilities);
        Assert.Null(source.GuardianForces[0].Progression);
    }
}
