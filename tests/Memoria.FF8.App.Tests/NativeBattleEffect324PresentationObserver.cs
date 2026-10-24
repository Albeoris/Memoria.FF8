using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect324PresentationObserver(bool allowRejectedHistory = false)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<string> banks = [];
    private readonly HashSet<int> recordTypes = [];
    private readonly HashSet<uint> sprites = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool capturedBones;
    private bool currentBones;
    private bool historyGeometry;
    private bool warmNativeRejectedHistory;
    private object? warmRejectionProof;
    private bool modelMask;
    private bool vibrationStarted;
    private bool vibrationFinished;
    private bool mainRetired;
    public bool HistoryGeometry => historyGeometry;
    public bool WarmNativeRejectedHistory => warmNativeRejectedHistory;
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
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect324State state = current.NativeEffectMemory!.Effect324!;
        NativeBattleEffect324Resources resources = effect.Binding.Effect324Resources!;
        int caster = checked((int)state.ReadDword(CasterGlobal));
        Assert.InRange(caster, 3, 6);
        Assert.Equal((byte)99, graphics.Models[caster].ModelId4);
        Assert.Equal(28, graphics.Models[caster].CurrentBodyBoneMatrices!.Matrices.Length);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            track |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            view |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect324-camera-current8-24-owners", current, cold);
        }
        if (!vibrationStarted && main is not null && age >= 1)
        {
            int handle = unchecked((int)state.ReadDword(VibrationGlobal));
            NativeVibrationSlot slot = current.NativeVibrationProcess!.Slots[handle & 3];
            Assert.Equal(handle, slot.Generation * 16 | slot.Id);
            Assert.Equal((byte)128, slot.Priority);
            NativeVibrationBank bank = resources.VibrationBank;
            int offset = bank.Offsets[1];
            int firstLength = BinaryPrimitives.ReadUInt16LittleEndian(bank.Bytes.AsSpan(offset, 2));
            int secondLength = BinaryPrimitives.ReadUInt16LittleEndian(bank.Bytes.AsSpan(offset + 2, 2));
            Assert.Equal(firstLength, slot.A.Length);
            Assert.Equal(secondLength, slot.B.Length);
            Assert.Equal(bank.Bytes.Skip(offset + 4).Take(firstLength + 2).ToArray(), slot.A.Bytes.ToArray());
            Assert.True(current.NativeVibrationProcess.IsPlaying(handle));
            vibrationStarted = true;
            Mark("effect324-returned-vibration-handle", current, cold);
        }
        if (oldAge == 197)
        {
            Assert.False(current.NativeVibrationProcess!.IsPlaying(unchecked((int)state.ReadDword(VibrationGlobal))));
            vibrationFinished = true;
            Mark("effect324-vibration197-query-stop-boundary", current, cold);
        }
        if (oldAge == 130)
        {
            Contacts++;
            Mark("effect324-first-contact130", current, cold);
        }
        if (oldAge == 200 && main is null)
        {
            mainRetired = true;
            Mark("effect324-main201-retirement", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        int live = 0;
        for (int index = 0; index < RecordCount; index++)
        {
            uint address = RecordsAddress + (uint)(index * RecordStride);
            uint flags = state.ReadDword(address);
            if (flags == 0)
            {
                continue;
            }
            Assert.Contains((int)flags, new[] { 1, 2 });
            recordTypes.Add((int)flags);
            Assert.InRange(state.ReadWord(address + 4), (ushort)0, (ushort)13);
            live++;
        }
        NativeBattleModulePoolTask? history = previous.Values.FirstOrDefault(task => task.Callback == 0x597520);
        NativeBattleModulePoolTask? currentHistory = tasks.Values.FirstOrDefault(task => task.Callback == 0x597520);
        if (currentHistory is not null)
        {
            Assert.Equal(0u, graphics.Models[caster].SequenceState!.MeshMask124 & 2);
            modelMask = true;
            var original = state.ReadView(OriginalBonesAddress, BonesLength).ReadSnapshot(0, BonesLength);
            var savedCurrent = state.ReadView(CurrentBonesAddress, BonesLength).ReadSnapshot(0, BonesLength);
            capturedBones |= Enumerable.Range(0, 28).All(index => original.Known.AsSpan(index * 32, 18).ToArray().All(value => value));
            currentBones |= Enumerable.Range(0, 28).All(index => savedCurrent.Known.AsSpan(index * 32, 18).ToArray().All(value => value));
            if (currentHistory.ReadWord(12) is 1 or 7 or 16 or 17 or 29 or 32)
            {
                Mark("effect324-current-bone-history-age-" + currentHistory.ReadWord(12), current, cold);
            }
        }
        NativeBattleLogicalCommand[] privateCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is NativeBattleEffect324PacketArena.FirstBank or NativeBattleEffect324PacketArena.SecondBank).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        foreach (NativeBattleLogicalCommand command in privateCommands)
        {
            banks.Add(command.Packet.Bank);
            Assert.All(command.VertexPayload4, depth => Assert.InRange(depth, 0, 65535));
        }
        foreach (uint source in NativeBattleEffect324Resources.SpriteAddresses)
        {
            if (privateCommands.Any(command => MatchesSprite(command, resources.Sprite(source), source == 0xCD0B2C ? 8 : 0)))
            {
                sprites.Add(source);
                Mark("effect324-current-sprite-material-" + source.ToString("X8"), current, cold);
            }
        }
        NativeBattleLogicalCommand[] body = [];
        if (history is not null || currentHistory is not null)
        {
            int bodyAge = history?.ReadWord(12) ?? 0;
            body = graphics.PrimitiveFrame.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is not (NativeBattleEffect324PacketArena.FirstBank or NativeBattleEffect324PacketArena.SecondBank) && MatchesHistory(command, current, resources, caster, bodyAge)).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (body.Length > 0)
            {
                historyGeometry = true;
                Mark("effect324-real-ordinaryCOMMON-current-history-material", current, cold);
            }
        }
        if (allowRejectedHistory && !warmNativeRejectedHistory && history?.ReadWord(12) == 17 && body.Length == 0)
        {
            Assert.Equal(prior.NativeGraphics!.Callback + 1, graphics.Callback);
            var result = ReproduceHistory(prior, current);
            Assert.Equal(17, result.Age);
            Assert.Empty(result.Draw.Packets.Attempts);
            warmRejectionProof = new { PriorFrame = prior.Frame, current.Frame, PriorNativeCallback = prior.NativeGraphics.Callback, CurrentNativeCallback = graphics.Callback, result.GeometryAddress, result.SkeletonAddress, result.Age, result.World, result.View, result.Draw.ProjectedBytesWritten, NativePacketCount = result.Draw.Packets.Attempts.Length, ActualHistoryCommands = body.Length, Scope = "Actual warm current-source geometry and captured bones/world with prior camera; native viewport/NCLIP rejection; no alpha, model, HP, status or camera reset" };
            warmNativeRejectedHistory = true;
            Mark("effect324-warm-native-history-rejected-current-source-age17", current, cold);
        }
        CurrentCommands = privateCommands.Concat(body).ToArray();
        if (age is 1 or 8 or 12 or 18 or 50 or 125 or 130 or 135 or 197 or 200)
        {
            Mark("effect324-before-main-age-" + age, current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Caster = caster, LiveRecords = live, Cursor = state.ReadDword(CursorGlobal), PrivateCommands = privateCommands.Length, HistoryCommands = body.Length, WarmNativeRejectedHistory = warmNativeRejectedHistory, Contacts, ContactGlyphs });
    }

    internal static (NativeBattleRawModelBodyDraw Draw, uint GeometryAddress, uint SkeletonAddress, int Age, NativeBattleActionMatrix World, NativeBattleActionMatrix View) ReproduceHistory(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleEffect324State state = current.NativeEffectMemory!.Effect324!;
        int actor = checked((int)state.ReadDword(CasterGlobal));
        NativeBattleGraphicsModel entering = prior.NativeGraphics!.Models[actor];
        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[actor];
        Assert.Equal((byte)99, model.ModelId4);
        Assert.Null(model.ActiveStagedResourceRow100);
        int row = model.ActiveResourceRow100 ?? throw new InvalidDataException("History requires its actual current CPU resource row.");
        Assert.Equal(entering.ActiveResourceRow100, row);
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuResourceRow source = cpu.Rows[row];
        NativeBattleModelCpuReference geometry = source.Require(16);
        NativeBattleModelCpuReference skeleton = source.Require(12);
        Assert.Equal(prior.NativeModelCpuResources!.Rows[row].Require(16), geometry);
        Assert.Equal(prior.NativeModelCpuResources.Rows[row].Require(12), skeleton);
        Assert.Equal((byte)28, NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, skeleton, 1)[0]);
        Assert.Equal(entering.CurrentBodyBoneMatrices!.SkeletonSha256, model.CurrentBodyBoneMatrices!.SkeletonSha256);
        int available = source.Lengths[16];
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, 28, objectMask: 2);
        Assert.Equal(3, meshes.Length);
        Assert.Equal(17, meshes[1].Vertices.Length);
        Assert.Equal(20, meshes[1].Polygons.Length);
        Assert.All(meshes[1].Polygons, polygon => Assert.Equal(polygon.Quad ? 20 : 16, polygon.Bytes.Length));
        NativeBattleModulePoolTask? history = Tasks(prior).Values.FirstOrDefault(task => task.Callback == 0x597520);
        int age = history?.ReadWord(12) ?? 0;
        NativeBattleActionMatrix world = age > 6 ? Packed(SavedWorldAddress).ReadMatrix() : entering.GeometryWorldMatrix!;
        NativeBattleActionMatrix view = prior.NativeCamera!.CapturePackedView1D97778().ReadMatrix();
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, world);
        ImmutableArray<NativeBattleActionMatrix> local = Enumerable.Range(0, 28).Select(index => Packed(OriginalBonesAddress + (uint)(index * 32)).ReadMatrix()).ToImmutableArray();
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, basis, 0);
        int capacity = checked(cpu.ScratchMaximum!.Value * 8);
        uint scratchAddress = checked(0x196B018u - (uint)capacity);
        ImmutableArray<byte> retained = current.NativeFileStaging!.Read(scratchAddress, capacity).ToImmutableArray();
        NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones, prior.NativeCamera.Projection!, model.Color40!.Value, 2, retained, current.Definition.NativeGraphics!.Rules.SummonColors!, 4, new(0, 0, 320, 216), 17);
        Assert.Equal(17 * 8, draw.ProjectedBytesWritten);
        return (draw, geometry.PhysicalAddress, skeleton.PhysicalAddress, age, world, view);

        NativeBattlePackedMatrix32 Packed(uint address)
        {
            (byte[] bytes, bool[] known) = state.ReadView(address, 32).ReadSnapshot(0, 32);
            return new(bytes.ToImmutableArray(), known.ToImmutableArray());
        }

        byte[] Read(int offset, int length)
        {
            Assert.InRange(offset, 0, available);
            Assert.InRange(length, 0, available - offset);
            return NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, geometry with { Offset = checked(geometry.Offset + offset) }, length);
        }
    }

    private static bool MatchesHistory(NativeBattleLogicalCommand command, BattleSnapshot current, NativeBattleEffect324Resources resources, int caster, int age)
    {
        bool quad = command.Opcode is 0x2C or 0x2E;
        if (!quad && command.Opcode is not (0x24 or 0x26) || command.VertexPayload4.Length != 4 || command.OrderingBucket is null)
        {
            return false;
        }
        BattleCombatantDefinition prototype = current.Definition.Combatants.Single(actor => !actor.IsPlayer && actor.SceneSlot == 0);
        NativeBattleGraphicsResource resource = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == prototype.Id).Resource;
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new InvalidDataException("Effect324 history requires its actual original83 geometry.");
        Assert.Equal(3, geometry.Length);
        NativeBattleSummonColors colors = current.Definition.NativeGraphics.Rules.SummonColors!;
        foreach (NativeBattleRawPolygon polygon in geometry[1].Polygons.Where(polygon => polygon.Quad == quad))
        {
            ImmutableArray<byte> bytes = resource.GeometryRelocated507550 ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, current.NativeGraphics!.Models[caster].BodyTextureSlots!);
            uint uv0 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
            uint uv1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4));
            uint uv2 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(quad ? 16 : 6, 2));
            if (command.Words.Length != (quad ? 9 : 7) || command.Words[2] != uv0 || command.Words[4] != uv1 || (command.Words[6] & 0xFFFF) != uv2 || quad && (command.Words[8] & 0xFFFF) != BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(18, 2)))
            {
                continue;
            }
            foreach (uint palette in resources.MaterialColors24.Append(0u))
            {
                uint color = age >= 16 ? (colors.Interpolate(0, unchecked((short)((age - 16) * 256)), palette) & 0xFFFFFF) | 0x2000000u : palette;
                uint material = (color & 0x2FFFFFF) | (quad ? 0x2C000000u : 0x24000000u);
                if ((uv1 & 0xFE000000) != 0)
                {
                    material |= 0x2000000;
                }
                if (command.Words[0] == material)
                {
                    Assert.All(command.VertexPayload4.Take(quad ? 4 : 3), depth => Assert.InRange(depth, 0, 65535));
                    return true;
                }
            }
        }
        return false;
    }

    private static bool MatchesSprite(NativeBattleLogicalCommand command, NativeBattleModuleResource source, int flags)
    {
        if (command.Words.Length != 9 || command.Opcode is not (0x2C or 0x2E))
        {
            return false;
        }
        int frames = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(8, 2));
        for (int frame = 0; frame < frames; frame++)
        {
            int offset = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(10 + frame * 2, 2));
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(offset, 4)) & int.MaxValue;
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> quad = source.Bytes.AsSpan(offset + 4 + index * 20, 20);
                int inset = (flags & 8) != 0 ? 1 : 0;
                byte right = unchecked((byte)Math.Min(quad[4] + quad[0] - inset, 255));
                byte bottom = unchecked((byte)Math.Min(quad[5] + quad[2] - inset, 255));
                uint color = (uint)quad[3] << 24 | (uint)quad[1] * 0x010101;
                uint uv0 = BinaryPrimitives.ReadUInt32LittleEndian(quad.Slice(4, 4));
                uint uv1 = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14, 2)) & 0x1FF) << 16 | (uint)quad[5] << 8 | right;
                if (command.Words[0] == color && command.Words[2] == uv0 && command.Words[4] == uv1 && (command.Words[6] & 0xFFFF) == ((uint)bottom << 8 | quad[4]) && (command.Words[8] & 0xFFFF) == ((uint)bottom << 8 | right))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        File.WriteAllText(Path.Combine(output, "effect324-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), RecordTypes = recordTypes.Order(), Sprites = sprites.Order(), Camera = camera, CameraTrack = track, CameraViewChanged = view, VibrationStarted = vibrationStarted, VibrationFinished = vibrationFinished, OriginalBones = capturedBones, CurrentBones = currentBones, HistoryGeometry = historyGeometry, AllowRejectedHistory = allowRejectedHistory, WarmNativeRejectedHistory = warmNativeRejectedHistory, WarmRejectionProof = warmRejectionProof, ModelMask = modelMask, MainRetired = mainRetired, Contacts, ContactGlyphs, Observations = observations }));
        Assert.True(camera);
        Assert.True(track);
        Assert.True(view);
        Assert.True(vibrationStarted);
        Assert.True(vibrationFinished);
        Assert.True(capturedBones);
        Assert.True(currentBones);
        if (warmRejectionProof is not null)
        {
            File.WriteAllText(Path.Combine(output, "effect324-warm-history-rejection.json"), JsonSerializer.Serialize(warmRejectionProof));
        }
        Assert.True(historyGeometry || allowRejectedHistory && warmNativeRejectedHistory);
        Assert.True(modelMask);
        Assert.True(mainRetired);
        Assert.Equal(2, recordTypes.Count);
        Assert.Equal(3, sprites.Count);
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect324!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
    {
        Dictionary<uint, NativeBattleModulePoolTask> result = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return result;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            int? index = pool.Head;
            HashSet<int> visited = [];
            while (index is int slot)
            {
                Assert.True(visited.Add(slot));
                result.Add(pool.BankAddress + (uint)(slot * pool.Stride), pool.Slots[slot]);
                index = pool.Slots[slot].Next;
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
