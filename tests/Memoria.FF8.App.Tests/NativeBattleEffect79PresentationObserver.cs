using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect79PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<byte> rootPhases = [];
    private readonly HashSet<byte> mainPhases = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> relocated = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool contactHandshake;
    private bool commonBody;
    private bool scriptDestinations;
    private bool detached;
    private bool musicLoaded;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect79State state = current.NativeEffectMemory!.Effect79!;
        state.Validate();
        Assert.Equal((byte)138, current.NativeGraphics.Models[4].ModelId4);
        Assert.Equal((byte)139, current.NativeGraphics.Models[3].ModelId4);
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Equal(Pools.Length, effect.Pools.Length);
            Assert.All(state.FixedBanks.Values, bank => Assert.Null(bank));
            Assert.NotNull(state.ProgramData);
            Assert.Equal(ScriptLength, state.ProgramData!.Value.Length);
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleCureFamilyPool expected = Pools[index];
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                {
                    callbacks.Add(task.Callback);
                    if (task.Callback == RootCallback)
                    {
                        byte phase = task.ReadByte(0x29);
                        Assert.InRange(phase, (byte)0, (byte)12);
                        if (rootPhases.Add(phase))
                        {
                            if (phase is 8 or 9)
                            {
                                ObserveMusicSource(current, phase == 9);
                            }
                            if (phase is 3 or 6 or 7 or 8 or 9 or 10)
                            {
                                Mark("effect79-root-phase-" + phase, current, cold);
                            }
                        }
                    }
                    if (task.Callback == 0x7B4FF0)
                    {
                        byte phase = task.ReadByte(0x29);
                        Assert.InRange(phase, (byte)0, (byte)4);
                        Assert.Equal((byte)3, task.ReadByte(0x2D));
                        mainPhases.Add(phase);
                        NativeBattleModulePoolTask? previous = prior.NativeGraphics!.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(item => item.Callback == 0x7B4FF0);
                        if (phase == 2 && previous?.ReadByte(0x29) == 1 && !contactHandshake)
                        {
                            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Effect79!.ReadWord(TargetAddress + 0x48));
                            contactHandshake = true;
                            Mark("effect79-contact-handshake", current, cold);
                        }
                    }
                }
            }
            foreach ((uint address, NativeBattleNodeSource bytes) in state.NodeBanks)
            {
                Assert.Equal(NodeSourceLengths[address], bytes.Length);
                if (state.ReadDword(address) == 1 && relocated.Add(address))
                {
                    Mark("effect79-relocated-" + address.ToString("X"), current, cold);
                }
            }
            if (Enumerable.Range(1, 5).Any(index => state.ReadDword(ScriptAddress + (uint)(index * 16 + 12)) != 0) && !scriptDestinations)
            {
                scriptDestinations = true;
                Mark("effect79-current-io-script", current, cold);
            }
            ImmutableHashSet<NativeBattlePrimitiveReference> prefix = BodyPacketReferences(current);
            if (!prefix.IsEmpty && !commonBody)
            {
                commonBody = true;
                Mark("effect79-current-deformed-body", current, cold);
            }
            foreach (NativeBattleLogicalCommand command in EffectCommands(prior, current))
            {
                opcodes.Add(command.Opcode);
                if (command.Packet.Bank == "20DFAB8")
                {
                    int initializer = checked((int)(state.ReadDword(InitializerGlobal) - 0x20DFAB8u));
                    int bank = (command.Packet.ByteOffset - initializer) / 0x8000;
                    Assert.InRange(bank, 0, 1);
                    if (packetBanks.Add(bank))
                    {
                        Mark("effect79-packet-bank-" + bank, current, cold);
                    }
                }
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            detached = true;
            Mark("effect79-physical-banks-detached", current, cold);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, RootPhases = rootPhases.Order().ToArray(), TargetPhase = state.ReadWord(TargetAddress + 0x40), TargetAge = state.ReadWord(TargetAddress + 0x46), ContactFlag = state.ReadWord(TargetAddress + 0x48), BodyFlag = state.ReadWord(TargetAddress + 0x4C), EmitterCursor = state.ReadWord(0x25E8950), GeometryCursor = state.ReadWord(0x25EAF40), IoCursor = state.ReadDword(0x25EA348), Completion = state.Read(0x25DF340, 1)[0], Random = current.NativeCrtRandom, Relocated = relocated.Order().ToArray() });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.NotEmpty(relocated);
        Assert.True(scriptDestinations);
        Assert.True(commonBody);
        Assert.True(contactHandshake);
        Assert.True(detached);
        Assert.True(musicLoaded);
        Assert.Contains((byte)8, rootPhases);
        Assert.Contains((byte)4, mainPhases);
        Assert.Contains(RootCallback, callbacks);
        Assert.Contains(0x7B51C0u, callbacks);
        Assert.Contains(0x7B82C0u, callbacks);
        Assert.Contains(0x7BC840u, callbacks);
        Assert.Contains(0x7BDAD0u, callbacks);
        Assert.NotEmpty(opcodes);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.NotNull(initialRandom);
        Assert.NotEqual(initialRandom, lastRandom);
        File.WriteAllText(Path.Combine(output, "effect79-observations.json"), JsonSerializer.Serialize(new { Scenario = "Declared post-formula death-action renderer and lifecycle; ordinary death-AI, KO, selector admission and formula execution are excluded", Markers = markers.Order(), RootPhases = rootPhases.Order(), MainPhases = mainPhases.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), Relocated = relocated.Order(), CurrentScript = scriptDestinations, CommonBody = commonBody, MusicLoaded = musicLoaded, ContactHandshake = contactHandshake, Detached = detached, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot prior, BattleSnapshot current)
    {
        ImmutableHashSet<NativeBattlePrimitiveReference> prefix = BodyPacketReferences(current);
        return EffectCommands(current).Concat(current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => prefix.Contains(item.Command.Packet)).Select(item => item.Command)).Distinct().ToArray();
    }

    private void ObserveMusicSource(BattleSnapshot current, bool completed)
    {
        NativeBattleOriginalEffectState effect = current.NativeGraphics!.OriginalEffect!;
        NativeBattleEffect79Resources resources = effect.Binding.Effect79Resources ?? throw new InvalidDataException("The music proof requires the selected effect resources.");
        ImmutableArray<byte> file = resources.ReadFile(401).Data;
        NativeBattleMusicLoadSource expected = NativeBattleMusicLoadSource.Read501A20(file);
        NativeBattleMusicProcessState process = current.NativeMusicProcess ?? throw new InvalidDataException("The music proof requires its actual current process.");
        NativeBattleMusicLoadPointers source = process.LoadSource ?? throw new InvalidDataException("The music proof requires actual retained load pointers.");
        uint arenaBase = current.NativeEffectMemory!.Effect79!.ReadDword(InitializerGlobal);
        Assert.Equal(checked(arenaBase + (uint)NativeBattleDatDirectory.Section(file.AsSpan(), 0)), source.SoundBankAddress);
        Assert.Equal(checked(arenaBase + (uint)NativeBattleDatDirectory.Section(file.AsSpan(), 1)), source.MusicAddress);
        Assert.Equal(expected.Music.Length, source.MusicLength);
        int offset = checked((int)(source.MusicAddress - 0x20DFAB8u));
        Assert.Equal(expected.Music.ToArray(), current.NativeEffectMemory.Arena!.Read(offset, source.MusicLength));
        if (completed)
        {
            byte buffer = process.Commands?.TargetBufferAddress switch
            {
                0x1CE075C => 0,
                0x1CDC750 => 1,
                _ => throw new InvalidDataException("The music proof requires its actual selected destination buffer.")
            };
            Assert.Equal(expected.ReadIdentity(), process.Buffers.ReadBuffer(buffer));
            Assert.NotEqual((byte)0, current.NativeEffectMemory.Effect79.Read(0x25DF340, 1)[0]);
            musicLoaded = true;
        }
    }

    private readonly record struct BodyFace(byte Opcode, uint Color, int Bucket, uint S0, uint S1, uint S2, uint S3, int Z0, int Z1, int Z2, int Z3, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static ImmutableHashSet<NativeBattlePrimitiveReference> BodyPacketReferences(BattleSnapshot current)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        NativeBattleModulePoolTask? body = effect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x7BCF10 && (task.ReadByte(0x26) & 12) == 8);
        if (body is null)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleEffect79State state = current.NativeEffectMemory!.Effect79!;
        NativeBattleGraphicsModel actor = current.NativeGraphics.Models[body.ReadByte(0x2C)];
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources ?? throw new InvalidDataException("The deformed body proof requires its current CPU resource owner.");
        NativeBattleModelCpuResourceRow resource = cpu.Rows[actor.ActiveResourceRow100 ?? throw new InvalidDataException("The deformed body proof requires its current resource row.")];
        NativeBattleModelCpuReference source = resource.Require(16);
        NativeBattlePoseSource pose = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == current.NativeGraphics.ActorIds[actor.Index]).Resource.PoseSource ?? throw new InvalidDataException("The deformed body proof requires its selected skeleton.");
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), pose.BoneCount, objectMask: actor.SequenceState?.MeshMask124 ?? throw new InvalidDataException("The deformed body proof requires its actual object mask."));
        uint color = (actor.Color40 ?? throw new InvalidDataException("The deformed body proof requires its current color.")) & 0xFFFFFF;
        short bias = unchecked((short)state.ReadWord(BodyContextAddress + 0xD0));
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
                    byte[] row = state.Read(TransformedAddress + (uint)(index * 16 + 8), 8);
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

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        NativeBattleEffect79State state = current.NativeEffectMemory!.Effect79!;
        uint source = state.ReadDword(InitializerGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (source == 0 || cursor == 0)
        {
            return [];
        }
        Assert.InRange(cursor, source, source + 0x10000);
        uint start = cursor >= source + 0x8000 ? source + 0x8000 : source;
        int first = checked((int)(start - 0x20DFAB8u));
        int end = checked((int)(cursor - 0x20DFAB8u));
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= first && item.Command.Packet.ByteOffset < end).Select(item => item.Command).ToArray();
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
