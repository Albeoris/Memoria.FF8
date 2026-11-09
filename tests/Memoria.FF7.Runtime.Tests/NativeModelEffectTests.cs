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
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [16, 42, 16]));
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
        Throws<NotSupportedException>(() => Ff7NativeModelEffect.Request(null, 0, [54]));
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
                    uint middle = Ff7NativeModelEffect.ShadeVertex(state, state.Profile == Ff7NativeModelEffect.SinglePart72_140Profile ? 4 : 8, native.NativeSinCos, part.Polygon.Vertices[group.VertexStart + vertex], part.Polygon.Normals[normal], part.Polygon.VertexColors[group.VertexStart + vertex], Matrix4x4.Identity);
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
