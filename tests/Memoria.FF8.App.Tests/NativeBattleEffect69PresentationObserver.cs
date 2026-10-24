using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect69PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> ages = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool sparks;
    private bool programme;
    private bool caster;
    private bool stage;
    private bool debris;
    private bool loaded;
    private bool reloaded;
    private bool detached;
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect69State state = current.NativeEffectMemory!.Effect69!;
        state.Validate();
        NativeBattleModulePoolTask? main = ReadMain(current);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        if (age >= 0)
        {
            ages.Add(age);
        }
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.InRange(effect.Pools.Length, 1, 2);
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                Assert.Null(state.FixedBanks[Pools[index].Bank]);
                Assert.Equal(Pools[index].Bank, effect.Pools[index].BankAddress);
                Assert.Equal(Pools[index].Stride, effect.Pools[index].Stride);
                Assert.Single(effect.Pools[index].Slots);
            }
            if (effect.Pools.Length == 1)
            {
                Assert.NotNull(state.FixedBanks[MainBank]);
            }
            uint p = state.ReadDword(ArenaGlobal);
            if (age is > 0 and < 48 && Enumerable.Range(0, 128).Any(index => current.NativeEffectMemory.Arena!.Read(0x8020 + index * 16 + 15, 1)[0] != 0) && !sparks)
            {
                sparks = true;
                Mark("effect69-current-sparks", current, cold);
            }
            if (main is not null && main.ReadDword(0x1C) == ProgrammeAddress && !programme)
            {
                programme = true;
                Mark("effect69-current-ACT-cache", current, cold);
            }
            if (age >= 51 && !loaded)
            {
                NativeBattleEffectFileAsset file = effect.Binding.Effect69Resources!.ReadFile(37);
                Assert.Equal(file.Data.ToArray(), current.NativeEffectMemory.Arena!.Read(0x8020, file.Data.Length));
                loaded = true;
                Mark("effect69-file37-current-COMMON", current, cold);
            }
            if (state.ReadDword(RandomGlobal) != prior.NativeEffectMemory!.Effect69!.ReadDword(RandomGlobal) && !debris)
            {
                Assert.Contains(Enumerable.Range(0, 364), index => Word(current.NativeEffectMemory.Arena!.Read(index * 0x3C + 6, 2)) is 1 or 2);
                Assert.Contains(Enumerable.Range(0, 364), index => BinaryPrimitives.ReadInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(0x5A50 + index * 4, 4)) == -1);
                debris = true;
                Mark("effect69-detached-face-current-private-LCG", current, cold);
            }
            if (age == 110)
            {
                Mark("effect69-before-current-stage-reload512AC0", current, cold);
            }
            if (age >= 111 && !reloaded)
            {
                NativeBattleStageState owner = current.NativeStage!;
                Assert.True(current.NativeEffectIoResult >= 0);
                Assert.Equal(prior.NativeStage!.SourceAddress1D9A820, owner.SourceAddress1D9A820);
                uint destination = owner.SourceAddress1D9A820!.Value;
                byte[] directory = current.NativeEffectMemory.Arena!.Read(0x9A84, 32);
                int length = checked(BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan(24)) + 0x2020);
                Assert.Equal(current.NativeEffectMemory.Arena.Read(0x9A84, length), current.NativeFileStaging!.Read(destination, length));
                Assert.NotNull(owner.MutableGeometry);
                Assert.Equal((short)8, owner.RotationSpeed);
                Assert.Equal(0x00606868u, owner.ModelColors!.Color40);
                Assert.Equal((owner.ReloadColorSeed1D9A824!.Value & 0xFFFFFF) | 0x32000000, owner.ModelColors.Color44);
                reloaded = true;
                Mark("effect69-current-stage-reload512AC0", current, cold);
            }
            uint cursor = state.ReadDword(CursorGlobal);
            if (cursor != 0)
            {
                int bank = cursor >= p + 0x47020 ? 1 : 0;
                if (packetBanks.Add(bank))
                {
                    Mark("effect69-private-packet-bank-" + bank, current, cold);
                }
            }
        }
        else if (!detached)
        {
            Assert.NotNull(state.FixedBanks[RootBank]);
            Assert.NotNull(state.FixedBanks[MainBank]);
            detached = true;
            Mark("effect69-retained-physical-banks", current, cold);
        }
        CurrentCommands = EffectCommands(prior, current);
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
        }
        if (!caster && BodyReferences(current).Count > 0)
        {
            caster = true;
            Mark("effect69-current-caster-COMMON", current, cold);
        }
        if (!stage && StageReferences(current).Count > 0)
        {
            stage = true;
            Mark("effect69-current-stage-COMMON", current, cold);
        }
        observations.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, Age = age, Cursor = state.ReadDword(CursorGlobal), PrivateRandom = state.ReadDword(RandomGlobal), Random = current.NativeCrtRandom, FileResult = current.NativeEffectIoResult, CommonCaster = caster, CommonStage = stage });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(sparks);
        Assert.True(programme);
        Assert.True(caster);
        Assert.True(stage);
        Assert.True(debris);
        Assert.True(loaded);
        Assert.True(reloaded);
        Assert.True(detached);
        Assert.Contains(50, ages);
        Assert.Contains(110, ages);
        Assert.Contains(126, ages);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Contains((byte)0x22, opcodes);
        Assert.Contains((byte)0x2A, opcodes);
        File.WriteAllText(Path.Combine(output, "effect69-observations.json"), JsonSerializer.Serialize(new { Scenario = "Declared original startup renderer and lifecycle; no target, contact or initializer magic TIM; current stage replacement TIM upload included; no ordinary AI admission or formula replay", Markers = markers.Order(), Ages = ages.Order(), PacketBanks = packetBanks.Order(), Opcodes = opcodes.Order(), Sparks = sparks, Programme = programme, Caster = caster, Stage = stage, Debris = debris, LoadedFile37 = loaded, ReloadedStage = reloaded, RetainedBanks = detached, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleEffect69State? state = current.NativeEffectMemory?.Effect69;
        NativeBattlePrimitiveFrame? primitive = current.NativeGraphics?.PrimitiveFrame;
        if (state is null || primitive is null)
        {
            return [];
        }
        uint p = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint start = cursor >= p + 0x47020 ? p + 0x47020 : p + 0x3F020;
        int first = p == 0 ? 0 : checked((int)(start - 0x20DFAB8));
        int end = cursor == 0 ? 0 : checked((int)(cursor - 0x20DFAB8));
        ImmutableHashSet<NativeBattlePrimitiveReference> common = BodyReferences(current).Union(StageReferences(current));
        int age = ReadMain(current) is { } main ? unchecked((short)main.ReadWord(12)) : -1;
        return primitive.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= first && command.Packet.ByteOffset < end || common.Contains(command.Packet) || age is > 0 and < 48 && SparkMatches(current, command) || age is >= 74 and <= 126 && DebrisMatches(current, command)).Distinct().ToArray();
    }

    private readonly record struct FaceKey(bool Quad, uint S0, uint S1, uint S2, uint S3, int Z0, int Z1, int Z2, int Z3, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static ImmutableHashSet<NativeBattlePrimitiveReference> BodyReferences(BattleSnapshot current)
    {
        NativeBattleModulePoolTask? main = ReadMain(current);
        if (main is null || main.ReadWord(12) is < 56 or > 73)
        {
            return [];
        }
        NativeBattleGraphicsModel actor = current.NativeGraphics!.Models[4];
        Assert.NotEqual(0, actor.Flags0 & 4);
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuReference source = cpu.Rows[actor.ActiveResourceRow100!.Value].Require(16);
        NativeBattlePoseSource pose = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == current.NativeGraphics.ActorIds[4]).Resource.PoseSource!;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, source with { Offset = checked(source.Offset + offset) }, length), pose.BoneCount, objectMask: actor.SequenceState!.MeshMask124);
        return MatchFaces(current, meshes, 8);
    }

    private readonly record struct StageFace(bool Quad, uint Color, uint Uv0, uint Uv1, ushort Uv2, ushort Uv3);

    private static ImmutableHashSet<NativeBattlePrimitiveReference> StageReferences(BattleSnapshot current)
    {
        NativeBattleModulePoolTask? main = ReadMain(current);
        if (main is null || main.ReadWord(12) is < 74 or > 108)
        {
            return [];
        }
        NativeBattleStageState stage = current.NativeStage!;
        Assert.NotEqual(3, stage.Groups[1].Flags & 3);
        uint address = stage.CurrentGroupGeometryAddress(1, (pointer, length) => current.NativeFileStaging!.Read(pointer, length))!.Value;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => current.NativeFileStaging!.Read(checked(address + (uint)offset), length), stage.Groups[1].Matrices.Length);
        HashSet<StageFace> faces = [];
        foreach (NativeBattleRawPolygon face in meshes.SelectMany(mesh => mesh.Polygons))
        {
            ReadOnlySpan<byte> source = face.Bytes.AsSpan();
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(face.Quad ? 20 : 16)) | ((uint)(Word(source.Slice(14)) & 0x200) << 16);
            if ((color & 0xFFFFFF) == 0xF0F0F0)
            {
                continue;
            }
            faces.Add(new(face.Quad, color, BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(8)), BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(12)) & 0xFDFFFFFF, Word(source.Slice(face.Quad ? 16 : 6)), face.Quad ? Word(source.Slice(18)) : (ushort)0));
        }
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(Matches).Select(command => command.Packet).ToImmutableHashSet();

        bool Matches(NativeBattleLogicalCommand command)
        {
            if (!Common(command) || command.OrderingBucket != 4122 || command.Words.Length is not (7 or 9) || command.VertexPayload4.Length != 4)
            {
                return false;
            }
            ImmutableArray<uint> words = command.Words;
            bool quad = words.Length == 9;
            StageFace key = new(quad, words[0], words[2], words[4], unchecked((ushort)words[6]), quad ? unchecked((ushort)words[8]) : (ushort)0);
            int x0 = unchecked((short)words[1]);
            int y0 = unchecked((short)(words[1] >> 16));
            int x1 = unchecked((short)words[3]);
            int y1 = unchecked((short)(words[3] >> 16));
            int x2 = unchecked((short)words[5]);
            int y2 = unchecked((short)(words[5] >> 16));
            int area = unchecked(x0 * (y1 - y2) + x1 * (y2 - y0) + x2 * (y0 - y1));
            return area >= 0 && faces.Contains(key) && command.VertexPayload4.Take(quad ? 4 : 3).All(depth => depth is > 0 and <= 16383);
        }
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> MatchFaces(BattleSnapshot current, ImmutableArray<NativeBattleRawMesh> meshes, int stride)
    {
        uint scratch = checked(0x196B018u - (uint)(current.NativeModelCpuResources!.ScratchMaximum!.Value * 8));
        HashSet<FaceKey> faces = [];
        foreach (NativeBattleRawMesh mesh in meshes)
        {
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                uint[] screen = new uint[4];
                int[] depths = new int[4];
                bool valid = true;
                for (int corner = 0; corner < (face.Quad ? 4 : 3); corner++)
                {
                    int index = Word(face.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                    if (index >= mesh.Vertices.Length)
                    {
                        valid = false;
                        break;
                    }
                    (byte[] data, bool[] known) = current.NativeFileStaging!.ReadSnapshot(checked(scratch + (uint)(index * stride)), 6);
                    if (!known.All(value => value))
                    {
                        valid = false;
                        break;
                    }
                    screen[corner] = BinaryPrimitives.ReadUInt32LittleEndian(data);
                    depths[corner] = Word(data.AsSpan(4));
                }
                if (valid)
                {
                    faces.Add(new(face.Quad, screen[0], screen[1], screen[2], screen[3], depths[0], depths[1], depths[2], depths[3], BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(8)), BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(12)) & 0xFDFFFFFF, Word(face.Bytes.AsSpan().Slice(face.Quad ? 16 : 6)), face.Quad ? Word(face.Bytes.AsSpan().Slice(18)) : (ushort)0));
                }
            }
        }
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => Common(command) && command.Words.Length is 7 or 9 && command.VertexPayload4.Length == 4 && faces.Contains(new(command.Words.Length == 9, command.Words[1], command.Words[3], command.Words[5], command.Words.Length == 9 ? command.Words[7] : 0, command.VertexPayload4[0], command.VertexPayload4[1], command.VertexPayload4[2], command.VertexPayload4[3], command.Words[2], command.Words[4], unchecked((ushort)command.Words[6]), command.Words.Length == 9 ? unchecked((ushort)command.Words[8]) : (ushort)0))).Select(command => command.Packet).ToImmutableHashSet();
    }

    private static bool SparkMatches(BattleSnapshot current, NativeBattleLogicalCommand command)
    {
        if (!Common(command) || command.Words.Length != 9)
        {
            return false;
        }
        NativeBattleModuleResource sprite = current.NativeGraphics!.OriginalEffect!.Binding.Effect69Resources!.Sprite;
        for (int phase = 0; phase < 18; phase++)
        {
            ImmutableArray<byte> quads = sprite.ProjectedFrame571C80(phase).Quads;
            for (int offset = 0; offset < quads.Length; offset += 20)
            {
                ReadOnlySpan<byte> quad = quads.AsSpan().Slice(offset, 20);
                byte right = (byte)Math.Min(quad[4] + quad[0], 255);
                byte bottom = (byte)Math.Min(quad[5] + quad[2], 255);
                uint second = (uint)(right | quad[5] << 8 | (Word(quad.Slice(14)) & 0x1FF) << 16);
                if (command.Words[0] == ((uint)quad[3] << 24 | (uint)quad[1] * 0x010101) && command.Words[2] == BinaryPrimitives.ReadUInt32LittleEndian(quad.Slice(4)) && command.Words[4] == second && (ushort)command.Words[6] == (ushort)(quad[4] | bottom << 8) && (ushort)command.Words[8] == (ushort)(right | bottom << 8))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool DebrisMatches(BattleSnapshot current, NativeBattleLogicalCommand command)
    {
        if (!Common(command) || command.Words.Length is not (7 or 9) || (command.Words[0] & 0xFFFFFF) != 0xF0F0F0)
        {
            return false;
        }
        for (int index = 0; index < 364; index++)
        {
            byte[] record = current.NativeEffectMemory!.Arena!.Read(index * 0x3C, 0x3C);
            int kind = Word(record.AsSpan(6));
            if (kind == (command.Words.Length == 9 ? 2 : 1) && command.Words[2] == BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x2C)) && command.Words[4] == BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x30)) && (ushort)command.Words[6] == Word(record.AsSpan(0x34)) && (kind == 1 || (ushort)command.Words[8] == Word(record.AsSpan(0x36))))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Common(NativeBattleLogicalCommand command) => command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.WordOffset == 1 && command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E;
    private static NativeBattleModulePoolTask? ReadMain(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == MainCallback);
    private static ushort Word(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt16LittleEndian(data);
    private void Mark(string marker, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(marker))
        {
            cold(marker, current);
        }
    }
}
