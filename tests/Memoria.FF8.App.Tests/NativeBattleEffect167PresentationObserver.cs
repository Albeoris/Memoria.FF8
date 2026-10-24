using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect167PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool particles;
    private bool trail;
    private bool localMeshes;
    private bool body;
    private bool retired;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentFadeCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (graphics.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect167State state = current.NativeEffectMemory!.Effect167!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        Assert.Equal((byte)86, graphics.Models[3].ModelId4);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal((int)shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            callbacks.Add(task.Callback);
            if (task.Callback == 0x5E33E0 && task.ReadWord(12) >= 10)
            {
                trail = true;
                Mark("effect167-current-trail-samples", current, cold);
            }
            if (task.Callback == 0x5E3C20 && task.ReadWord(12) is 12 or 24 or 32)
            {
                Mark("effect167-before-plane-body-age-" + task.ReadWord(12), current, cold);
            }
        }
        if (age is 0 or 27 or 28 or 44 or 45 or 64 or 65 or 68 or 69 or 72 or 74 or 75 or 77 or 97 or 98 or 143 or 144 or 145)
        {
            Mark("effect167-before-main-age-" + age, current, cold);
        }
        if (oldAge == 143 && age == 144)
        {
            Contacts++;
            Mark("effect167-current-contact-range", current, cold);
        }
        if (oldAge == 145 && main is null)
        {
            retired = true;
            Assert.NotEqual(0, (graphics.Models[3].ResourceFlags44 ?? throw new InvalidDataException("Effect167 requires the current caster sequence header.")) & 0x40);
            Mark("effect167-main-current-resource-retired", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect167-current-contact-glyph", current, cold);
            }
        }
        int activeRecords = Enumerable.Range(0, FirstRecordCount).Count(index => state.ReadDword(FirstRecordsAddress + (uint)(index * FirstRecordStride)) != 0);
        if (activeRecords > 0)
        {
            particles = true;
            Mark("effect167-current-projectile-records", current, cold);
        }
        NativeBattleLogicalCommand[] privateCommands = PrivateCommands(current, state);
        if (tasks.Values.Any(task => task.Callback is 0x5E38D0 or 0x5E3A00) && privateCommands.Length > 0)
        {
            localMeshes = true;
            Mark("effect167-current-local-mesh", current, cold);
        }
        NativeBattleLogicalCommand[] bodyCommands = BodyCommands(current, tasks);
        if (bodyCommands.Length > 0)
        {
            body = true;
            Mark("effect167-current-plane-body", current, cold);
        }
        CurrentFadeCommands = Commands(current).Where(command => Common(command) && (command.Opcode is 0x60 or 0x62) && command.Words.Length == 3 && command.Words[1] == 0 && command.Words[2] == 0x00D80140).ToArray();
        CurrentCommands = privateCommands.Concat(bodyCommands).Concat(CurrentFadeCommands).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Pools = effect.Pools.Length, ActiveRecords = activeRecords, PrivateCommands = privateCommands.Length, BodyCommands = bodyCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(particles);
        Assert.True(trail);
        Assert.True(localMeshes);
        Assert.True(body);
        Assert.True(retired);
        foreach (int age in new[] { 12, 24, 32 })
        {
            Assert.Contains("effect167-before-plane-body-age-" + age, markers);
        }
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Contains(MainCallback, callbacks);
        Assert.Equal(2, packetBanks.Count);
        File.WriteAllText(Path.Combine(output, "effect167-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), Particles = particles, Trail = trail, LocalMeshes = localMeshes, CurrentPlaneBody = body, MainRetired = retired, Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect167State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        uint start = cursor >= arena + 0x8000 ? arena + 0x8000 : arena;
        Assert.InRange(cursor, start, start + 0x8000);
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private static NativeBattleLogicalCommand[] BodyCommands(BattleSnapshot current, Dictionary<uint, NativeBattleModulePoolTask> tasks)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        NativeBattleModulePoolTask? task = tasks.Values.FirstOrDefault(item => item.Callback == 0x5E3C20 && item.ReadWord(12) > 0 && item.ReadWord(12) < 40);
        if (task is null)
        {
            return [];
        }
        int actorIndex = task.ReadWord(14);
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        Assert.NotEqual(0, actor.Flags0 & 4);
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        string actorId = graphics.ActorIds[actorIndex]!;
        NativeBattleGraphicsBinding binding = current.Definition.NativeGraphics!.Bindings.Single(item => item.ActorId == actorId);
        NativeBattlePoseSource pose = binding.Resource.PoseSource!;
        HashSet<(uint Clut, uint Tpage)> texturePairs = [];
        Add(actor.ActiveResourceRow100!.Value, pose.BoneCount, actor.SequenceState!.MeshMask124);
        if ((actor.Flags0 & 0x200) == 0 && actor.LinkedTextureResourceIndex is int linked)
        {
            NativeBattlePoseSource linkedPose = binding.Resource.LinkedPoseSource ?? throw new InvalidDataException("Effect167 requires the current linked skeleton.");
            Add(linked, linkedPose.BoneCount, uint.MaxValue);
        }
        uint color = (actor.Color40 ?? throw new InvalidDataException("Effect167 requires the current actor color.")) & 0xFFFFFF;
        return Commands(current).Where(command => Common(command) && (command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E) && command.Words.Length >= 7 && command.VertexPayload4.Length == 4 && (command.Words[0] & 0xFFFFFF) == color && texturePairs.Contains((command.Words[2] >> 16, command.Words[4] >> 16))).ToArray();

        void Add(int row, int boneCount, uint mask)
        {
            NativeBattleModelCpuReference source = cpu.Rows[row].Require(16);
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), boneCount, objectMask: mask);
            foreach (NativeBattleRawPolygon polygon in meshes.SelectMany(mesh => mesh.Polygons))
            {
                texturePairs.Add((BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan(8, 4)) >> 16, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan(12, 4)) >> 16));
            }
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
                Assert.InRange(selected, 0, pool.Slots.Length - 1);
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(checked(pool.BankAddress + (uint)(selected * pool.Stride)), task);
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
