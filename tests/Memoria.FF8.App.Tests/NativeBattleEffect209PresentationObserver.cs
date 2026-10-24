using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect209PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> packetBanks = [];
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
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
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
        NativeBattleLogicalCommand[] gridCommands = GridCommands(current, state);
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
        CurrentCommands = Private(current, state).Concat(gridCommands).Concat(captured).DistinctBy(command => command.Packet).ToArray();
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Variant = variants.Index, Grid = gridCommands.Length, Capture = captured.Length, Deformed = deformed, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(reloaded);
        Assert.True(grid);
        Assert.True(deformed);
        Assert.True(capture);
        Assert.True(cleanup);
        Assert.True(retired);
        Assert.Equal(2, packetBanks.Count);
        Assert.NotEmpty(gridSides);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect209!.FixedBanks.Values, bank => Assert.NotNull(bank));
        File.WriteAllText(Path.Combine(output, "effect209-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Reloaded = reloaded, Grid = grid, Deformed = deformed, Capture = capture, Cleanup = cleanup, Retired = retired, PacketBanks = packetBanks.Order(), GridSides = gridSides.Order(), Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] Private(BattleSnapshot current, NativeBattleEffect209State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor < arena)
        {
            return [];
        }
        uint start = cursor >= arena + 0x10000 ? arena + 0x10000 : arena;
        Assert.InRange(cursor, start, start + 0x10000);
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private NativeBattleLogicalCommand[] GridCommands(BattleSnapshot current, NativeBattleEffect209State state)
    {
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "209FAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset < 0x19500 && command.Packet.ByteOffset % 96 is 16 or 56).DistinctBy(command => command.Packet).ToArray();
        foreach (NativeBattleLogicalCommand command in commands)
        {
            Assert.Equal((byte)0x2C, command.Opcode);
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
        if (!previous.Values.Any(task => task.Callback == 0x637A40 && (task.ReadWord(12) < 36 || task.ReadWord(12) is 98 or 99)))
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
