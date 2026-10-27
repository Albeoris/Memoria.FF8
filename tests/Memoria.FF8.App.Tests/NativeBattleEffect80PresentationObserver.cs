using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect80PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> relocated = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool bodyObserved;
    private bool trailObserved;
    private bool sequence26;
    private bool morphObserved;
    private bool actObserved;
    private bool randomChanged;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect80State state = current.NativeEffectMemory!.Effect80!;
        state.Validate();
        Assert.Equal((byte)139, current.NativeGraphics.Models[4].ModelId4);
        Assert.Equal((byte)140, current.NativeGraphics.Models[5].ModelId4);
        Assert.Equal((ushort)1448, current.NativeModelCpuResources!.ScratchMaximum);
        if (current.NativeGraphics.Models[5].Sequence == 26)
        {
            sequence26 = true;
            Mark("effect80-target-sequence26", current, cold);
        }
        NativeBattleModulePoolTask? beforeDriver = Find(prior, DriverCallback);
        NativeBattleModulePoolTask? driver = Find(current, DriverCallback);
        if (beforeDriver?.ReadByte(0x29) == 1 && driver?.ReadByte(0x29) == 2)
        {
            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Effect80!.ReadWord(TargetAddress + 0x48));
            Contacts++;
            Mark("effect80-current-contact-handshake", current, cold);
        }
        for (int index = 0; index < current.NativeGraphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = current.NativeGraphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 5 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect80-contact-glyph", current, cold);
            }
        }
        NativeBattleModulePoolTask? bodyTask = Find(current, 0x7AF390);
        if (bodyTask is not null && bodyTask.ReadWord(0x24) is 80 or 81)
        {
            uint pointer = state.ReadDword(TrailPointerAddress);
            Assert.Equal(unchecked((ushort)(bodyTask.ReadWord(0x24) == 80 ? pointer : pointer >> 16)), state.ReadWord(TrailPointerAddress + (bodyTask.ReadWord(0x24) == 80 ? 0u : 2u)));
            Mark("effect80-before-birth-pointer-alias-" + bodyTask.ReadWord(0x24), current, cold);
        }
        if (effect.ReturnedDescriptor > 0)
        {
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                NativeBattleCureFamilyPool expected = Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal((int)expected.Capacity, pool.Slots.Length);
                Assert.Null(state.FixedBanks[pool.BankAddress]);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                {
                    callbacks.Add(task.Callback);
                    byte phase = task.ReadByte(0x29);
                    if (task.Callback == RootCallback && phase is 2 or 4 or 7 or 8 or 9)
                    {
                        Mark("effect80-root-phase-" + phase, current, cold);
                    }
                    if (task.Callback is 0x7ABF90 or 0x7B4540 && phase == 1)
                    {
                        actObserved = true;
                        Mark("effect80-act-" + task.Callback.ToString("X"), current, cold);
                    }
                }
            }
            foreach ((uint address, NativeBattleNodeSource bytes) in state.NodeBanks)
            {
                Assert.Equal(NodeSourceLengths[address], bytes.Length);
                if (state.ReadDword(address) == 1 && relocated.Add(address))
                {
                    Mark("effect80-relocated-" + address.ToString("X"), current, cold);
                }
            }
            if (current.NativeGraphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.Callback == 0x7AF390 && (task.ReadByte(0x26) & 8) != 0))
            {
                Mark("effect80-body-context-C0", current, cold);
            }
            if (state.ReadView(MorphAddress, MorphLength).ReadSnapshot(0, MorphLength).Data.Any(value => value != 0))
            {
                morphObserved = true;
                Mark("effect80-current-act-morph", current, cold);
            }
            if (state.InitializedSources.ContainsKey(TrailPointerAddress) && state.InitializedSources.ContainsKey(TrailAddress))
            {
                uint pointer = state.ReadDword(TrailPointerAddress);
                Assert.Equal(TrailAddress, pointer);
                Assert.Equal(unchecked((ushort)pointer), state.ReadWord(TrailPointerAddress));
                Assert.Equal(unchecked((ushort)(pointer >> 16)), state.ReadWord(TrailPointerAddress + 2));
                Mark("effect80-current-trail-pointer-alias", current, cold);
            }
            if (prior.NativeEffectMemory?.Effect80 is { } previous && previous.ReadDword(TrailRandomGlobal) != state.ReadDword(TrailRandomGlobal))
            {
                randomChanged = true;
                Mark("effect80-private-lcg", current, cold);
            }
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> body = BodyPacketReferences(current);
        ImmutableHashSet<NativeBattlePrimitiveReference> trail = TrailPacketReferences(current);
        if (!body.IsEmpty)
        {
            bodyObserved = true;
            Mark("effect80-current-deformed-body", current, cold);
        }
        if (!trail.IsEmpty)
        {
            trailObserved = true;
            Mark("effect80-current-trail-packets", current, cold);
        }
        CurrentCommands = PrivateCommands(current).Concat(current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => body.Contains(command.Packet) || trail.Contains(command.Packet))).Distinct().ToArray();
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
        }
        if (CurrentCommands.Length != 0)
        {
            Mark("effect80-owned-packets", current, cold);
        }
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            Mark("effect80-physical-banks-retained", current, cold);
        }
        observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, DriverPhase = driver?.ReadByte(0x29), Sequence = current.NativeGraphics.Models[5].Sequence, Contacts, ContactGlyphs, BodyPackets = body.Count, TrailPackets = trail.Count, OwnedPackets = CurrentCommands.Length, Cursor = state.ReadDword(CursorGlobal), SecondaryCursor = state.ReadDword(SecondaryCursorGlobal), Random = state.ReadDword(TrailRandomGlobal) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.True(bodyObserved);
        Assert.True(trailObserved);
        Assert.True(sequence26);
        Assert.True(actObserved);
        Assert.True(morphObserved);
        Assert.True(randomChanged);
        Assert.Equal(NodeSourceLengths.Keys.Order().ToArray(), relocated.Order().ToArray());
        Assert.Contains("effect80-physical-banks-retained", markers);
        File.WriteAllText(Path.Combine(output, "effect80-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), RelocatedSources = relocated.Order(), Opcodes = opcodes.Order(), Body = bodyObserved, Trail = trailObserved, Sequence26 = sequence26, Acts = actObserved, Morph = morphObserved, PrivateRandomChanged = randomChanged, Contacts, ContactGlyphs, Observations = observations }));
    }

    private readonly record struct BodyFace(byte Opcode, uint Color, int Bucket, uint S0, uint S1, uint S2, uint S3, int Z0, int Z1, int Z2, int Z3, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static ImmutableHashSet<NativeBattlePrimitiveReference> BodyPacketReferences(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        NativeBattleModulePoolTask? body = effect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x7AF390 && (task.ReadByte(0x26) & 12) == 8);
        if (body is null)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleEffect80State state = current.NativeEffectMemory!.Effect80!;
        NativeBattleGraphicsModel actor = current.NativeGraphics.Models[checked((int)((state.ReadDword(BodyContextAddress + 0x64) - 0x1D972C0u) / 156))];
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources ?? throw new InvalidDataException("The deformed body proof requires its current CPU resource owner.");
        NativeBattleModelCpuResourceRow resource = cpu.Rows[actor.ActiveResourceRow100 ?? throw new InvalidDataException("The deformed body proof requires its current resource row.")];
        NativeBattleModelCpuReference source = resource.Require(16);
        string actorId = current.NativeGraphics.ActorIds[actor.Index] ?? throw new InvalidDataException("The deformed body proof requires its actual allocated actor.");
        NativeBattleGraphicsBinding? binding = current.Definition.NativeGraphics!.Bindings.SingleOrDefault(item => item.ActorId == actorId);
        if (binding is null)
        {
            NativeBattleEnemyAllocation allocation = Assert.Single(current.NativeAllocations, item => item.Id == actorId);
            BattleCombatantDefinition prototype = Assert.Single(current.Definition.Combatants, item => !item.IsPlayer && item.SceneSlot == allocation.SceneSlot);
            binding = Assert.Single(current.Definition.NativeGraphics.Bindings, item => item.ActorId == prototype.Id);
        }
        NativeBattlePoseSource pose = binding.Resource.PoseSource ?? throw new InvalidDataException("The deformed body proof requires its selected skeleton.");
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), pose.BoneCount, objectMask: actor.SequenceState?.MeshMask124 ?? throw new InvalidDataException("The deformed body proof requires its actual object mask."));
        uint color = (actor.Color40 ?? throw new InvalidDataException("The deformed body proof requires its current color.")) & 0xFFFFFF;
        short bias = unchecked((short)state.ReadWord(BodyContextAddress + 0xC0));
        HashSet<BodyFace> faces = [];
        foreach (NativeBattleRawMesh mesh in meshes)
        {
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
                int count = polygon.Quad ? 4 : 3;
                uint[] screens = new uint[4];
                int[] depths = new int[4];
                for (int corner = 0; corner < count; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(corner * 2, 2)) & 0xFFF;
                    Assert.InRange(index, 0, mesh.Vertices.Length - 1);
                    byte[] row = state.Read(RowsAddress + (uint)(index * 16 + 8), 8);
                    screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(row);
                    depths[corner] = BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(4));
                }
                int sum = unchecked(depths[0] + depths[1] + depths[2]);
                int depth = polygon.Quad ? unchecked(sum + depths[3]) >> 6 : (sum / 3) >> 4;
                int bucket = checked(17 + Math.Max(0, unchecked(depth + bias)));
                byte opcode = (byte)((polygon.Quad ? 0x2C : 0x24) | ((bytes[15] & 2) != 0 ? 2 : 0));
                faces.Add(new(opcode, color, bucket, screens[0], screens[1], screens[2], screens[3], depths[0], depths[1], depths[2], depths[3], BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(polygon.Quad ? 16 : 6, 2)), polygon.Quad ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18, 2)) : (ushort)0));
            }
        }
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(Matches).Select(command => command.Packet).ToImmutableHashSet();

        bool Matches(NativeBattleLogicalCommand command)
        {
            if (command.Packet.Bank is not ("off_B6D070" or "off_B6D094") || command.WordOffset != 1 || command.Opcode is not (0x24 or 0x26 or 0x2C or 0x2E) || command.OrderingBucket is not int bucket)
            {
                return false;
            }
            bool quad = (command.Opcode & 8) != 0;
            if (command.Words.Length != (quad ? 9 : 7) || command.VertexPayload4.Length != 4)
            {
                return false;
            }
            ImmutableArray<uint> words = command.Words;
            ImmutableArray<int> depths = command.VertexPayload4;
            return faces.Contains(new(command.Opcode, words[0] & 0xFFFFFF, bucket, words[1], words[3], words[5], quad ? words[7] : 0, depths[0], depths[1], depths[2], depths[3], words[2], words[4], unchecked((ushort)words[6]), quad ? unchecked((ushort)words[8]) : (ushort)0));
        }
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> TrailPacketReferences(BattleSnapshot current)
    {
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.WordOffset == 1 && command.Opcode == 0x3E && command.Words.Length == 12 && command.VertexPayload4.SequenceEqual(new[] { 0, 0, 0, 0 }) && MatchesTrail(command.Words)).Select(command => command.Packet).ToImmutableHashSet();
    }

    private static bool MatchesTrail(ImmutableArray<uint> words)
    {
        return words[2] == 0x3E54F808 && words[5] == 0x00B9F838 && unchecked((ushort)words[8]) == 0xF808 && unchecked((ushort)words[11]) == 0xF838 || words[2] == 0x3E54F908 && words[5] == 0x00D9F938 && unchecked((ushort)words[8]) == 0xF908 && unchecked((ushort)words[11]) == 0xF938;
    }

    private static NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current)
    {
        NativeBattleEffect80State state = current.NativeEffectMemory!.Effect80!;
        uint source = state.ReadDword(InitializerGlobal);
        uint primary = state.ReadDword(CursorGlobal);
        uint secondary = state.ReadDword(SecondaryCursorGlobal);
        if (source == 0)
        {
            return [];
        }
        bool Contains(int offset, uint cursor)
        {
            if (cursor <= source || cursor > source + 0x18000)
            {
                return false;
            }
            uint start = source + ((cursor - source - 1) / 0x8000) * 0x8000;
            return offset >= checked((int)(start - 0x20DFAB8u)) && offset < checked((int)(cursor - 0x20DFAB8u));
        }
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && (Contains(command.Packet.ByteOffset, primary) || Contains(command.Packet.ByteOffset, secondary))).ToArray();
    }

    private static NativeBattleModulePoolTask? Find(BattleSnapshot snapshot, uint callback) => snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == Effect ? snapshot.NativeGraphics.OriginalEffect.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == callback) : null;

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
