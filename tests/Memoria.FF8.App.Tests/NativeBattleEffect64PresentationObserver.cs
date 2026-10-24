using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect64PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool currentBody;
    private bool warmScripts;
    private bool programme;
    private bool particles;
    private bool flash;
    private bool sequence16;
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect64State state = current.NativeEffectMemory!.Effect64!;
        NativeBattleEffect64Resources resources = effect.Binding.Effect64Resources!;
        state.Validate();
        Assert.Equal((byte)140, current.NativeGraphics.Models[5].ModelId4);
        Assert.Equal((ushort)1448, current.NativeModelCpuResources!.ScratchMaximum);
        if (current.NativeGraphics.Models[5].Sequence == 16)
        {
            sequence16 = true;
            Mark("effect64-sequence16-native-handshake", current, cold);
        }
        NativeBattleModulePoolTask? main = ReadMain(current);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        if (effect.ReturnedDescriptor > 0)
        {
            Assert.True(effect.Pools.Length is 1 or 5);
            Assert.Null(state.RootBankStorage);
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                NativeBattleCureFamilyPool shape = Pools[index];
                Assert.Equal(shape.Descriptor, pool.DescriptorAddress);
                Assert.Equal(index == 0 ? RootBank : checked(state.ReadDword(ArenaGlobal) + shape.Bank), pool.BankAddress);
                Assert.Equal(shape.Stride, pool.Stride);
                Assert.Equal((int)shape.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in Live(pool))
                {
                    callbacks.Add(task.Callback);
                    if (task.Callback == 0x6E6310)
                    {
                        programme = true;
                        Mark("effect64-current-ACT", current, cold);
                    }
                    if (task.Callback == 0x6E67F0)
                    {
                        particles = true;
                        Mark("effect64-current-particles", current, cold);
                    }
                }
            }
            if (age is 1 or 10 or 19 or 24 or 43 or 80 or 100 or 166 or 170)
            {
                Mark("effect64-main-age-" + age, current, cold);
            }
            foreach ((uint address, NativeBattleEffectArena script) in state.ScriptSources)
            {
                if (!script.Read(0, ScriptLengths[address]).AsSpan().SequenceEqual(resources.ReadOriginalScript(address).AsSpan()))
                {
                    warmScripts = true;
                    Mark("effect64-current-script-counter", current, cold);
                    break;
                }
            }
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> body = BodyReferences(current);
        if (!body.IsEmpty)
        {
            currentBody = true;
            Mark("effect64-current-COMMON-body", current, cold);
        }
        CurrentCommands = PrivateCommands(current).Concat(current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => body.Contains(command.Packet))).Distinct().ToArray();
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
            if (command.Packet.Bank == "20DFAB8")
            {
                int offset = command.Packet.ByteOffset - FirstPacketOffset;
                int bank = offset / 0x10000;
                Assert.InRange(bank, 0, 1);
                if (packetBanks.Add(bank))
                {
                    Mark("effect64-private-bank-" + bank, current, cold);
                }
                if (command.Opcode == 0x2A && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == 0x00D80000 && command.Words[4] == 0x00D80140)
                {
                    flash = true;
                    Mark("effect64-fullviewport-flash", current, cold);
                }
            }
        }
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.NotNull(state.RootBankStorage);
            Assert.Equal(ScriptCount, state.ScriptSources.Count);
            Assert.Equal(3u, current.NativeGraphics.Models[5].SequenceState!.MeshMask124);
            Assert.All(ReadMeshes(current).SelectMany(mesh => mesh.Polygons), face => Assert.Equal(0, face.Bytes[15] & 0x80));
            Mark("effect64-cleanup-retained-banks-and-scripts", current, cold);
        }
        observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, Age = age, Sequence = current.NativeGraphics.Models[5].Sequence, Flags = current.NativeGraphics.Models[5].Flags0, Cursor = state.ReadDword(CursorGlobal), Scripts = state.ScriptSources.Count, CommonBodyPackets = body.Count, OwnedPackets = CurrentCommands.Length });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(sequence16);
        Assert.True(currentBody);
        Assert.True(warmScripts);
        Assert.True(programme);
        Assert.True(particles);
        Assert.True(flash);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Contains("effect64-cleanup-retained-banks-and-scripts", markers);
        File.WriteAllText(Path.Combine(output, "effect64-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), CurrentCommonBody = currentBody, WarmScripts = warmScripts, Programme = programme, Particles = particles, FullviewportFlash = flash, Contacts = 0, ContactGlyphs = 0, Observations = observations }));
    }

    private static NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current)
    {
        NativeBattleEffect64State state = current.NativeEffectMemory!.Effect64!;
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor < arena + FirstPacketOffset)
        {
            return [];
        }
        int end = checked((int)(cursor - arena));
        int start = FirstPacketOffset + ((end - FirstPacketOffset - 1) / 0x10000) * 0x10000;
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end).ToArray();
    }

    private readonly record struct BodyFace(byte Opcode, uint Color, int Bucket, uint S0, uint S1, uint S2, uint S3, int Z0, int Z1, int Z2, int Z3, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static ImmutableHashSet<NativeBattlePrimitiveReference> BodyReferences(BattleSnapshot current)
    {
        if (ReadMain(current) is null || (current.NativeGraphics!.Models[5].Flags0 & 4) == 0)
        {
            return ImmutableHashSet<NativeBattlePrimitiveReference>.Empty;
        }
        NativeBattleEffectMemory memory = current.NativeEffectMemory!;
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics!;
        NativeBattleFileStaging staging = current.NativeFileStaging!;
        uint scratch = checked(0x196B018u - (uint)(current.NativeModelCpuResources!.ScratchMaximum!.Value * 8));
        uint color = current.NativeGraphics.Models[5].Color40!.Value & 0xFFFFFF;
        HashSet<BodyFace> expected = [];
        foreach (NativeBattleRawMesh mesh in ReadMeshes(current))
        {
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                ReadOnlySpan<byte> raw = face.Bytes.AsSpan();
                if ((raw[15] & 0x80) != 0)
                {
                    continue;
                }
                int corners = face.Quad ? 4 : 3;
                byte[][] rows = new byte[corners][];
                bool valid = true;
                for (int corner = 0; corner < corners; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(corner * 2, 2)) & 0xFFF;
                    Assert.InRange(index, 0, mesh.Vertices.Length - 1);
                    uint address = checked(scratch + (uint)(index * 8));
                    byte clip = staging.Read(address + 6, 1)[0];
                    if (clip >= 0x10)
                    {
                        valid = false;
                        break;
                    }
                    rows[corner] = staging.Read(address, 8);
                }
                if (!valid || rows.Aggregate(0, (value, row) => value | row[6]) >= 0x10 || rows.Aggregate(255, (value, row) => value & row[6]) != 0 || rows.Skip(1).Any(row => row[7] != rows[0][7]))
                {
                    continue;
                }
                uint[] xy = rows.Select(row => BinaryPrimitives.ReadUInt32LittleEndian(row)).ToArray();
                int[] z = rows.Select(row => (int)BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan(4))).ToArray();
                NativeBattleGeometryRegisters geometry = memory.GeometryRegisters! with { Sxy = xy.Take(3).Select(value => (uint?)value).ToImmutableArray(), Sz = face.Quad ? z.Select(value => (uint?)unchecked((uint)value)).ToImmutableArray() : [0, unchecked((uint)z[0]), unchecked((uint)z[1]), unchecked((uint)z[2])] };
                NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Clip45EE10(new(geometry, memory.ScriptedColorRegisters!));
                if (unchecked((int)registers.Geometry.Mac0!.Value) < 0)
                {
                    continue;
                }
                registers = face.Quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4!.Value) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3!.Value);
                int bucket = checked(17 + (int)registers.Geometry.Otz!.Value);
                byte opcode = (byte)((face.Quad ? 0x2C : 0x24) | ((BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(14)) & 0x200) >> 8));
                uint uv1 = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(12));
                uv1 &= 0xFDFFFFFF;
                expected.Add(new(opcode, color, bucket, xy[0], xy[1], xy[2], face.Quad ? xy[3] : 0, z[0], z[1], z[2], face.Quad ? z[3] : 0, BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(8)), uv1, BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(face.Quad ? 16 : 6)), face.Quad ? BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(18)) : (ushort)0));
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
            ImmutableArray<uint> words = command.Words;
            ImmutableArray<int> depth = command.VertexPayload4;
            return words.Length == (quad ? 9 : 7) && depth.Length == 4 && expected.Contains(new(command.Opcode, words[0] & 0xFFFFFF, bucket, words[1], words[3], words[5], quad ? words[7] : 0, depth[0], depth[1], depth[2], depth[3], words[2], words[4], unchecked((ushort)words[6]), quad ? unchecked((ushort)words[8]) : (ushort)0));
        }
    }

    private static ImmutableArray<NativeBattleRawMesh> ReadMeshes(BattleSnapshot current)
    {
        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[5];
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuReference source = cpu.Rows[model.ActiveResourceRow100!.Value].Require(16);
        int bones = model.CurrentBodyBoneMatrices!.Matrices.Length;
        return NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), bones);
    }

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
