using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect287PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool? cameraDecision;
    private bool morph;
    private bool currentBones;
    private bool processRandom;
    private bool capturedTargets;
    private bool meshQueueDetached;
    private bool meshFileAlias;
    private bool uploaded;
    private bool mainRetired;
    private bool drained;
    private bool geometry;
    private bool ribbon;
    private bool stage;
    private int mainVisits;
    private int targetVisits;
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
        NativeBattleEffect287State state = current.NativeEffectMemory!.Effect287!;
        NativeBattleEffect287Resources resources = effect.Binding.Effect287Resources!;
        Assert.Equal(3, effect.Parameters!.Actor78);
        Assert.Equal((byte)125, graphics.Models[3].ModelId4);
        NativeBattleBodyBoneMatrices matrices = graphics.Models[3].CurrentBodyBoneMatrices!;
        Assert.Equal(28, matrices.Matrices.Length);
        int row = graphics.Models[3].ActiveResourceRow100 ?? throw new InvalidDataException("Effect287 lost its actual current CPU row.");
        Assert.NotEqual(0u, current.NativeModelCpuResources!.Rows[row].Require(12).PhysicalAddress);
        currentBones = true;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        int processed = before is not null ? oldAge : main is not null ? age - 1 : -1;
        if (graphics.ActionGraphics is { UseActionCamera: not null } action)
        {
            cameraDecision = action.UseActionCamera;
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(32, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            track |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[32..]));
            view |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect287-current-camera8-32", current, cold);
        }
        if (processed is >= 0 and <= 74)
        {
            mainVisits++;
            if (processed == 65)
            {
                Contacts++;
                Mark("effect287-current-header-all-contacts65", current, cold);
            }
        }
        if (main is not null)
        {
            Assert.Equal(1, (int)main.ReadWord(14));
            Assert.Equal(0x1D972C0u, main.ReadDword(0x20));
            Assert.Equal(FirstProgrammeAddress, main.ReadDword(0x2C));
            Assert.Equal(SecondProgrammeAddress, main.ReadDword(0x8F0));
            capturedTargets = true;
            Assert.Equal(age <= 30 ? 8 : 0, graphics.Models[3].Flags0 & 8);
        }
        targetVisits += tasks.Values.Count(task => task.Callback == 0x6F3D00) + previous.Count(item => item.Value.Callback == 0x6F3D00 && !tasks.ContainsKey(item.Key));
        if (processed == 30)
        {
            Assert.DoesNotContain(effect.Pools, pool => pool.DescriptorAddress == MeshDescriptor);
            Assert.NotNull(state.MeshQueueStorage);
            Assert.Equal(new byte[8], state.Read(MeshQueueAddress, 8));
            meshQueueDetached = true;
            Mark("effect287-old30-detached-mesh-head-tail-only", current, cold);
        }
        processRandom |= prior.NativeEffectMemory?.SharedEffectRandom?.State != current.NativeEffectMemory.SharedEffectRandom?.State;
        if (state.ReadDword(IntensityGlobal) != 0)
        {
            stage = true;
        }
        if (before is not null && main is null)
        {
            Assert.Equal(74, oldAge);
            mainRetired = true;
            Mark("effect287-main75-retired-real-IO-drain", current, cold);
        }
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena != 0)
        {
            int offset = checked((int)(arena - 0x20DFAB8));
            if (processed is >= 0 and <= 74)
            {
                var scratch = current.NativeEffectMemory.Arena!.ReadSnapshot(offset + MorphOffset, MorphLength);
                morph |= scratch.Known.Take(6).All(value => value) && scratch.Known.Skip(MorphLength - 8).Take(6).All(value => value);
            }
            uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
            if (cursor >= start && cursor <= start + 0x10000)
            {
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                if (CurrentCommands.Length > 0)
                {
                    banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                    geometry |= CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length == 4);
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
            }
            if (processed >= 55 || mainRetired)
            {
                byte[] header = current.NativeEffectMemory.Arena!.Read(offset + IoOffset, 5);
                if (header[4] == 1 && header[0] == 0 && header[1] == 0)
                {
                    byte[] file = resources.ReadFile(360).Data.ToArray();
                    Assert.Equal(file, current.NativeEffectMemory.Arena.Read(offset + File360Offset, file.Length));
                    VerifyTim(file, current.NativeEffectMemory.Textures!.ReadVram());
                    meshFileAlias = MeshDescriptor != 0 && CommonPools.Single(pool => pool.Descriptor == MeshDescriptor).Bank == File360Offset + 4;
                    uploaded = true;
                    Mark("effect287-real-type0-file360-mesh-alias-upload", current, cold);
                }
            }
        }
        NativeBattleLogicalCommand[] ribbons = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "off_B6D070" && command.Opcode == 0x3E && command.Words.Length == 12 && command.VertexPayload4.Length == 4 && command.OrderingBucket >= 17 && command.Words[2] == 0x3E14C048 && command.Words[5] == 0x00B9C078 && (command.Words[8] & 0xFFFF) == 0xC048 && (command.Words[11] & 0xFFFF) == 0xC078).ToArray();
        if (ribbons.Length > 0)
        {
            Assert.All(ribbons.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
            CurrentCommands = CurrentCommands.Concat(ribbons).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            ribbon = true;
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        if (age is 1 or 8 or 29 or 30 or 31 or 33 or 36 or 38 or 40 or 44 or 48 or 55 or 56 or 59 or 65 or 66 or 74)
        {
            Mark("effect287-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            Assert.NotNull(state.MeshQueueStorage);
            Assert.Equal(0, graphics.Models[3].Flags0 & 8);
            drained = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, ProcessedAge = processed, Cursor = cursor, Commands = CurrentCommands.Length, MainVisits = mainVisits, TargetVisits = targetVisits, Intensity = state.ReadDword(IntensityGlobal), Random = current.NativeEffectMemory.SharedEffectRandom?.State, MeshQueueRetained = state.MeshQueueStorage is not null, Contacts, ContactGlyphs });
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

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect287-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Camera = camera, CameraTrack = track, CameraView = view, CameraDecision = cameraDecision, CurrentBones = currentBones, Process7059E0 = processRandom, CapturedTargets = capturedTargets, MainVisits = mainVisits, TargetVisits = targetVisits, ScheduledSourceNodeOutputs = mainVisits * 29 + targetVisits * 5, Morph1408 = morph, MeshQueueDetached = meshQueueDetached, MeshFileAlias = meshFileAlias, Uploaded = uploaded, MainRetired = mainRetired, Geometry = geometry, NativeRibbon = ribbon, Stage = stage, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track && view);
        }
        Assert.True(currentBones && processRandom && capturedTargets && morph);
        Assert.True(meshQueueDetached && meshFileAlias && uploaded && mainRetired && drained);
        Assert.True(geometry && ribbon && stage);
        Assert.Equal(75, mainVisits);
        Assert.Equal(18, targetVisits);
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect287!.FixedBanks.Values, bank => Assert.NotNull(bank));
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
            while (index is int slot)
            {
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
