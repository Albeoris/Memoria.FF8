using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeModelEffectTests
{
    public static void Run()
    {
        int[] counts = [16, 42, 16, 33];
        Ff7NativeModelEffectState requested = Ff7NativeModelEffect.Request(null, 0, counts);
        Check(Enumerable.Range(0, 4).All(part => Ff7NativeModelEffect.PartVisible(requested, part)), "An unprocessed request hid the native model.");
        Check(!requested.SuppressesNativeLighting && Ff7NativeModelEffect.Advance(requested, false) == requested, "The rejected native processing gate changed presentation.");
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Advance(requested, true);
        Check(state.Initialized && !state.Applied && Enumerable.Range(0, 4).All(part => !Ff7NativeModelEffect.PartVisible(state, part)), "The initialization frame did not hide all original rigid parts.");
        Check(Ff7NativeModelEffect.Request(state, 1, counts) == state, "Replacing nonreset payload0 with1 reset the persistent native effect.");
        for (int tick = 0; tick < 100; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state.Stage == 1 && state.Counter == 100 && state.Phase == 0 && state.SampleFrame == 0, "Initial native100-frame hold changed.");
        Check(!Ff7NativeModelEffect.PartVisible(state, 0) && Ff7NativeModelEffect.PartVisible(state, 1) && !Ff7NativeModelEffect.PartVisible(state, 2) && Ff7NativeModelEffect.PartVisible(state, 3), "Native topology-selected rigid-part visibility changed.");
        for (int tick = 0; tick < 18; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state.Stage == 2 && state.Phase == 288 && state.SampleFrame == 17, "The first pulse did not sample before incrementing phase.");
        Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
        Check(Ff7NativeModelEffect.Advance(cold, true) == Ff7NativeModelEffect.Advance(state, true), "Cold continuation changed the next pulse sample.");
        Check(Ff7NativeModelEffect.Advance(state, false) == state, "An invisible/out-of-depth effect advanced its native phase.");
        for (int tick = 0; tick < 10; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state.Stage == 3 && state.Phase == 0 && state.SampleFrame == 18, "Native10-frame endpoint hold changed.");
        for (int tick = 0; tick < 18; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state.Stage == 0 && state.Phase == 288 && state.Counter == 0, "The second pulse did not retain its endpoint for the next hold.");
        state = Ff7NativeModelEffect.Request(state, 2, counts);
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.Restored && !state.Completed && !state.SuppressesNativeLighting && Enumerable.Range(0, 4).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "Reset did not immediately restore original parts and lighting.");
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.Completed, "Reset completed before/after the native next processed frame.");
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [36, 36, 16, 42]));
        Throws<InvalidDataException>(() => (requested with { Phase = 1 }).Validate());
        short[] table = new short[512];
        table[1] = 4096;
        table[128] = 4096;
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 0, table) == new Vector3(64, 0, 16), "Native orbit start changed.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 4, table) == new Vector3(0, -64, 144), "Native orbit quarter turn or arithmetic shifts changed.");
        uint bright = Ff7NativeModelEffect.ShadeVertex(requested, 0, table, Vector3.Zero, Vector3.UnitX, 0x7FFFFFFF, Matrix4x4.Identity);
        uint dark = Ff7NativeModelEffect.ShadeVertex(requested, 0, table, Vector3.Zero, -Vector3.UnitX, 0x7FFFFFFF, Matrix4x4.Identity);
        uint ambient = Ff7NativeModelEffect.ShadeVertex(requested, 0, table, Vector3.Zero, Vector3.Zero, 0x7F808080, Matrix4x4.Identity);
        Check(bright == 0x7FFFFFFF && dark == 0x7FC0C0C0 && ambient == 0x7F303030, "Native cached light modulation, original-color tint or alpha changed.");
        RunSinglePart(table);
        RunStep32(table);
        RunSingle54(table);
        RunSingle80_76();
        RunFourPartDefault();
        RunFourPart35_36();
        RunTwoOrThreeParts();
        RunInstalled();
    }

    private static void RunSinglePart(short[] table)
    {
        Ff7NativeModelEffectState single = Ff7NativeModelEffect.Request(null, 0, [35]);
        Ff7NativeModelEffectState four = Ff7NativeModelEffect.Request(null, 0, [16, 42, 16, 33]);
        Check(single.Profile == Ff7NativeModelEffect.SinglePart35Profile, "The native one-part35 topology selected the wrong profile.");
        Check(Ff7NativeModelEffect.OrbitPoint(single, 0, table) == new Vector3(32, 0, 16), "The one-part35 native XY orbit shift changed.");
        Check(Ff7NativeModelEffect.OrbitPoint(single, 4, table) == new Vector3(0, -32, 80), "The one-part35 native Z orbit shift changed.");
        uint distant = Ff7NativeModelEffect.ShadeVertex(single, 0, table, new(-600, 0, 0), Vector3.UnitX, 0xFFFFFFFF, Matrix4x4.Identity);
        Check(distant == 0xFFC0C0C0, "The one-part35 native512 light radius changed.");
        for (int tick = 0; tick < 320; tick++)
        {
            bool eligible = tick % 7 != 0;
            single = Ff7NativeModelEffect.Advance(single, eligible);
            four = Ff7NativeModelEffect.Advance(four, eligible);
            Check(single.Phase == four.Phase && single.Counter == four.Counter && single.Stage == four.Stage && single.SampleFrame == four.SampleFrame, "The one-part35 profile changed the shared native phase clock.");
            Check(Ff7NativeModelEffect.PartVisible(single, 0) == (!single.Initialized || single.Applied), "The one-part35 initialization/application visibility changed.");
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(single))!;
            Check(Ff7NativeModelEffect.Advance(cold, true) == Ff7NativeModelEffect.Advance(single, true), "The one-part35 cold next sample changed.");
        }
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.Request(single, 1, [16, 42, 16, 33]));
        single = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(single, 2, [35]), true);
        Check(single.Restored && !single.Completed && Ff7NativeModelEffect.PartVisible(single, 0) && !single.SuppressesNativeLighting, "The one-part35 reset failed to restore the original model.");
        Check(Ff7NativeModelEffect.Advance(single, true).Completed, "The one-part35 reset completion clock changed.");
        Ff7NativeModelEffectState defaultSingle = Ff7NativeModelEffect.Request(null, 0, [34]);
        Check(defaultSingle.Profile == Ff7NativeModelEffect.SinglePartDefaultProfile, "The reached one-part34 topology selected the wrong native defaults.");
        Check(Ff7NativeModelEffect.Request(null, 0, [37]).Profile == defaultSingle.Profile, "An asymmetric nonexception topology did not select native defaults.");
        Check(Ff7NativeModelEffect.Request(null, 0, [112], partPolygonCounts: [63]).Profile == defaultSingle.Profile, "A conditional exception's nonmatching polygon count did not retain native defaults.");
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [112], partPolygonCounts: [64]));
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [112]));
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.Request(null, 0, [0]));
        Check(Ff7NativeModelEffect.OrbitPoint(defaultSingle, 0, table) == new Vector3(256, 0, 0), "The one-part34 default orbit start changed.");
        Check(Ff7NativeModelEffect.OrbitPoint(defaultSingle, 4, table) == new Vector3(0, -256, 128), "The one-part34 default orbit shifts or zero Z offset changed.");
        Check(Ff7NativeModelEffect.ShadeVertex(defaultSingle, 0, table, new(-1500, 0, 0), Vector3.UnitX, 0xFFFFFFFF, Matrix4x4.Identity) == 0xFFFFFFFF, "The one-part34 default2048 light radius changed.");
        for (int tick = 0; tick < 320; tick++)
        {
            defaultSingle = Ff7NativeModelEffect.Advance(defaultSingle, tick % 7 != 0);
            Check(Ff7NativeModelEffect.PartVisible(defaultSingle, 0) == (!defaultSingle.Initialized || defaultSingle.Applied), "The one-part34 visibility owner changed.");
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(defaultSingle))!;
            Check(Ff7NativeModelEffect.Advance(cold, true) == Ff7NativeModelEffect.Advance(defaultSingle, true), "The one-part34 cold continuation changed.");
        }
    }

    private static void RunStep32(short[] table)
    {
        Ff7NativeModelEffectState requested = Ff7NativeModelEffect.Request(null, 0, [72], 400, [140]);
        Check(requested.Profile == Ff7NativeModelEffect.SinglePart72_140Profile, "The native72/140 topology did not select its step32 profile.");
        Check(Ff7NativeModelEffect.Request(null, 0, [72], 400, [139]).Profile == Ff7NativeModelEffect.SinglePartDefaultProfile, "A nonmatching polygon count selected the native exception.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 2, table) == new Vector3(0, -256, 128), "Step32 cache indices must multiply by32 before orbit lookup.");
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.OrbitPoint(requested, 10, table));
        Throws<InvalidDataException>(() => (requested with { SampleFrame = 10 }).Validate());
        Throws<InvalidDataException>(() => (requested with { Phase = 16 }).Validate());
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Advance(requested, true);
        Check(!Ff7NativeModelEffect.PartVisible(state, 0), "Create must hide the single original part for its initialization frame.");
        for (int tick = 0; tick < 100; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state.Stage == 1 && state.Phase == 0, "Step32 changed the native initial100-update hold.");
        for (int index = 0; index < 9; index++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == index, "Native step32 must sample phase/32 before increment.");
        }
        Check(state.Stage == 2 && state.Phase == 288 && state.SampleFrame == 8, "Step32 ramp length or cap changed.");
        for (int tick = 0; tick < 10; tick++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == 9, "Native endpoint hold must use cache index9.");
        }
        Check(state.Stage == 3 && state.Phase == 0, "The native ten-update endpoint hold changed.");
        for (int index = 0; index < 9; index++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == index, "Native second ramp cache sequence changed.");
        }
        Check(state.Stage == 0 && state.Phase == 288, "The second ramp must retain phase288 into the later100-update hold.");
        for (int tick = 0; tick < 180; tick++)
        {
            bool eligible = tick % 7 != 0;
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
            Check(Ff7NativeModelEffect.Advance(state, eligible) == Ff7NativeModelEffect.Advance(cold, eligible), "Step32 cold continuation changed its next native sample.");
            state = Ff7NativeModelEffect.Advance(state, eligible);
        }
        state = Ff7NativeModelEffect.Request(state, 2, [72], 400, [140]);
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.Restored && !state.Completed, "Step32 reset did not restore before completion.");
        Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Step32 reset did not complete on its second processed update.");
    }

    private static void RunSingle80_76()
    {
        Ff7NativeModelEffectState requested = Ff7NativeModelEffect.Request(null, 0, [80], 512, [76]);
        Check(requested.Profile == Ff7NativeModelEffect.SinglePart80_76Profile, "The native80/76 topology must select its step12 profile.");
        Check(Ff7NativeModelEffect.Request(null, 0, [80], 512, [75]).Profile == Ff7NativeModelEffect.SinglePartDefaultProfile, "The native polygon predicate must retain defaults for a nonmatching topology.");
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [80]));
        short[] table = new short[512];
        table[1] = 4096;
        table[24] = 4095;
        table[25] = -4095;
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 0, table) == new Vector3(32, 0, 0), "Native80/76 must use XY shift7 and no Z offset.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 1, table) == new Vector3(-32, -32, 31), "Native80/76 must use phase12, arithmetic shifts7 and signed short coordinates.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested with { NativeScale = 1024 }, 1, table) == new Vector3(-64, -64, 62), "Native80/76 must retain the shared scale multiplier.");
        Check(Ff7NativeModelEffect.ShadeVertex(requested, 0, table, new Vector3(1500, 0, 0), -Vector3.UnitX, 0x7FFFFFFF, Matrix4x4.Identity) == 0x7FC0C0C0, "Native80/76 must retain radius1024 and source alpha.");
        for (int frame = 0; frame <= 24; frame++)
        {
            _ = Ff7NativeModelEffect.OrbitPoint(requested, frame, table);
        }
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.OrbitPoint(requested, 25, table));
        Throws<InvalidDataException>(() => (requested with { Phase = 16 }).Validate());
        Throws<InvalidDataException>(() => (requested with { SampleFrame = 25 }).Validate());
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Advance(requested, true);
        Check(!Ff7NativeModelEffect.PartVisible(state, 0), "Native80/76 Create must hide its original rigid part.");
        for (int callback = 0; callback < 100; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 1, Phase: 0, Counter: 100 }, "Native80/76 must retain the initial100-callback hold.");
        for (int sample = 0; sample < 24; sample++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == sample, "Native80/76 must select its cache before advancing phase12.");
        }
        Check(state is { Stage: 2, Phase: 288, SampleFrame: 23 }, "Native80/76 must reach its endpoint after24 ramp callbacks.");
        for (int callback = 0; callback < 10; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == 24, "Native80/76 endpoint hold must select the25th cache sample.");
        }
        for (int callback = 0; callback < 24; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 0, Phase: 288, Counter: 0 }, "Native80/76 must retain its endpoint into the next hold.");
        Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
        Check(Ff7NativeModelEffect.Advance(cold, true) == Ff7NativeModelEffect.Advance(state, true), "Native80/76 cold continuation changed the next cache sample.");
        Check(Ff7NativeModelEffect.Advance(state, false) == state && Ff7NativeModelEffect.PartVisible(state, 0), "Native80/76 visibility or rejected-depth freeze changed.");
        state = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(state, 2, [80], 512, [76]), true);
        Check(state.Restored && !state.Completed && !state.SuppressesNativeLighting, "Native80/76 reset must restore original colors before completion.");
        Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Native80/76 reset must complete on the following eligible callback.");
    }

    private static void RunSingle54(short[] table)
    {
        Ff7NativeModelEffectState requested = Ff7NativeModelEffect.Request(null, 0, [54]);
        Check(requested.Profile == Ff7NativeModelEffect.SinglePart54Profile && Ff7NativeModelEffect.Request(null, 1, [54], 512, [1]) == requested, "The native single54 branch must ignore polygon counts and preserve equivalent request modes.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested, 0, table) == new Vector3(256, 0, 0) && Ff7NativeModelEffect.OrbitPoint(requested, 2, table) == new Vector3(0, -256, 8), "Single54 must retain XY shifts4, Z shift9 and zero Z offset.");
        Check(Ff7NativeModelEffect.OrbitPoint(requested with { NativeScale = 1024 }, 2, table) == new Vector3(0, -512, 16), "Single54 must use the shared signed native scale contract.");
        Check(Ff7NativeModelEffect.ShadeVertex(requested, 0, table, new Vector3(1500, 0, 0), -Vector3.UnitX, 0x7FFFFFFF, Matrix4x4.Identity) == 0x7FC0C0C0, "Single54 must use radius1024 rather than the default2048 light falloff.");
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Advance(requested, true);
        Check(!Ff7NativeModelEffect.PartVisible(state, 0), "Single54 initialization must hide its part before the first cached application.");
        for (int callback = 0; callback < 16; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 1, Counter: 16, Phase: 0, SampleFrame: 0 }, "Single54 must leave its initial hold after16 eligible callbacks.");
        for (int sample = 0; sample < 9; sample++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == sample, "Single54 must sample before advancing its step32 phase.");
        }
        Check(state is { Stage: 2, Phase: 288, Counter: 0 }, "Single54 must reach phase288 in nine ramp callbacks.");
        for (int callback = 0; callback < 10; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == 9, "Single54 endpoint hold must retain cache9 for ten callbacks.");
        }
        Check(state is { Stage: 3, Phase: 0 }, "Single54 endpoint hold must reset only the next ramp phase.");
        for (int sample = 0; sample < 9; sample++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == sample, "Single54 second ramp changed its cache order.");
        }
        Check(state is { Stage: 0, Phase: 288, Counter: 0 }, "Single54 must retain the endpoint into its next short hold.");
        for (int callback = 0; callback < 16; callback++)
        {
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
            Check(Ff7NativeModelEffect.Advance(cold, false) == state && Ff7NativeModelEffect.Advance(cold, true) == Ff7NativeModelEffect.Advance(state, true), "Single54 must freeze under its draw gate and preserve exact cold continuation.");
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(state.SampleFrame == 9 && Ff7NativeModelEffect.PartVisible(state, 0), "The subsequent short hold must display endpoint colors on the original part.");
        }
        Check(state is { Stage: 1, Phase: 0, Counter: 16 }, "Single54 recurring hold must also last16 callbacks.");
        Throws<InvalidDataException>(() => (state with { Phase = 16 }).Validate());
        Throws<InvalidDataException>(() => (state with { SampleFrame = 10 }).Validate());
        Throws<InvalidDataException>(() => (state with { Counter = 17 }).Validate());
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.OrbitPoint(state, 10, table));
        state = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(state, 2, [54]), true);
        Check(state.Restored && !state.Completed && Ff7NativeModelEffect.PartVisible(state, 0) && !state.SuppressesNativeLighting, "Single54 reset must restore ordinary visibility and lighting before completion.");
        Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Single54 reset must complete on the next eligible callback.");
    }

    private static void RunFourPartDefault()
    {
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Request(null, 0, [70, 78, 12, 16], 512, [72, 72, 8, 28]);
        Check(state.Profile == Ff7NativeModelEffect.FourPartDefaultProfile, "The reached four-part topology did not select original defaults.");
        Check(Ff7NativeModelEffect.Request(null, 0, [71, 79, 13, 17]).Profile == state.Profile, "A generic nonexception four-part topology did not retain native defaults.");
        Check(Ff7NativeModelEffect.Request(null, 0, [70, 78, 12, 16], partPolygonCounts: [1, 2, 3, 4]).Profile == state.Profile, "The native four-part classifier incorrectly used polygon counts.");
        Check(Ff7NativeModelEffect.Request(null, 0, [35, 36, 12, 16]).Profile == Ff7NativeModelEffect.FourPart35_36Profile, "The native first-count pair must select its distinct step24 profile independently of the remaining parts.");
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [36, 36, 12, 16]));
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(Enumerable.Range(0, 4).All(part => !Ff7NativeModelEffect.PartVisible(state, part)), "Create did not hide every four-part default component.");
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(Enumerable.Range(0, 4).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "Default Apply did not enable all four components.");
        Ff7NativeModelEffectState reference = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(null, 0, [37]), true), true);
        for (int update = 0; update < 320; update++)
        {
            bool eligible = update % 17 != 0;
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
            Ff7NativeModelEffectState expected = Ff7NativeModelEffect.Advance(state, eligible);
            Check(expected == Ff7NativeModelEffect.Advance(cold, eligible), "Default four-part cold phase/sample diverged.");
            state = expected;
            reference = Ff7NativeModelEffect.Advance(reference, eligible);
            Check(state.Phase == reference.Phase && state.Counter == reference.Counter && state.Stage == reference.Stage && state.SampleFrame == reference.SampleFrame, "Default four-part phase clock diverged from the same native default clock.");
        }
        short[] directions = new short[512];
        directions[1] = 4096;
        Check(Ff7NativeModelEffect.OrbitPoint(state, 0, directions) == new Vector3(256, 0, 0), "Default four-part orbit changed its native shift or offset.");
        state = Ff7NativeModelEffect.Request(state, 2, [70, 78, 12, 16]);
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.Restored && Enumerable.Range(0, 4).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "Default four-part reset did not restore all original components.");
        Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Default four-part reset did not complete on its second processed callback.");
    }

    private static void RunFourPart35_36()
    {
        int[] vertices = [35, 36, 36, 40];
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Request(null, 0, vertices, 384, [18, 50, 56, 20]);
        Check(state.Profile == Ff7NativeModelEffect.FourPart35_36Profile && Ff7NativeModelEffect.Request(null, 1, vertices, 384, [1, 2, 3, 4]).Profile == state.Profile, "The native35/36 pair must select the step24 profile without consulting polygon counts.");
        Check(Ff7NativeModelEffect.Advance(state, false) == state, "An ineligible native depth gate must preserve the new profile's complete owner.");
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.Initialized && !state.Applied && Enumerable.Range(0, 4).All(part => !Ff7NativeModelEffect.PartVisible(state, part)), "Step24 initialization must hide all original rigid parts.");
        for (int callback = 0; callback < 100; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 1, Phase: 0, Counter: 100 } && !Ff7NativeModelEffect.PartVisible(state, 0) && Ff7NativeModelEffect.PartVisible(state, 1) && Ff7NativeModelEffect.PartVisible(state, 2) && !Ff7NativeModelEffect.PartVisible(state, 3), "Native35/36 Apply must preserve its100-callback hold and hide only parts0/3.");
        for (int callback = 0; callback < 12; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 2, Phase: 288, Counter: 0, SampleFrame: 11 }, "Step24 must reach its native endpoint after twelve pre-advance cache selections.");
        for (int callback = 0; callback < 10; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 3, Phase: 0, SampleFrame: 12 }, "The endpoint hold must display cache12 before the native second ramp.");
        for (int callback = 0; callback < 12; callback++)
        {
            state = Ff7NativeModelEffect.Advance(state, true);
        }
        Check(state is { Stage: 0, Phase: 288, Counter: 0, SampleFrame: 11 }, "The second native step24 ramp must return to the long hold after twelve callbacks.");
        state = Ff7NativeModelEffect.Advance(state, true);
        Check(state.SampleFrame == 12, "The following long hold must reuse the endpoint cache instead of resetting its presentation phase.");
        for (int callback = 0; callback < 320; callback++)
        {
            Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
            bool eligible = callback % 17 != 0;
            Check(Ff7NativeModelEffect.Advance(state, eligible) == Ff7NativeModelEffect.Advance(cold, eligible), "Step24 independent JSON continuation must preserve every gated phase/cache field.");
            state = Ff7NativeModelEffect.Advance(state, eligible);
        }
        short[] directions = new short[512];
        directions[1] = 4096;
        directions[144] = 4096;
        directions[64] = -4096;
        Check(Ff7NativeModelEffect.OrbitPoint(state, 0, directions) == new Vector3(128, 0, 40) && Ff7NativeModelEffect.OrbitPoint(state, 3, directions) == new Vector3(0, -128, 104) && Ff7NativeModelEffect.OrbitPoint(state, 12, directions) == new Vector3(0, 128, -24), "The native step24 orbit must retain shifts5/5/6, offset40 and the wrapped phase288 sample.");
        Throws<InvalidDataException>(() => (state with { Phase = 16 }).Validate());
        Throws<InvalidDataException>(() => (state with { SampleFrame = 13 }).Validate());
        Throws<InvalidDataException>(() => Ff7NativeModelEffect.OrbitPoint(state, 13, directions));
        state = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(state, 2, vertices, 384), true);
        Check(state.Restored && !state.Completed && Enumerable.Range(0, 4).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "The new profile reset must restore original parts/lighting before its completion callback.");
        Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Step24 reset must finish only on its next eligible native callback.");
    }

    private static void RunTwoOrThreeParts()
    {
        foreach (int[] counts in new int[][] { [127, 131], [1, 999], [35, 36, 54] })
        {
            Ff7NativeModelEffectState state = Ff7NativeModelEffect.Request(null, 0, counts);
            Check(state.Profile == Ff7NativeModelEffect.TwoOrThreePartProfile && state.PartCount == counts.Length, "The generic native two/three-part classifier consulted a resource, vertex or polygon allowlist.");
            Check(Ff7NativeModelEffect.Request(null, 0, counts, 512, Enumerable.Repeat(1, counts.Length).ToArray()).Profile == state.Profile, "Polygon counts changed the native part-count branch.");
            state = Ff7NativeModelEffect.Advance(state, true);
            Check(Enumerable.Range(0, counts.Length).All(part => !Ff7NativeModelEffect.PartVisible(state, part)), "Create did not hide every original rigid part.");
            for (int callback = 0; callback < 100; callback++)
            {
                state = Ff7NativeModelEffect.Advance(state, true);
            }
            Check(state is { Stage: 1, Phase: 0, Counter: 100 } && Enumerable.Range(0, counts.Length).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "Two/three-part Apply reused four-part selective hiding or changed its long hold.");
            for (int callback = 0; callback < 12; callback++)
            {
                state = Ff7NativeModelEffect.Advance(state, true);
            }
            Check(state is { Stage: 2, Phase: 288, Counter: 0, SampleFrame: 11 }, "The generic step24 ramp changed its pre-advance cache selection.");
            for (int callback = 0; callback < 10; callback++)
            {
                state = Ff7NativeModelEffect.Advance(state, true);
            }
            Check(state is { Stage: 3, Phase: 0, SampleFrame: 12 }, "The native endpoint hold lost the thirteenth cached sample.");
            for (int callback = 0; callback < 160; callback++)
            {
                bool eligible = callback % 17 != 0;
                Ff7NativeModelEffectState cold = JsonSerializer.Deserialize<Ff7NativeModelEffectState>(JsonSerializer.Serialize(state))!;
                Check(Ff7NativeModelEffect.Advance(state, eligible) == Ff7NativeModelEffect.Advance(cold, eligible), "Cold or rejected-depth continuation changed the generic part-count profile.");
                if (!eligible)
                {
                    Check(Ff7NativeModelEffect.Advance(state, false) == state, "A rejected native depth gate advanced a presentation field.");
                }
                state = Ff7NativeModelEffect.Advance(state, eligible);
            }
            short[] directions = new short[512];
            directions[1] = 4096;
            directions[48] = 1024;
            directions[49] = 2048;
            Check(Ff7NativeModelEffect.OrbitPoint(state, 0, directions) == new Vector3(128, 0, 16) && Ff7NativeModelEffect.OrbitPoint(state, 1, directions) == new Vector3(64, -32, 48), "Native two/three-part shifts or offset differ from the four-part step24 orbit.");
            Check(Ff7NativeModelEffect.OrbitPoint(state with { NativeScale = 1024 }, 1, directions) == new Vector3(128, -64, 96), "The native scale multiplier changed the two/three-part orbit.");
            _ = Ff7NativeModelEffect.OrbitPoint(state, 12, directions);
            Throws<InvalidDataException>(() => Ff7NativeModelEffect.OrbitPoint(state, 13, directions));
            Throws<ArgumentOutOfRangeException>(() => Ff7NativeModelEffect.PartVisible(state, counts.Length));
            Throws<InvalidDataException>(() => (state with { PartCount = null }).Validate());
            Throws<InvalidDataException>(() => (state with { PartCount = 4 }).Validate());
            state = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(state, 2, counts), true);
            Check(state.Restored && !state.Completed && Enumerable.Range(0, counts.Length).All(part => Ff7NativeModelEffect.PartVisible(state, part)), "Reset failed to restore all generic original parts.");
            Check(Ff7NativeModelEffect.Advance(state, true).Completed, "Reset completed outside its native second processed callback.");
        }
        Ff7NativeModelEffectState old = JsonSerializer.Deserialize<Ff7NativeModelEffectState>("{\"Profile\":\"pc-kawai13-four-part-default-v1\"}")!;
        old.Validate();
        Check(old.PartCount is null && Ff7NativeModelEffect.PartVisible(old, 3) && !JsonSerializer.Serialize(old).Contains("PartCount", StringComparison.Ordinal), "Old four-part presentation did not retain its inferred bounds or serialization.");
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed model effect: MEMORIA_FF7_GAME_DIR is unset");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "nmkin_1");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[8]);
        int[] counts = model.Geometry.Select(part => part.Polygon.Vertices.Count).ToArray();
        Check(counts.SequenceEqual(new[] { 16, 42, 16, 33 }), "The bounded original procedural-color topology changed.");
        CheckInstalledPulse(model, native);
        Ff7FieldResources pipes = Ff7FieldResources.Load(installation, "nmkin_3");
        Ff7FieldModelResources single = Ff7FieldModelResources.Load(characters, pipes.Models.Models[2]);
        Check(single.Geometry.Select(part => part.Polygon.Vertices.Count).SequenceEqual(new[] { 35 }), "The bounded original one-part35 topology changed.");
        CheckInstalledPulse(single, native);
        Ff7FieldResources street = Ff7FieldResources.Load(installation, "md8_3");
        Ff7FieldModelResources objectModel = Ff7FieldModelResources.Load(characters, street.Models.Models[10]);
        Check(objectModel.Geometry.Select(part => part.Polygon.Vertices.Count).SequenceEqual(new[] { 34 }) && objectModel.Geometry[0].Polygon.Polygons.Count == 56, "The bounded original one-part34 topology changed.");
        CheckInstalledPulse(objectModel, native);
        Ff7FieldResources basement = Ff7FieldResources.Load(installation, "mds7pb_2");
        Ff7FieldModelResources materia = Ff7FieldModelResources.Load(characters, basement.Models.Models[8]);
        Check(materia.HrcName.Equals("DABF.hrc", StringComparison.OrdinalIgnoreCase) && materia.Geometry.Count == 1 && materia.Geometry[0].Polygon.Vertices.Count == 72 && materia.Geometry[0].Polygon.Polygons.Count == 140, "The reached native materia topology changed.");
        CheckInstalledPulse(materia, native, 400);
        Ff7FieldResources tunnel = Ff7FieldResources.Load(installation, "sbwy4_6");
        Ff7FieldModelResources save = Ff7FieldModelResources.Load(characters, tunnel.Models.Models[2]);
        Check(save.HrcName.Equals("AVFE.hrc", StringComparison.OrdinalIgnoreCase) && save.Geometry.Select(part => part.Polygon.Vertices.Count).SequenceEqual(new[] { 70, 78, 12, 16 }) && save.Geometry.Select(part => part.Polygon.Polygons.Count).SequenceEqual(new[] { 72, 72, 8, 28 }), "The reached native four-part save icon topology changed.");
        CheckInstalledPulse(save, native);
        Ff7FieldResources mine = Ff7FieldResources.Load(installation, "psdun_2");
        Ff7FieldModelResources chest = Ff7FieldModelResources.Load(characters, mine.Models.Models[6]);
        Check(chest.HrcName.Equals("AVHE.hrc", StringComparison.OrdinalIgnoreCase) && mine.Models.Models[6].Scale == "384" && chest.Geometry.Select(part => part.Polygon.Vertices.Count).SequenceEqual(new[] { 35, 36, 36, 40 }) && chest.Geometry.Select(part => part.Polygon.Polygons.Count).SequenceEqual(new[] { 18, 50, 56, 20 }), "The reached original Mine chest topology/scale changed.");
        CheckInstalledPulse(chest, native, 384);
        Ff7FieldResources temple = Ff7FieldResources.Load(installation, "kuro_1");
        Ff7FieldModelResources twoPart = Ff7FieldModelResources.Load(characters, temple.Models.Models[10]);
        Check(twoPart.Geometry.Select(part => part.Polygon.Vertices.Count).SequenceEqual(new[] { 127, 131 }) && twoPart.Geometry.Select(part => part.Polygon.Polygons.Count).SequenceEqual(new[] { 80, 80 }), "The reached original two-part resource topology changed.");
        CheckInstalledPulse(twoPart, native);
        RunInstalledCheckpoint(installation);
    }

    public static void RunReachedCheckpoint()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure MEMORIA_FF7_GAME_DIR.");
        RunInstalledCheckpoint(Ff7Installation.Discover(root));
    }

    private static void RunInstalledCheckpoint(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MODEL_EFFECT_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP reached model-effect standalone: configure MEMORIA_FF7_MODEL_EFFECT_SAVE.");
            return;
        }
        using FileStream source = File.OpenRead(path);
        Ff7SessionSave saved = CombinedSessionCodec.Read(source).Ff7 ?? throw new InvalidDataException("The configured model-effect standalone has no FFVII owner.");
        Check(saved.FieldName is "psdun_2" or "seto1" or "jtemplb", "The configured model-effect standalone must contain a supported reached initializer fixture.");
        byte[] sourceBytes = File.ReadAllBytes(path);
        bool collapse = saved.FieldName == "jtemplb";
        bool shortHold = saved.FieldName == "seto1";
        using Ff7Session warm = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        for (int callback = 0; callback < 3; callback++)
        {
            warm.AdvanceTick(advanceClock: false);
            cold.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The reached original initializer and next procedural callback must retain exact independent JSON continuation.");
        }
        Ff7Actor actor = warm.Host.Process.Actors.Single(actor => actor.Entity == (collapse ? 17 : shortHold ? 11 : 8) && actor.EntityBound);
        Check(actor.ModelEffect is { Initialized: true, Applied: true } && actor.ModelEffect.Profile == (collapse ? Ff7NativeModelEffect.SinglePart80_76Profile : shortHold ? Ff7NativeModelEffect.SinglePart54Profile : Ff7NativeModelEffect.FourPart35_36Profile), "The original initializer must enter its resource-selected pulse instead of a default or script bypass.");
        if (shortHold || collapse)
        {
            using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
            Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, warm.Field.Models.Models[actor.Model]);
            Check(model.Geometry.Count == 1 && model.Geometry[0].Polygon.Vertices.Count == (collapse ? 80 : 54) && (!collapse || model.Geometry[0].Polygon.Polygons.Count == 76), "The actual reached resource must select its native single-part predicate.");
            CheckInstalledPulse(model, Ff7PcExecutableData.Load(installation));
        }
        else
        {
            Check(actor.Model == 6, "The original Mine fixture model changed.");
        }
        Check(File.ReadAllBytes(path).SequenceEqual(sourceBytes), "The independent initializer check changed its original standalone file.");
        Console.WriteLine($"reached-model-effect field={warm.Field.Name} profile={actor.ModelEffect!.Profile} native={saved.Process.Tick}->{warm.Host.Process.Tick} cold=exact source=unchanged");
    }

    private static void CheckInstalledPulse(Ff7FieldModelResources model, Ff7PcExecutableData native, int nativeScale = 512)
    {
        int[] counts = model.Geometry.Select(part => part.Polygon.Vertices.Count).ToArray();
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Advance(Ff7NativeModelEffect.Request(null, 1, counts, nativeScale, model.Geometry.Select(part => part.Polygon.Polygons.Count).ToArray()), true), true);
        bool changed = false;
        foreach (Ff7BoneGeometry part in model.Geometry)
        {
            int partIndex = model.Geometry.ToList().IndexOf(part);
            if (!Ff7NativeModelEffect.PartVisible(state, partIndex))
            {
                continue;
            }
            foreach (PcModelGroup group in part.Polygon.Groups)
            {
                Dictionary<int, int> normals = [];
                foreach (PcModelPolygon polygon in part.Polygon.Polygons.Skip(group.PolygonStart).Take(group.PolygonCount))
                {
                    normals.TryAdd(polygon.A, polygon.NormalA);
                    normals.TryAdd(polygon.B, polygon.NormalB);
                    normals.TryAdd(polygon.C, polygon.NormalC);
                }
                foreach ((int vertex, int normal) in normals)
                {
                    uint first = Ff7NativeModelEffect.ShadeVertex(state, 0, native.NativeSinCos, part.Polygon.Vertices[group.VertexStart + vertex], part.Polygon.Normals[normal], part.Polygon.VertexColors[group.VertexStart + vertex], Matrix4x4.Identity);
                    uint middle = Ff7NativeModelEffect.ShadeVertex(state, state.Profile is Ff7NativeModelEffect.SinglePart72_140Profile or Ff7NativeModelEffect.SinglePart54Profile ? 4 : 8, native.NativeSinCos, part.Polygon.Vertices[group.VertexStart + vertex], part.Polygon.Normals[normal], part.Polygon.VertexColors[group.VertexStart + vertex], Matrix4x4.Identity);
                    changed |= first != middle;
                }
            }
        }
        Check(changed, "Original visible save-point parts did not produce an animated color pulse.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidDataException($"Expected {typeof(T).Name}.");
    }
}
