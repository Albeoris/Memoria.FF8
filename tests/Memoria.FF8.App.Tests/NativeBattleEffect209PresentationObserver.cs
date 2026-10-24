using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect209PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> cursorSides = [];
    private readonly HashSet<uint> meshSources = [];
    private bool mesh;
    private readonly HashSet<int> gridSides = [];
    private readonly List<object> observations = [];
    private bool reloaded;
    private bool grid;
    private bool deformed;
    private bool capture;
    private bool cleanup;
    private bool retired;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentMeshCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentMeshCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect209State state = current.NativeEffectMemory!.Effect209!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback is MainCallback or AlternateCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback is MainCallback or AlternateCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        if (age is 0 or 1 or 2 or 3 or 16 or 17 or 20 or 21 or 31 or 32 or 51 or 52 or 53 or 54 or 55 or 82 or 83 or 114 or 115 or 124 or 125 or 130)
        {
            Mark("effect209-before-main-age-" + age, current, cold);
        }
        NativeBattleStageVariant137State variants = current.NativeStage!.Extended!.Stage137.Variants!;
        if (variants.Index == 1)
        {
            NativeBattleStageVariant137Entry entry = effect.Binding.Effect209Resources!.StageVariants[0];
            Assert.Equal(entry.Color40 & 0xFFFFFF, state.ReadDword(StageAfterColor40Global) & 0xFFFFFF);
            Assert.Equal(entry.Color44 & 0xFFFFFF, state.ReadDword(StageAfterColor44Global) & 0xFFFFFF);
            NativeBattleEffectFileAsset file = effect.Binding.Effect209Resources.ReadFile(entry.FileId);
            uint destination = current.NativeStage.SourceAddress1D9A820!.Value;
            Assert.Equal(file.Data.AsSpan(entry.HeaderOffset, 32).ToArray(), current.NativeFileStaging!.Read(destination, 32));
            reloaded = true;
            Mark("effect209-current-stage-variant-prefix", current, cold);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            if (task.Callback == 0x637A40 && task.ReadWord(12) is 1 or 35 or 36 or 38 or 39 or 68 or 82 or 98 or 99)
            {
                Mark("effect209-capture-grid-age-" + task.ReadWord(12), current, cold);
            }
            if (task.Callback == CleanupCallback && task.ReadWord(12) == 1)
            {
                cleanup = true;
                Mark("effect209-before-cleanup-request", current, cold);
            }
        }
        if (oldAge == 124 && age == 125)
        {
            Contacts++;
            Mark("effect209-current-first-contact", current, cold);
        }
        if (oldAge == 130 && main is null)
        {
            retired = true;
            Mark("effect209-main-retired", current, cold);
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect209-current-contact-glyph", current, cold);
            }
        }
        NativeBattleLogicalCommand[] gridCommands = GridCommands(current, state, previous);
        if (gridCommands.Length > 0)
        {
            grid = true;
            Assert.Equal(1080, gridCommands.Select(command => command.Packet).Distinct().Count());
            for (int row = 1; row < GridRows - 1; row++)
            {
                for (int column = 1; column < GridColumns - 1; column++)
                {
                    deformed |= state.ReadWord(GridAddress + (uint)((row * GridColumns + column) * GridStride + 8)) != 0;
                }
            }
            Mark("effect209-current-addressed-grid", current, cold);
            if (deformed)
            {
                Mark("effect209-current-deformed-grid", current, cold);
            }
        }
        NativeBattleLogicalCommand[] captured = CaptureCommands(current, previous);
        if (captured.Length > 0)
        {
            capture = true;
            Mark("effect209-current-common-stage-capture", current, cold);
        }
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena != 0)
        {
            Assert.True(cursor == arena || cursor == arena + 0x10000);
            cursorSides.Add(cursor == arena ? 0 : 1);
        }
        CurrentMeshCommands = MeshCommands(prior, current, out HashSet<uint> sources);
        mesh |= CurrentMeshCommands.Length > 0;
        meshSources.UnionWith(sources);
        if (CurrentMeshCommands.Length > 0)
        {
            Mark("effect209-current-common-source-mesh", current, cold);
        }
        CurrentCommands = CurrentMeshCommands.Concat(gridCommands).Concat(captured).DistinctBy(command => command.Packet).ToArray();
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Variant = variants.Index, Grid = gridCommands.Length, Capture = captured.Length, Mesh = CurrentMeshCommands.Length, Deformed = deformed, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(reloaded);
        Assert.True(grid);
        Assert.True(deformed);
        Assert.True(capture);
        Assert.True(cleanup);
        Assert.True(retired);
        Assert.True(mesh);
        Assert.NotEmpty(meshSources);
        Assert.Equal(2, cursorSides.Count);
        Assert.NotEmpty(gridSides);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect209!.FixedBanks.Values, bank => Assert.NotNull(bank));
        File.WriteAllText(Path.Combine(output, "effect209-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Reloaded = reloaded, Grid = grid, Deformed = deformed, Capture = capture, Cleanup = cleanup, Retired = retired, CursorSides = cursorSides.Order(), Mesh = mesh, MeshSources = meshSources.Order(), GridSides = gridSides.Order(), Contacts, ContactGlyphs, Observations = observations }));
    }

    private sealed record MeshTemplate(uint Source, int Group, bool Compound, uint? Window);

    private static NativeBattleLogicalCommand[] MeshCommands(BattleSnapshot prior, BattleSnapshot current, out HashSet<uint> matchedSources)
    {
        matchedSources = [];
        if (current.NativeGraphics?.OriginalEffect?.Binding.Effect209Resources is not { } resources)
        {
            return [];
        }
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        List<(NativeBattleModulePoolTask Task, int Age)> executed = previous.Values.Select(task => (task, (int)unchecked((short)task.ReadWord(12)))).ToList();
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks)
        {
            if (!previous.ContainsKey(address))
            {
                executed.Add((task, Math.Max(0, unchecked((short)task.ReadWord(12)) - 1)));
            }
        }
        NativeBattleSummonColors colors = current.Definition.NativeGraphics!.Rules.SummonColors!;
        Dictionary<string, List<MeshTemplate>> templates = [];
        NativeBattleModuleProjectedVertex[] sample = [new(0x00640064, 1024, 0), new(0x00640078, 1024, 0), new(0x00780064, 1024, 0), new(0x00780078, 1024, 0)];
        foreach ((NativeBattleModulePoolTask task, int age) in executed)
        {
            if (task.Callback is not (0x638490 or 0x6385E0 or 0x638770 or 0x638900 or 0x638A80))
            {
                continue;
            }
            uint source = task.Callback switch
            {
                0x638490 => 0xE288F0,
                0x6385E0 => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(MeshDirectoryAddress + (uint)(task.ReadWord(14) * 4), 4)),
                0x638770 => 0xE26340,
                0x638900 => 0xE277B8,
                _ => 0xE290E0
            };
            int? cue = task.Callback switch
            {
                0x638490 => age >= 102 ? (age - 102) << 9 : null,
                0x6385E0 => age >= (task.ReadWord(0x16) == 0 ? 102 : 47) ? (age - (task.ReadWord(0x16) == 0 ? 102 : 47)) << 9 : null,
                0x638770 => age >= 102 ? age * 320 - 31104 : 1536,
                0x638900 => age < 2 ? (2 - age) << 11 : age >= 8 ? (age - 8) << 8 : null,
                _ => age >= 4 ? (age - 4) << 8 : null
            };
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
            int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
                cursor += 4;
                for (int face = 0; face < count; face++, cursor += lengths[group])
                {
                    ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(cursor, lengths[group]);
                    bool quad = (group & 1) != 0;
                    bool scrolling = task.Callback == 0x638770;
                    ImmutableArray<uint> words;
                    uint? window = null;
                    if (scrolling)
                    {
                        NativeBattleHoly175WindowedMeshContext context = new(0xF3, 0, cue, resources.ScrollColors[age % 18], 0, age * 8, new(0, 0, 64, 128), 17, 2);
                        NativeBattleGfGeometryBatch batch = quad ? NativeBattleHoly175WindowedMeshPackets.EmitQuad(bytes, index => sample[index], false, context, colors) : NativeBattleHoly175WindowedMeshPackets.EmitTriangle(bytes, index => sample[index], false, context, colors);
                        NativeBattleGeometryPrimitive? primitive = batch.Attempts.Single().Primitive;
                        if (primitive is null)
                        {
                            continue;
                        }
                        words = primitive.Words.Skip(2).Take(quad ? 9 : 7).ToImmutableArray();
                        window = NativeBattleEffectTextureWindows.Encode(context.Window) & 0xFFFFFF;
                    }
                    else
                    {
                        NativeBattleModuleMeshContext context = new(cue is null ? 0x33u : 0xF3u, 0, cue, null, null, 0, 17, 2);
                        NativeBattleGeometryPrimitive? primitive = NativeBattleModulePackets.EmitFace572200(bytes, sample.Take(quad ? 4 : 3).ToArray(), group, context, colors).Primitive;
                        if (primitive is null)
                        {
                            continue;
                        }
                        words = primitive.Words.Skip(1).ToImmutableArray();
                    }
                    string key = MeshKey(words, group);
                    if (!templates.TryGetValue(key, out List<MeshTemplate>? selected))
                    {
                        selected = [];
                        templates.Add(key, selected);
                    }
                    selected.Add(new(source, group, scrolling || task.Callback == 0x638490, window));
                }
            }
        }
        List<NativeBattleLogicalCommand> result = [];
        foreach (NativeBattleLogicalResolvedCommand resolved in current.NativeGraphics!.PrimitiveFrame!.LastCommands)
        {
            NativeBattleLogicalCommand command = resolved.Command;
            if (command.Packet.Bank is not ("off_B6D070" or "off_B6D094") || command.Flags20 != 0 || command.OrderingBucket is not int bucket || bucket < 17 || bucket > 4112)
            {
                continue;
            }
            int group = (command.Opcode & 0xFC) switch { 0x20 => 0, 0x28 => 1, 0x24 => 2, 0x2C => 3, 0x30 => 4, 0x38 => 5, 0x34 => 6, 0x3C => 7, _ => -1 };
            if (group < 0 || !templates.TryGetValue(MeshKey(command.Words, group), out List<MeshTemplate>? selected))
            {
                continue;
            }
            int[] screenWords = ScreenWords(group);
            if (screenWords.Any(index => index >= command.Words.Length) || NativeBattleModulePackets.Nclip(command.Words[screenWords[0]], command.Words[screenWords[1]], command.Words[screenWords[2]]) == 0 || command.VertexPayload4.Length != 4)
            {
                continue;
            }
            foreach (MeshTemplate template in selected)
            {
                if (template.Window is uint bits && resolved.DrawMode.TextureWindowBits != bits)
                {
                    continue;
                }
                bool zero = command.VertexPayload4.All(value => value == 0);
                if (zero)
                {
                    if (!template.Compound || command.WordOffset != 2)
                    {
                        continue;
                    }
                }
                else
                {
                    int corners = (group & 1) != 0 ? 4 : 3;
                    if (command.VertexPayload4.Any(value => value < 0 || value > 65535))
                    {
                        continue;
                    }
                    uint depth = unchecked((uint)command.VertexPayload4.Take(corners).Sum() * (corners == 4 ? 0x100u : 0x155u)) >> 12;
                    if (bucket != 17 + (depth >> 2))
                    {
                        continue;
                    }
                }
                matchedSources.Add(template.Source);
                result.Add(command);
                break;
            }
        }
        return result.DistinctBy(command => command.Packet).ToArray();
    }

    private static string MeshKey(ImmutableArray<uint> words, int group)
    {
        HashSet<int> screens = ScreenWords(group).ToHashSet();
        return group + ":" + words.Length + ":" + string.Join(",", words.Where((_, index) => !screens.Contains(index)).Select(value => value.ToString("X8")));
    }

    private static int[] ScreenWords(int group)
    {
        int count = (group & 1) != 0 ? 4 : 3;
        int stride = ((group & 2) != 0 ? 1 : 0) + (group >= 4 ? 1 : 0) + 1;
        return Enumerable.Range(0, count).Select(index => 1 + index * stride).ToArray();
    }

    private NativeBattleLogicalCommand[] GridCommands(BattleSnapshot current, NativeBattleEffect209State state, Dictionary<uint, NativeBattleModulePoolTask> previous)
    {
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "209FAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset < 0x19500 && command.Packet.ByteOffset % 96 is 16 or 56).DistinctBy(command => command.Packet).ToArray();
        foreach (NativeBattleLogicalCommand command in commands)
        {
            int age = previous.Values.Single(task => task.Callback == 0x637A40).ReadWord(12);
            Assert.InRange(age, 38, 97);
            Assert.Equal(age >= 82 ? (byte)0x2E : (byte)0x2C, command.Opcode);
            Assert.NotEqual(0, command.Flags20 & 1);
            Assert.Equal(9, command.Words.Length);
            int cell = command.Packet.ByteOffset / 96;
            int row = cell / 40;
            int column = cell % 40;
            int[] vertices = [row * GridColumns + column, row * GridColumns + column + 1, (row + 1) * GridColumns + column, (row + 1) * GridColumns + column + 1];
            for (int corner = 0; corner < 4; corner++)
            {
                Assert.Equal(state.ReadDword(GridAddress + (uint)(vertices[corner] * GridStride)), command.Words[1 + corner * 2]);
            }
            gridSides.Add(command.Packet.ByteOffset % 96 == 16 ? 0 : 1);
        }
        return commands;
    }

    private readonly record struct StageFace(bool Quad, uint Color, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3, int Bias);
    private static NativeBattleLogicalCommand[] CaptureCommands(BattleSnapshot current, Dictionary<uint, NativeBattleModulePoolTask> previous)
    {
        if (!previous.Values.Any(task => task.Callback == 0x637A40 && (task.ReadWord(12) < 36 || task.ReadWord(12) is >= 82 and <= 99)))
        {
            return [];
        }
        NativeBattleStageState stage = current.NativeStage!;
        if (stage.Groups.Any(group => (group.Flags & 3) == 3))
        {
            return [];
        }
        HashSet<StageFace>[] faces = Enumerable.Range(0, 4).Select(_ => new HashSet<StageFace>()).ToArray();
        for (int group = 0; group < 4; group++)
        {
            uint? address = stage.CurrentGroupGeometryAddress(group, (pointer, length) => current.NativeFileStaging!.Read(pointer, length));
            if (address is not uint source || (stage.Groups[group].Flags & 1) == 0)
            {
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => current.NativeFileStaging!.Read(source + (uint)offset, length), stage.Groups[group].Matrices.Length);
            foreach (NativeBattleRawPolygon polygon in meshes.SelectMany(mesh => mesh.Polygons))
            {
                ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
                uint material = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
                int ordinary = polygon.Quad ? 20 : 16;
                uint color;
                if (bytes.Length == ordinary + 4)
                {
                    color = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(ordinary, 4));
                    if (stage.Groups[group].ColorDepth != 0)
                    {
                        color = current.Definition.NativeGraphics!.Rules.SummonColors!.Interpolate(color, stage.Groups[group].ColorDepth, stage.Groups[group].ColorFlags);
                    }
                }
                else
                {
                    color = (stage.Groups[group].ColorFlags & 0x2FFFFFF) | (polygon.Quad ? 0x2C000000u : 0x24000000u) | ((material & 0xFE000000) != 0 ? 0x2000000u : 0u);
                }
                faces[group].Add(new(polygon.Quad, color, BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)), material, BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(polygon.Quad ? 16 : 6, 2)), polygon.Quad ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18, 2)) : (ushort)0, (BinaryPrimitives.ReadUInt16LittleEndian(bytes) >> 12) - 8));
            }
        }
        Dictionary<StageFace, List<(int Group, int Bias)>> selected = [];
        for (int group = 0; group < 2; group++)
        {
            foreach (StageFace face in faces[group])
            {
                StageFace key = face with { Bias = 0 };
                if (!selected.TryGetValue(key, out List<(int Group, int Bias)>? values))
                {
                    values = [];
                    selected.Add(key, values);
                }
                values.Add((group, face.Bias));
            }
        }
        NativeBattleEffect209Resources resources = current.NativeGraphics!.OriginalEffect!.Binding.Effect209Resources!;
        return Commands(current).Where(command => Common(command) && Matches(command)).ToArray();
        bool Matches(NativeBattleLogicalCommand command)
        {
            if (command.Words.Length is not (7 or 9) || command.VertexPayload4.Length != 4 || command.OrderingBucket is not int bucket)
            {
                return false;
            }
            bool quad = command.Words.Length == 9;
            StageFace key = new(quad, command.Words[0], command.Words[2], command.Words[4], (ushort)command.Words[6], quad ? (ushort)command.Words[8] : (ushort)0, 0);
            if (!selected.TryGetValue(key, out List<(int Group, int Bias)>? values))
            {
                return false;
            }
            foreach ((int group, int bias) in values)
            {
                int depth = command.VertexPayload4.Take(quad ? 4 : 3).Min() - bias;
                NativeBattleEffect209StageDepth row = resources.StageDepthRows[group];
                if (depth is >= 0 and <= 65535 && bucket == row.TableBaseBucket + (depth >> row.DepthShift))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static bool Common(NativeBattleLogicalCommand command) => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1;
    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSnapshot snapshot) => snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command);
    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
    {
        Dictionary<uint, NativeBattleModulePoolTask> result = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return result;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            HashSet<int> visited = [];
            int? index = pool.Head;
            while (index is int selected)
            {
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(pool.BankAddress + (uint)(selected * pool.Stride), task);
                index = task.Next;
            }
        }
        return result;
    }
    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
