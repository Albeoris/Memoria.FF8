using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect66PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool body;
    private bool stage;
    private bool shards;
    private bool sparks;
    private bool burst;
    private bool text;
    private bool soundGain;
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
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect66State state = current.NativeEffectMemory!.Effect66!;
        state.Validate();
        Assert.Equal((byte)140, graphics.Models[5].ModelId4);
        Assert.Equal((ushort)1448, current.NativeModelCpuResources!.ScratchMaximum);
        NativeBattleModulePoolTask? before = ReadMain(prior);
        NativeBattleModulePoolTask? main = ReadMain(current);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal((int)shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
            foreach (NativeBattleModulePoolTask task in Live(pool))
            {
                callbacks.Add(task.Callback);
            }
        }
        if (oldAge == 25 && age == 26)
        {
            Contacts++;
            Mark("effect66-first-current-contact25", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect66-contact-glyph", current, cold);
            }
        }
        if (age is 25 or 53 or 55 or 191 or 217 or 222 or 382 or 524 or 802 or 832)
        {
            Mark("effect66-before-main-age-" + age, current, cold);
        }
        foreach ((uint address, NativeBattleGuardianCameraTask task) in state.CameraTasks)
        {
            NativeBattleModulePoolTask slot = effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor).Slots[checked((int)((address - ChildBank) / ChildStride))];
            Assert.Equal(0x63E9C0u, slot.Callback);
            Assert.Equal(task.Frame, slot.ReadWord(12));
            Assert.Equal(CameraAddress + (uint)task.Offset, slot.ReadDword(20));
            Assert.Equal(CapturedWorldAddress, slot.ReadDword(24));
            if (task.Offset > 0)
            {
                camera = true;
                Mark("effect66-current-paired-camera", current, cold);
            }
        }
        uint arena = state.ReadDword(ArenaGlobal);
        if (state.ReadDword(0x24C9190) != 0)
        {
            Assert.Equal(arena + 0x28000, state.ReadDword(0x24C9190));
            Assert.Equal(arena + 0x29184, state.ReadDword(0x24C917C));
            byte[] flags = current.NativeEffectMemory.Arena!.Read(0x28000, 2242 * 2);
            Assert.Equal(4484, flags.Length);
            Mark("effect66-current-2242-faceflags", current, cold);
            if (age is >= 223 and < 383)
            {
                int active = Enumerable.Range(0, 1000).Count(index => Word(current.NativeEffectMemory.Arena!.Read(0x29184 + index * 68, 2), 0) != 0);
                if (active > 0)
                {
                    shards = true;
                    Mark("effect66-current-shards", current, cold);
                }
            }
        }
        if (state.ReadDword(InitializedBase + 4) != uint.MaxValue)
        {
            Assert.Equal(arena, state.ReadDword(InitializedBase + 4));
            if (Enumerable.Range(0, 800).Any(index => Dword(current.NativeEffectMemory.Arena!.Read(index * 24, 4), 0) != 0))
            {
                sparks = true;
                Mark("effect66-current-800-spark-records", current, cold);
            }
        }
        if (state.ReadDword(InitializedBase) != uint.MaxValue)
        {
            Assert.Equal(state.ReadDword(0x24C917C), state.ReadDword(InitializedBase));
            burst = true;
            Mark("effect66-current-shard-storage-burst-alias", current, cold);
        }
        if (state.Read(PartialMatrixAddress, 32).Any(value => value != 0))
        {
            Mark("effect66-current-partial-matrix", current, cold);
        }
        ObserveText(current, cold);
        if (oldAge == 802 && age == 803)
        {
            NativeBattleSoundGainRequest gain = current.Events.Where(item => item.Frame > prior.Frame).Select(item => item.NativeSoundGain).OfType<NativeBattleSoundGainRequest>().Single();
            Assert.Equal(120, gain.RawDuration);
            Assert.Equal(1200, gain.DurationMilliseconds);
            Assert.Equal(0, gain.RawGain);
            Assert.Equal((byte)0, gain.Gain);
            Assert.Equal((byte)1, gain.FirstVoice);
            Assert.Equal((byte)29, gain.VoiceCount);
            Assert.Equal(NativeGlobalSoundBackend.Bufferless, gain.Backend);
            Assert.Equal(JsonSerializer.Serialize(prior.NativeMusicProcess), JsonSerializer.Serialize(current.NativeMusicProcess));
            soundGain = true;
            Mark("effect66-sound-gain-request-music-retained", current, cold);
        }
        if (oldAge == 832 && (main is null || age == 833))
        {
            NativeBattleCameraState previous = prior.NativeCamera!;
            NativeBattleCameraState captured = current.NativeCamera!;
            Assert.Equal(previous.CapturedPose!.Roll, captured.CapturedPose!.Roll);
            Assert.Equal(previous.PoseCaptureCount, captured.PoseCaptureCount);
            Assert.Equal(captured.FocalLength, captured.CapturedPose.FocalLength);
            retired = true;
            Mark("effect66-selective-camera-retirement", current, cold);
        }
        NativeBattleLogicalCommand[] bodyCommands = BodyCommands(current);
        NativeBattleLogicalCommand[] stageCommands = StageCommands(current);
        if (bodyCommands.Length > 0)
        {
            body = true;
            Mark("effect66-current-COMMON-shattering-body", current, cold);
        }
        if (stageCommands.Length > 0)
        {
            stage = true;
            Mark("effect66-current-COMMON-stage-group1", current, cold);
        }
        CurrentFadeCommands = Commands(current).Where(command => Common(command) && (command.Opcode == 0x62 && command.Words.Length == 3 && command.Words[1] == 0 && command.Words[2] == 0x00D80140 || command.Opcode == 0x2A && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == 0x00D80000 && command.Words[4] == 0x00D80140)).ToArray();
        CurrentCommands = PrivateCommands(current).Concat(bodyCommands).Concat(stageCommands).Concat(CurrentFadeCommands).DistinctBy(command => command.Packet).ToArray();
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
        }
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.Empty(state.CameraTasks);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            Assert.Equal((byte)1, graphics.Models[5].Sequence);
            Assert.Equal(2, current.NativeStage!.Groups[1].Flags & 2);
            Mark("effect66-retained-native-completion", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, CameraTasks = state.CameraTasks.Count, Pools = effect.Pools.Length, Cursor = state.ReadDword(CursorGlobal), FaceFlags = state.ReadDword(0x24C9190), Shards = shards, Sparks = sparks, BodyPackets = bodyCommands.Length, StagePackets = stageCommands.Length, PrivatePackets = CurrentCommands.Count(command => command.Packet.Bank == "20DFAB8"), FadePackets = CurrentFadeCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(camera);
        Assert.True(body);
        Assert.True(stage);
        Assert.True(shards);
        Assert.True(sparks);
        Assert.True(burst);
        Assert.True(text);
        Assert.True(soundGain);
        Assert.True(retired);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Contains("effect66-retained-native-completion", markers);
        File.WriteAllText(Path.Combine(output, "effect66-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), Camera = camera, Body = body, Stage = stage, Shards = shards, Sparks = sparks, BurstAlias = burst, Text = text, SoundGain = soundGain, SelectiveCameraRetirement = retired, Contacts, ContactGlyphs, completed.Frame, Observations = observations }));
    }

    private void ObserveText(BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        string id = current.NativeGraphics!.ActorIds[5]!;
        NativeBattleEnemyAllocation allocation = current.NativeAllocations.Single(item => item.Id == id);
        string prototype = current.Definition.Combatants.Single(item => item.SceneSlot == allocation.SceneSlot).Id;
        string expected = current.Definition.NativeGraphics!.Bindings.Single(item => item.ActorId == prototype).Resource.ReadText91(5);
        foreach (NativeBattleDescriptorPacket packet in current.NativeGraphics.Descriptors.Packets.Where(packet => packet.Message is { Kind: "NativeText" } message && message.ActorId == id && message.Message == expected && packet.PriorityState != 0))
        {
            Assert.Equal(240, packet.PriorityState & 0xF0);
            Assert.Equal(0, packet.Value >> 8);
            Assert.Equal((short)40, packet.Index);
            Assert.Equal((ushort)3, packet.Value);
            Mark("effect66-original-text5-descriptor", current, cold);
        }
        if (current.Events.Any(item => item.Kind == "NativeText" && item.ActorId == id && item.Message == expected && item.Amount == 40 && item.NativeMessageMode == 3))
        {
            text = true;
            Mark("effect66-original-text5-published", current, cold);
        }
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current)
    {
        NativeBattleEffect66State state = current.NativeEffectMemory!.Effect66!;
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        Assert.InRange(cursor, arena, arena + 0x20000);
        uint start = cursor > arena + 0x10000 ? arena + 0x10000 : arena;
        int first = checked((int)(start - 0x20DFAB8u));
        int end = checked((int)(cursor - 0x20DFAB8u));
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= first && command.Packet.ByteOffset < end).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private readonly record struct TextureFace(bool Quad, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static NativeBattleLogicalCommand[] BodyCommands(BattleSnapshot current)
    {
        NativeBattleModulePoolTask? task = current.NativeGraphics!.OriginalEffect!.Pools.SelectMany(Live).FirstOrDefault(task => task.Callback == 0x6257F0 && unchecked((short)task.ReadWord(12)) > 1);
        NativeBattleGraphicsModel actor = current.NativeGraphics.Models[5];
        if (task is null || (actor.Flags0 & 4) == 0)
        {
            return [];
        }
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuReference source = cpu.Rows[actor.ActiveResourceRow100 ?? throw new InvalidDataException("Effect66 body requires its current resource row.")].Require(16);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), 71, objectMask: actor.SequenceState!.MeshMask124);
        return MatchFaces(current, meshes, true);
    }

    private static NativeBattleLogicalCommand[] StageCommands(BattleSnapshot current)
    {
        if (!current.NativeGraphics!.OriginalEffect!.Pools.SelectMany(Live).Any(task => task.Callback == 0x627CF0 && unchecked((short)task.ReadWord(12)) is > 0 and <= 120) || (current.NativeStage!.Groups[1].Flags & 3) != 1)
        {
            return [];
        }
        ImmutableArray<NativeBattleRawMesh> geometry = current.NativeStage.MutableGeometry ?? current.Definition.NativeStageSource!.Geometry;
        return MatchFaces(current, geometry.Where(mesh => mesh.StageGroup == 1).ToImmutableArray(), false);
    }

    private static NativeBattleLogicalCommand[] MatchFaces(BattleSnapshot current, ImmutableArray<NativeBattleRawMesh> meshes, bool body)
    {
        HashSet<TextureFace> faces = [];
        foreach (NativeBattleRawPolygon polygon in meshes.SelectMany(mesh => mesh.Polygons))
        {
            ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
            if (body ? bytes.Length != (polygon.Quad ? 20 : 16) : bytes.Length != (polygon.Quad ? 20 : 16) && bytes.Length != (polygon.Quad ? 24 : 20))
            {
                continue;
            }
            uint uv1 = Dword(bytes, 12);
            if (body)
            {
                uv1 &= 0x01FFFFFF;
            }
            ushort uv2 = Word(bytes, polygon.Quad ? 16 : 6);
            faces.Add(new(polygon.Quad, Dword(bytes, 8), uv1, uv2, polygon.Quad ? Word(bytes, 18) : (ushort)0));
        }
        return Commands(current).Where(command => Common(command) && (!body || (command.Words[0] & 0xFFFFFF) == (current.NativeGraphics!.Models[5].Color40!.Value & 0xFFFFFF)) && (command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E) && command.Words.Length == ((command.Opcode & 8) != 0 ? 9 : 7) && command.VertexPayload4.Length == 4 && command.OrderingBucket is >= 17 && faces.Contains(new((command.Opcode & 8) != 0, command.Words[2], command.Words[4], unchecked((ushort)command.Words[6]), (command.Opcode & 8) != 0 ? unchecked((ushort)command.Words[8]) : (ushort)0))).ToArray();
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static uint Dword(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static bool Common(NativeBattleLogicalCommand command) => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1;
    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSnapshot snapshot) => snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command);
    private static NativeBattleModulePoolTask? ReadMain(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == Effect ? snapshot.NativeGraphics.OriginalEffect.Pools.SelectMany(Live).FirstOrDefault(task => task.Callback == MainCallback) : null;

    private static IEnumerable<NativeBattleModulePoolTask> Live(NativeBattleEffectTaskPool pool)
    {
        HashSet<int> visited = [];
        int? index = pool.Head;
        while (index is int selected)
        {
            Assert.InRange(selected, 0, pool.Slots.Length - 1);
            Assert.True(visited.Add(selected));
            NativeBattleModulePoolTask task = pool.Slots[selected];
            yield return task;
            index = task.Next;
        }
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
