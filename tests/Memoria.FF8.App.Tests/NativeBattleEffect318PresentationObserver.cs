using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect318PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> bones = [];
    private readonly HashSet<uint> programmes = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool cleared;
    private bool restored;
    private bool delayedZero;
    private bool scaledBodyPackets;
    private bool terminal;
    private bool morph512;
    private bool morph1032;
    private bool? cameraDecision;
    private bool disabledCamera;
    private bool customQuads;
    private int recordBirths;
    private int meshBirths;
    private int firstFrames;
    private int secondFrames;
    private int thirdFrames;
    private NativeCrtRandomState? randomBefore;
    private bool random380;
    private bool uploaded;
    private bool drained;
    private bool geometry;
    private bool mainRetired;
    public bool ScaledBodyPackets => scaledBodyPackets;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect318State state = current.NativeEffectMemory!.Effect318!;
        NativeBattleEffect318Resources resources = effect.Binding.Effect318Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            int modelOffset = BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4));
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(modelOffset, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool currentTrack = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[modelOffset..]));
            track |= currentTrack;
            if (currentTrack)
            {
                view |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            }
            if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
            {
                cameraDecision = decision;
                if (!decision)
                {
                    Assert.False(currentTrack);
                    if (current.NativeCamera.Variables[0] == -1 && current.NativeCamera.Scratch[0] == -1 && current.NativeCamera.ControlCursor is null && current.NativeCamera.Order.IsEmpty)
                    {
                        disabledCamera = true;
                        Mark("effect318-native-camera-declined-selector-and-terminated-control", current, cold);
                    }
                }
            }
            Mark("effect318-current-camera-control-and-bank", current, cold);
        }
        if (main is not null)
        {
            Assert.Equal(FirstProgrammeAddress, main.ReadDword(FirstWorkspaceOffset));
            Assert.Equal(SecondProgrammeAddress, main.ReadDword(SecondWorkspaceOffset));
            Assert.Equal(ThirdProgrammeAddress, main.ReadDword(ThirdWorkspaceOffset));
            programmes.UnionWith([FirstProgrammeAddress, SecondProgrammeAddress, ThirdProgrammeAddress]);
            Assert.Equal((uint)Math.Clamp(age - 2, 0, 22), main.ReadDword(FirstWorkspaceOffset + 4));
            Assert.Equal((uint)Math.Clamp(age - 4, 0, 16), main.ReadDword(SecondWorkspaceOffset + 4));
            Assert.Equal((uint)Math.Clamp(age - 19, 0, 32), main.ReadDword(ThirdWorkspaceOffset + 4));
            for (int index = 0; index < RecordCount; index++)
            {
                int life = main.ReadWord(RecordOffset + index * RecordStride + 6);
                Assert.InRange(life, 0, 10);
                int rowOffset = RecordOffset + index * RecordStride;
                if (life == 10)
                {
                    Assert.True(before is not null && before.ReadWord(rowOffset + 6) == 0);
                    recordBirths++;
                }
                if (before is not null && before.ReadWord(rowOffset + 6) > 0)
                {
                    Assert.Equal(before.ReadWord(rowOffset + 6) - 1, life);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = unchecked((sbyte)before.Payload[rowOffset + axis]);
                        Assert.Equal(unchecked((ushort)(before.ReadWord(rowOffset + axis * 2) + (velocity << 2))), main.ReadWord(rowOffset + axis * 2));
                    }
                }
            }
        }
        if (oldAge is >= 2 and < 24)
        {
            firstFrames++;
        }
        if (oldAge is >= 4 and < 20)
        {
            secondFrames++;
        }
        if (oldAge is >= 19 and < 51)
        {
            thirdFrames++;
        }
        meshBirths += tasks.Count(item => item.Value.Callback == 0x65F100 && !previous.ContainsKey(item.Key));
        if (oldAge == 4)
        {
            randomBefore = prior.NativeCrtRandom;
        }
        if (oldAge == 22 && randomBefore is not null)
        {
            NativeCrtRandomState expected = randomBefore;
            for (int index = 0; index < 380; index++)
            {
                expected = expected.Draw(out _);
            }
            Assert.Equal(expected, current.NativeCrtRandom);
            random380 = true;
            Mark("effect318-380-ordered-CRT-draws", current, cold);
        }
        if (oldAge == 4)
        {
            Assert.Equal(0u, graphics.Models[3].SequenceState!.MeshMask124 & 8);
            cleared = true;
        }
        if (oldAge == 39)
        {
            Assert.Equal(8u, graphics.Models[3].SequenceState!.MeshMask124 & 8);
            restored = true;
        }
        if (oldAge == 24)
        {
            Contacts++;
            Mark("effect318-contact-first-row-age24", current, cold);
        }
        if (oldAge >= 50 && main is null && before is not null)
        {
            mainRetired = true;
            Mark("effect318-main-retired-IO-and-bone-drain", current, cold);
        }
        int row = graphics.Models[3].ActiveResourceRow100 ?? throw new InvalidDataException("Effect318 requires its current source row.");
        uint skeleton = current.NativeModelCpuResources!.Rows[row].Require(12).PhysicalAddress;
        NativeBattleBodyBoneMatrices matrices = graphics.Models[3].CurrentBodyBoneMatrices!;
        Assert.Equal(36, matrices.Matrices.Length);
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(item => item.Value.Callback == 0x65F2B0))
        {
            int bone = NativeBattleEffect319BoneCache.ResolveIndex(skeleton, 36, task.ReadDword(12));
            Assert.Contains(bone, new[] { 22, 29, 33, 34, 35 });
            bones.Add(bone);
            Assert.Equal(1024, task.ReadWord(20));
            Assert.Equal(512, task.ReadWord(24));
            if (unchecked((short)task.ReadWord(16)) > 0)
            {
                Assert.Equal(0, task.ReadWord(18));
                Assert.Equal(0, task.ReadWord(22));
                delayedZero = true;
                Mark("effect318-positive-delay-applies-zero-local-R", current, cold);
            }
            if (previous.TryGetValue(address, out NativeBattleModulePoolTask? old) && old.Callback == task.Callback)
            {
                int delay = unchecked((short)(old.ReadWord(16) - 1));
                Assert.Equal(Math.Max(delay, 0), task.ReadWord(16));
                int z = delay <= 0 ? Math.Min((int)unchecked((short)(old.ReadWord(18) + old.ReadWord(20))), 4096) : old.ReadWord(18);
                int xy = delay <= 0 ? Math.Min((int)unchecked((short)(old.ReadWord(22) + old.ReadWord(24))), 4096) : old.ReadWord(22);
                Assert.Equal(z, task.ReadWord(18));
                Assert.Equal(xy, task.ReadWord(22));
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask old) in previous.Where(item => item.Value.Callback == 0x65F2B0))
        {
            if (!tasks.ContainsKey(address))
            {
                Assert.True(old.ReadWord(18) + old.ReadWord(20) >= 4096);
                Assert.True(old.ReadWord(22) + old.ReadWord(24) >= 4096);
                terminal = true;
                Mark("effect318-terminal-bone-scale-skips-final-write", current, cold);
            }
        }
        if (!scaledBodyPackets && tasks.Values.Any(task => task.Callback == 0x65F2B0))
        {
            scaledBodyPackets = VerifyCurrentScaledBody(prior, current, tasks, skeleton);
            if (scaledBodyPackets)
            {
                Mark("effect318-scaled-current-local-R-consumed-by-ordinary-body-before-pose-refresh", current, cold);
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
        if (arena != 0)
        {
            int offset = checked((int)(arena - 0x20DFAB8));
            var scratch = current.NativeEffectMemory.Arena!.ReadSnapshot(offset + MorphOffset, MorphLength);
            bool Known(int length) => Enumerable.Range(0, length / 8).All(index => scratch.Known.AsSpan(index * 8, 6).ToArray().All(value => value));
            morph512 |= oldAge is >= 2 and <= 23 && Known(512);
            morph1032 |= oldAge is >= 4 and <= 19 && Known(MorphLength);
            if (cursor >= arena + FirstPacketOffset && cursor <= arena + ThirdPacketOffset)
            {
                uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                if (CurrentCommands.Length > 0)
                {
                    banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                    customQuads |= CurrentCommands.Any(command => command.OrderingBucket == 19 && command.Words.Length == 9 && MatchesProjectedQuad(command, resources));
                    geometry |= CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length > 0);
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
            }
            if (mainRetired)
            {
                byte[] header = current.NativeEffectMemory.Arena.Read(offset + IoOffset, 5);
                if (header[4] == 1 && header[0] == 0 && header[1] == 0)
                {
                    byte[] file = resources.ReadFile(360).Data.ToArray();
                    Assert.Equal(file, current.NativeEffectMemory.Arena.Read(offset + ThirdPacketOffset, file.Length));
                    byte[] vram = current.NativeEffectMemory.Textures!.ReadVram();
                    VerifyTim(file, vram);
                    uploaded = true;
                    Mark("effect318-file360-real-type0-completion-and-VRAM", current, cold);
                }
            }
        }
        if (age is 2 or 4 or 8 or 19 or 20 or 23 or 24 or 25 or 30 or 31 or 39 or 40 or 41 or 50 or 51)
        {
            Mark("effect318-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Skeleton = skeleton, SkeletonIdentity = matrices.SkeletonSha256, Cursor = cursor, Commands = CurrentCommands.Length, Bones = bones.Order(), Contacts, ContactGlyphs });
    }

    private static bool VerifyCurrentScaledBody(BattleSnapshot prior, BattleSnapshot current, Dictionary<uint, NativeBattleModulePoolTask> tasks, uint skeleton)
    {
        NativeBattleGraphicsModel entering = prior.NativeGraphics!.Models[3];
        NativeBattleGraphicsResource resource = prior.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == prior.NativeGraphics.ActorIds[3]).Resource;
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Effect318 requires its original current rig.");
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new InvalidDataException("Effect318 requires its current original body geometry.");
        Assert.Equal(4, geometry.Length);
        NativeBattleGraphicsModel modified = entering;
        foreach (NativeBattleModulePoolTask task in tasks.Values.Where(task => task.Callback == 0x65F2B0))
        {
            int bone = NativeBattleEffect319BoneCache.ResolveIndex(skeleton, pose.BoneCount, task.ReadDword(12));
            modified = NativeBattleEffect319BoneCache.ScaleCurrent56BEF0(modified, bone, unchecked((short)task.ReadWord(22)), unchecked((short)task.ReadWord(18)));
        }
        NativeBattleCameraProjection projection = prior.NativeCamera!.Projection!;
        NativeBattleCameraPose camera = prior.NativeCamera.CurrentPose!;
        NativeBattleVector eye = new(camera.Eye.X, camera.Eye.Y, camera.Eye.Z);
        NativeBattleGraphicsModel scaled = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(modified, pose, projection.View!, eye);
        NativeBattleGraphicsModel baseline = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(entering, pose, projection.View!, eye);
        NativeBattleModelTextureBinding? material = resource.GeometryRelocated507550 ? null : entering.BodyTextureSlots;
        uint color = current.NativeGraphics!.Models[3].Color40!.Value;
        NativeBattleGeometryViewport viewport = new(short.MinValue / 8, short.MinValue / 8, ushort.MaxValue / 8, ushort.MaxValue / 8);
        ImmutableArray<NativeBattleGeometryPrimitive> expected = NativeBattleRawGeometryPackets.Emit5099D0(geometry, scaled.CurrentBodyBoneMatrices!.Matrices, projection, color, 1, null, 17, 4, material, viewport: viewport);
        ImmutableArray<NativeBattleGeometryPrimitive> unmodified = NativeBattleRawGeometryPackets.Emit5099D0(geometry, baseline.CurrentBodyBoneMatrices!.Matrices, projection, color, 1, null, 17, 4, material, viewport: viewport);
        NativeBattleLogicalCommand[] commands = current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank != "20DFAB8" && command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E).ToArray();
        return expected.Any(packet => !unmodified.Any(original => original.Bucket == packet.Bucket && original.Words.SequenceEqual(packet.Words) && original.VertexPayload4.SequenceEqual(packet.VertexPayload4)) && commands.Any(command => MatchesBodyPacket(command, packet)));
    }

    private static bool MatchesBodyPacket(NativeBattleLogicalCommand command, NativeBattleGeometryPrimitive packet)
    {
        if (command.OrderingBucket != packet.Bucket || command.Words.Length != packet.Words.Length - 1 || !command.VertexPayload4.SequenceEqual(packet.VertexPayload4))
        {
            return false;
        }
        for (int word = 0; word < command.Words.Length; word++)
        {
            uint mask = word is 6 or 8 ? 0xFFFFu : uint.MaxValue;
            if ((command.Words[word] & mask) != (packet.Words[word + 1] & mask))
            {
                return false;
            }
        }
        return true;
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

    public void Publish(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect318-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Bones = bones.Order(), Programmes = programmes.Order(), Camera = camera, CameraTrack = track, CameraViewChanged = view, CameraDecision = cameraDecision, DisabledCamera = disabledCamera, MaskCleared = cleared, MaskRestored = restored, PositiveDelayZero = delayedZero, ScaledBodyPackets = scaledBodyPackets, TerminalSkip = terminal, Morph512 = morph512, Morph1032 = morph1032, Uploaded = uploaded, Drained = drained, Geometry = geometry, CustomQuads = customQuads, MainRetired = mainRetired, ScheduledFrames = new[] { firstFrames, secondFrames, thirdFrames }, ScheduledNodeOutputs = firstFrames * 2 + secondFrames * 13 + thirdFrames * 9, RecordBirths = recordBirths, MeshBirths = meshBirths, Crt380 = random380, Contacts, ContactGlyphs, Observations = observations }));
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track);
            Assert.True(view);
        }
        else
        {
            Assert.True(disabledCamera && !track);
        }
        Assert.True(cleared);
        Assert.True(restored);
        Assert.True(delayedZero);
        Assert.True(scaledBodyPackets);
        Assert.True(terminal);
        Assert.True(morph512);
        Assert.True(morph1032);
        Assert.True(uploaded);
        Assert.True(drained);
        Assert.True(geometry);
        Assert.True(customQuads);
        Assert.True(mainRetired);
        Assert.True(random380);
        Assert.Equal(new[] { 22, 16, 32 }, new[] { firstFrames, secondFrames, thirdFrames });
        Assert.Equal(540, firstFrames * 2 + secondFrames * 13 + thirdFrames * 9);
        Assert.Equal(192, recordBirths);
        Assert.Equal(32, meshBirths);
        Assert.Equal(5, bones.Count);
        Assert.Equal(3, programmes.Count);
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect318!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static bool MatchesProjectedQuad(NativeBattleLogicalCommand command, NativeBattleEffect318Resources resources)
    {
        byte[] data = resources.ReadProjectedQuads(0xFF7B6C).Bytes.ToArray();
        for (int index = 1; index <= 11; index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8 + index * 2)) + 4;
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
