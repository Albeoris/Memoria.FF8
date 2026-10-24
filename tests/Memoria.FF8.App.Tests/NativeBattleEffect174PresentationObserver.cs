using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect174PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly List<object> observations = [];
    private bool history;
    private bool body;
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
        NativeBattleEffect174State state = current.NativeEffectMemory!.Effect174!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        if (age is 9 or 10 or 11 or 68 or 69 or 100 or 101 or 129 or 130 or 131 or 159 or 160 or 175 or 176 or 180)
        {
            Mark("effect174-before-main-age-" + age, current, cold);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            if (task.Callback == CleanupCallback && task.ReadWord(12) == 1)
            {
                cleanup = true;
                Mark("effect174-before-cleanup-age-one", current, cold);
            }
            if ((task.Callback is 0x5D4ED0 or 0x5D64E0) && (task.ReadWord(12) is 0 or 15 or 29))
            {
                Mark("effect174-" + (task.Callback == 0x5D4ED0 ? "history" : "body") + "-age-" + task.ReadWord(12), current, cold);
            }
        }
        if (oldAge == 175 && age == 176)
        {
            Contacts++;
            Mark("effect174-contact", current, cold);
        }
        if (oldAge == 180 && main is null)
        {
            retired = true;
            Mark("effect174-main-retired", current, cold);
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect174-contact-glyph", current, cold);
            }
        }
        NativeBattleLogicalCommand[] historyCommands = CommonGeometry(current, previous, true);
        NativeBattleLogicalCommand[] bodyCommands = CommonGeometry(current, previous, false);
        if (historyCommands.Length > 0)
        {
            history = true;
            Mark("effect174-common-history", current, cold);
        }
        if (bodyCommands.Length > 0)
        {
            body = true;
            Mark("effect174-common-target-body", current, cold);
        }
        CurrentCommands = Private(current, state).Concat(historyCommands).Concat(bodyCommands).DistinctBy(command => command.Packet).ToArray();
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, History = historyCommands.Length, Body = bodyCommands.Length, Commands = CurrentCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(history, "The history callback must deliver its actual source faces to COMMON.");
        Assert.True(body, "The target body callback must deliver its actual source faces to COMMON.");
        Assert.True(cleanup);
        Assert.True(retired);
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect174!.FixedBanks.Values, bank => Assert.NotNull(bank));
        File.WriteAllText(Path.Combine(output, "effect174-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), History = history, Body = body, Cleanup = cleanup, Retired = retired, Banks = banks.Order(), Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] Private(BattleSnapshot current, NativeBattleEffect174State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        uint start = cursor >= arena + 0xC000 ? arena + 0xC000 : arena;
        Assert.InRange(cursor, start, start + 0xC000);
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            banks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private static NativeBattleLogicalCommand[] CommonGeometry(BattleSnapshot current, Dictionary<uint, NativeBattleModulePoolTask> previous, bool historical)
    {
        uint callback = historical ? 0x5D4ED0u : 0x5D64E0u;
        if (!previous.Values.Any(task => task.Callback == callback))
        {
            return [];
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        int actorIndex = historical ? 3 : 0;
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        NativeBattleGraphicsBinding binding = current.Definition.NativeGraphics!.Bindings.Single(item => item.ActorId == graphics.ActorIds[actorIndex]);
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        HashSet<Face> faces = [];
        uint[] colors = historical ? graphics.OriginalEffect!.Binding.Effect174Resources!.HistoryColors.ToArray() : [actor.Color40!.Value];
        Add(actor.ActiveResourceRow100!.Value, binding.Resource.PoseSource!.BoneCount, historical ? 8u : actor.SequenceState!.MeshMask124);
        if (!historical && (actor.Flags0 & 0x200) == 0 && actor.LinkedTextureResourceIndex is int linked)
        {
            Add(linked, binding.Resource.LinkedPoseSource!.BoneCount, uint.MaxValue);
        }
        return Commands(current).Where(command => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1 && command.VertexPayload4.Length == 4 && Matches(command)).ToArray();

        void Add(int row, int count, uint mask)
        {
            NativeBattleModelCpuReference source = cpu.Rows[row].Require(16);
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), count, (_, counts, _, _) =>
            {
                Assert.Equal((ushort)0, counts[2]);
                Assert.Equal((ushort)0, counts[3]);
            }, objectMask: mask);
            foreach (NativeBattleRawPolygon polygon in meshes.SelectMany(mesh => mesh.Polygons))
            {
                ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
                uint material = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
                uint last = polygon.Quad ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)) : unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4)) >> 16));
                foreach (uint color in colors)
                {
                    bool semitransparent = historical ? ((color & 0x02000000) != 0 || (material & 0xFE000000) != 0) : (material & 0x02000000) != 0;
                    byte opcode = (byte)((polygon.Quad ? 0x2C : 0x24) | (semitransparent ? 2 : 0));
                    faces.Add(new(opcode, color & 0xFFFFFF, BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)), historical ? material : material & 0x01FFFFFF, historical ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(polygon.Quad ? 16 : 6, 2)) : last, polygon.Quad ? historical ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18, 2)) : unchecked((uint)(unchecked((int)last) >> 16)) : 0));
                }
            }
        }
        bool Matches(NativeBattleLogicalCommand command)
        {
            bool quad = (command.Opcode & 8) != 0;
            if (command.Opcode is not (0x24 or 0x26 or 0x2C or 0x2E) || command.Words.Length != (quad ? 9 : 7))
            {
                return false;
            }
            return faces.Contains(new(command.Opcode, command.Words[0] & 0xFFFFFF, command.Words[2], command.Words[4], historical ? command.Words[6] & 0xFFFF : command.Words[6], quad ? historical ? command.Words[8] & 0xFFFF : command.Words[8] : 0));
        }
    }

    private readonly record struct Face(byte Opcode, uint Color, uint Uv0, uint Uv1, uint Uv2, uint Uv3);
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
