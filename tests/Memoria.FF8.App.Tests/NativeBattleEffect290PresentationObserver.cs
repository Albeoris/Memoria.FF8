using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect290PresentationObserver(bool warm = false)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<int> mainAges = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> programmes = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool cameraTrack;
    private bool cameraView;
    private bool? cameraDecision;
    private bool sourceBound;
    private bool exhaustedCounts;
    private bool currentBones;
    private bool internalRandom;
    private bool processRandom;
    private bool crtRandom;
    private bool morph;
    private bool fragments;
    private bool smoke;
    private bool customQuads;
    private bool geometry;
    private bool mainRetired;
    private bool uploaded;
    private bool drained;
    private bool vibrationStarted;
    private bool vibrationStopped;
    private int? vibrationHandle;
    public int Contacts => 0;
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentMeshCommands => CurrentCommands.Where(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length == 4).ToArray();
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect290State state = current.NativeEffectMemory!.Effect290!;
        NativeBattleEffect290Resources resources = effect.Binding.Effect290Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        int processed = before is not null ? oldAge : main is not null ? age - 1 : -1;
        callbacks.UnionWith(tasks.Values.Select(task => task.Callback));
        Assert.Equal((byte)0, current.NativeActionPackets!.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(16));
        Assert.Equal((byte)125, graphics.Models[3].ModelId4);
        Assert.Equal(28, graphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
        int row = graphics.Models[3].ActiveResourceRow100 ?? throw new InvalidDataException("Effect290 requires its current physical resource row.");
        Assert.NotEqual(0u, current.NativeModelCpuResources!.Rows[row].Require(12).PhysicalAddress);
        currentBones = true;
        if (graphics.ActionGraphics is { UseActionCamera: not null } action)
        {
            cameraDecision = action.UseActionCamera;
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            cameraTrack |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            cameraView |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect290-current-camera8-24", current, cold);
        }
        if (state.ScriptSourcePending == false)
        {
            short[] counts = NativeBattleEffect290Resources.ScriptCountAddresses.Select(address => resources.ReadScriptWord(current.NativeFileStaging!, address)).ToArray();
            sourceBound = true;
            if (warm)
            {
                Assert.All(counts, count => Assert.Equal((short)0, count));
            }
            if (counts.All(count => count == 0))
            {
                exhaustedCounts = true;
                Mark("effect290-all23-current-script-counts-exhausted", current, cold);
            }
        }
        if (processed is >= 0 and <= 89)
        {
            Assert.True(mainAges.Add(processed));
            Assert.Equal(processed >= 30 ? 12 : 4, graphics.Models[3].Flags0 & 12);
        }
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        internalRandom |= prior.NativeEffectMemory?.Effect290 is { } oldState && oldState.ReadDword(InternalRandomGlobal) != state.ReadDword(InternalRandomGlobal);
        processRandom |= prior.NativeEffectMemory?.SharedEffectRandom?.State != current.NativeEffectMemory.SharedEffectRandom?.State;
        crtRandom |= prior.NativeCrtRandom?.Seed != current.NativeCrtRandom?.Seed;
        if (main is not null)
        {
            int handle = unchecked((int)main.ReadDword(16));
            vibrationHandle ??= handle;
            Assert.Equal(vibrationHandle.Value, handle);
            if (!vibrationStarted)
            {
                NativeVibrationSlot slot = current.NativeVibrationProcess!.Slots[handle & 3];
                Assert.Equal(handle, slot.Generation * 16 | slot.Id);
                Assert.True(current.NativeVibrationProcess.IsPlaying(handle));
                vibrationStarted = true;
                Mark("effect290-actual-returned-vibration-handle", current, cold);
            }
            foreach ((int offset, uint source) in new (int, uint)[] { (0x64E0, FirstProgrammeAddress), (0x65F0, SecondProgrammeAddress), (0x681C, ThirdProgrammeAddress) })
            {
                Assert.Equal(source, main.ReadDword(offset));
                programmes.Add(source);
            }
            fragments |= Enumerable.Range(0, 192).Any(index => main.ReadWord(0x1D8 + index * 0x7C + 6) != 0);
            smoke |= Enumerable.Range(0, 128).Any(index => main.ReadWord(0x5ED8 + index * 12 + 6) != 0);
        }
        if (processed == 86)
        {
            Assert.NotNull(vibrationHandle);
            Assert.False(current.NativeVibrationProcess!.IsPlaying(vibrationHandle!.Value));
            vibrationStopped = true;
            Mark("effect290-vibration-stopped-at86", current, cold);
        }
        if (before is not null && main is null)
        {
            Assert.Equal(90, oldAge);
            mainRetired = true;
            Mark("effect290-main-retired-real-IO-drain", current, cold);
        }
        if (arena != 0)
        {
            int offset = checked((int)(arena - 0x20DFAB8));
            if (processed is >= 20 and <= 41)
            {
                (byte[] _, bool[] known) = current.NativeEffectMemory.Arena!.ReadSnapshot(offset + MorphOffset, MorphLength);
                if (Enumerable.Range(0, MorphLength / 8).All(index => known.AsSpan(index * 8, 6).ToArray().All(value => value)))
                {
                    morph = true;
                    Mark("effect290-morph640-XYZ-only-knownness", current, cold);
                }
            }
            uint first = arena + FirstPacketOffset;
            uint second = arena + SecondPacketOffset;
            uint start = cursor >= second ? second : first;
            if (cursor >= start && cursor <= start + 0xC000)
            {
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                if (CurrentCommands.Length > 0)
                {
                    packetBanks.Add(start == first ? 0 : 1);
                    geometry |= CurrentMeshCommands.Length > 0;
                    customQuads |= CurrentCommands.Any(command => MatchesSmoke(command, resources));
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
            }
            if (mainRetired)
            {
                byte[] header = current.NativeEffectMemory.Arena!.Read(offset + IoOffset, 5);
                if (header[4] == 1 && header[0] == 0 && header[1] == 0)
                {
                    byte[] file = resources.ReadFile(360).Data.ToArray();
                    Assert.Equal(file, current.NativeEffectMemory.Arena.Read(offset + File360Offset, file.Length));
                    VerifyTim(file, current.NativeEffectMemory.Textures!.ReadVram());
                    uploaded = true;
                    Mark("effect290-file360-type0-complete-current-VRAM", current, cold);
                }
            }
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        if (age is 1 or 16 or 20 or 32 or 40 or 42 or 48 or 61 or 66 or 80 or 86 or 87 or 90)
        {
            Mark("effect290-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, ProcessedAge = processed, Cursor = cursor, MainVisits = mainAges.Count, Commands = CurrentCommands.Length, InternalRandom = state.ReadDword(InternalRandomGlobal), ProcessRandom = current.NativeEffectMemory.SharedEffectRandom?.State, CrtRandom = current.NativeCrtRandom?.Seed, SourceBound = sourceBound, ExhaustedCounts = exhaustedCounts, Fragments = fragments, Smoke = smoke, ModelFlags = graphics.Models[3].Flags0, CurrentSkeleton = graphics.Models[3].CurrentBodyBoneMatrices!.SkeletonSha256 });
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect290-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = packetBanks.Order(), Callbacks = callbacks.Order(), Programmes = programmes.Order(), Warm = warm, Camera = camera, CameraTrack = cameraTrack, CameraView = cameraView, CameraDecision = cameraDecision, CurrentBones = currentBones, MainVisits = mainAges.Count, SourceBound = sourceBound, ExhaustedCounts = exhaustedCounts, InternalRandom = internalRandom, ProcessRandom = processRandom, CrtRandom = crtRandom, Morph640 = morph, Fragments = fragments, Smoke = smoke, CustomQuads = customQuads, Geometry = geometry, VibrationStarted = vibrationStarted, VibrationStopped = vibrationStopped, VibrationHandle = vibrationHandle, Uploaded = uploaded, MainRetired = mainRetired, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(cameraTrack && cameraView);
        }
        Assert.Equal(90, mainAges.Count);
        Assert.True(currentBones && sourceBound && exhaustedCounts);
        Assert.True(internalRandom && processRandom && crtRandom);
        Assert.True(morph && fragments && smoke && customQuads && geometry);
        Assert.True(vibrationStarted && vibrationStopped && mainRetired && uploaded && drained);
        Assert.Equal(3, programmes.Count);
        Assert.Equal(2, packetBanks.Count);
        Assert.Equal(0, ContactGlyphs);
        Assert.Equal(4, completed.NativeGraphics!.Models[3].Flags0 & 12);
        Assert.False(completed.NativeEffectMemory!.Effect290!.ScriptSourcePending);
        Assert.All(completed.NativeEffectMemory.Effect290.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static bool MatchesSmoke(NativeBattleLogicalCommand command, NativeBattleEffect290Resources resources)
    {
        if (command.OrderingBucket != 19 || command.Words.Length != 9 || command.Opcode is not (0x2C or 0x2E))
        {
            return false;
        }
        byte[] data = resources.ReadProjectedQuads(SmokeSourceAddress).Bytes.ToArray();
        for (int life = 12; life > 0; life--)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2 * (17 - life))) + 4;
            uint color = (uint)data[offset + 3] << 24 | (uint)data[offset + 1] * 0x010101;
            uint uv0 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
            uint page = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 14)) & 0x1FF);
            byte right = unchecked((byte)Math.Min(data[offset + 4] + data[offset], 255));
            byte bottom = unchecked((byte)Math.Min(data[offset + 5] + data[offset + 2], 255));
            if (command.Words[0] == color && command.Words[2] == uv0 && command.Words[4] == (uint)(right | data[offset + 5] << 8) + (page << 16) && (command.Words[6] & 0xFFFF) == (uint)(data[offset + 4] | bottom << 8) && (command.Words[8] & 0xFFFF) == (uint)(right | bottom << 8))
            {
                return command.VertexPayload4.Length == 4 && command.VertexPayload4.All(value => value is >= 0 and <= 65535);
            }
        }
        return false;
    }

    private static void VerifyTim(byte[] data, byte[] vram)
    {
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 4, 2));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 6, 2));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8, 2));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10, 2));
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.AsSpan(offset + 12 + row * width * 2, width * 2).ToArray(), vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2).ToArray());
            }
            offset += size;
        }
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
            HashSet<int> visited = [];
            int? index = pool.Head;
            while (index is int slot)
            {
                Assert.True(visited.Add(slot));
                NativeBattleModulePoolTask task = pool.Slots[slot];
                result.Add(pool.BankAddress + (uint)(slot * pool.Stride), task);
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
