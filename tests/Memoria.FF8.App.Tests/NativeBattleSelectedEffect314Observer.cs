using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleSelectedEffect314Observer
{
    private readonly HashSet<int> ages = [];
    private readonly HashSet<string> boundaries = [];
    private readonly List<object> observations = [];
    private int maximumFragments;
    private int maximumSparks;
    private bool terminal;

    private int capturedPrimitives;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        capturedPrimitives += NativeBattleRecovery32CaptureAssertions.AssertFrame(current, 314);
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return;
        }
        NativeBattleModulePoolTask? before = FindMainTask(prior, true);
        NativeBattleModulePoolTask? after = FindMainTask(current, false);
        if (before is null)
        {
            return;
        }
        int age = before.ReadWord(12);
        Assert.True(ages.Add(age), "The effect314 main task age executed twice.");
        Assert.InRange(age, 0, 226);
        if (age < 226)
        {
            Assert.NotNull(after);
            Assert.Equal(unchecked((ushort)(age + 1)), after.ReadWord(12));
            int amplitude = age < 8 ? age * 125 : age > 218 ? (226 - age) * 125 : 1000;
            Assert.Equal(unchecked((uint)amplitude), current.NativeEffectMemory!.Effect314!.ReadDword(AmplitudeGlobal));
        }
        int fragments = Count(after, FragmentOffset + 6, FragmentStride, FragmentCapacity);
        int sparks = Count(after, SparkOffset + 6, SparkStride, SparkCapacity);
        maximumFragments = Math.Max(maximumFragments, fragments);
        maximumSparks = Math.Max(maximumSparks, sparks);
        if (age == 226)
        {
            terminal = true;
            uint caster = current.NativeEffectMemory!.Effect314!.ReadDword(CasterGlobal);
            int physical = checked((int)((caster - 0x1D972C0) / 156));
            Assert.Equal(before.ReadDword(0x14), current.NativeGraphics.Models[physical].Color44);
            Assert.Equal(before.ReadDword(0x18), current.NativeGraphics.Models[physical + 1].Color44);
            Assert.Equal(0, current.NativeGraphics.Models[physical].Flags0 & 8);
            Assert.Equal(0, current.NativeGraphics.Models[physical + 1].Flags0 & 8);
        }
        observations.Add(new { current.Frame, ProcessedAge = age, Fragments = fragments, Sparks = sparks, LocalRandomBefore = prior.NativeEffectMemory!.Effect314!.ReadDword(RandomGlobal), LocalRandomAfter = current.NativeEffectMemory!.Effect314!.ReadDword(RandomGlobal), CrtBefore = prior.NativeCrtRandom!.Seed, CrtAfter = current.NativeCrtRandom!.Seed, RootAge = effect.Pools.IsEmpty ? (int?)null : effect.Pools[0].Slots[0].ReadWord(12) });
        if (age is 16 or 66 or 100 or 140 or 210 or 226)
        {
            Save($"effect314-age{age:D3}-before", prior);
            Save($"effect314-age{age:D3}-after", current);
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            if (boundaries.Add(name))
            {
                cold(name, snapshot);
            }
        }
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect314-native-observations.json"), JsonSerializer.Serialize(new { ProcessedAges = ages.Order(), Boundaries = boundaries.Order(), MaximumFragments = maximumFragments, MaximumSparks = maximumSparks, Terminal = terminal, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(capturedPrimitives > 0, "The complete standalone route did not observe owned moving geometry.");
        NativeBattleRecovery32CaptureAssertions.AssertFrame(completed, 314);
        Assert.True(terminal, "The effect314 main task must execute its native cleanup age.");
        Assert.Contains(66, ages);
        Assert.Contains(100, ages);
        Assert.Contains(210, ages);
        Assert.Equal(12, boundaries.Count);
        Assert.True(maximumSparks > 0, "The original effect314 opening or particle burst must create actual sparks.");
        Publish(output);
    }

    private static NativeBattleModulePoolTask? FindMainTask(BattleSnapshot snapshot, bool active)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return null;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            if (pool.DescriptorAddress == MainDescriptor && pool.Slots.Length == 1)
            {
                NativeBattleModulePoolTask slot = pool.Slots[0];
                return !active || (slot.Flags & 1) != 0 ? slot : null;
            }
        }
        return null;
    }

    private static int Count(NativeBattleModulePoolTask? task, int offset, int stride, int capacity)
    {
        if (task is null)
        {
            return 0;
        }
        int count = 0;
        for (int index = 0; index < capacity; index++)
        {
            if (task.ReadWord(offset + index * stride) != 0)
            {
                count++;
            }
        }
        return count;
    }
}
