using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect345PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> geometryParities = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<byte> modelAnimations = [];
    private readonly HashSet<uint> relocated = [];
    private readonly List<object> observations = [];
    private bool copiedView;
    private bool decodedPose;
    private bool appearanceGeometry;
    private bool normalGeometry;
    private bool normalBounds;
    private bool material;
    private bool detached;
    private bool mutableGeometry;
    private bool cameraRegistered;
    private bool cameraTrack;
    private bool cameraChanged;
    private bool? cameraDecision;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect345State state = current.NativeEffectMemory!.Effect345!;
        NativeBattleEffect345Resources resources = effect.Binding.Effect345Resources!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> before = Tasks(prior);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        KeyValuePair<uint, NativeBattleModulePoolTask> root = tasks.FirstOrDefault(pair => pair.Value.Callback == RootCallback);
        bool scanned = root.Value is not null && before.TryGetValue(root.Key, out NativeBattleModulePoolTask? oldRoot) && root.Value.ReadWord(0x24) == unchecked((ushort)(oldRoot.ReadWord(0x24) + 1));
        if (effect.ReturnedDescriptor > 0)
        {
            Assert.Equal(Pools.Length, effect.Pools.Length);
            Assert.All(state.BankStorage, bank => Assert.Null(bank));
            for (int index = 0; index < Pools.Length; index++)
            {
                Assert.Equal(Pools[index].Descriptor, effect.Pools[index].DescriptorAddress);
                Assert.Equal(Pools[index].Bank, effect.Pools[index].BankAddress);
                Assert.Equal(Pools[index].Stride, effect.Pools[index].Stride);
                Assert.Equal(Pools[index].Capacity, effect.Pools[index].Slots.Length);
            }
            uint source = state.ReadDword(InitializerGlobal);
            Assert.NotEqual(0u, source);
            Assert.Equal(source, state.ReadDword(0x255DC0C));
            Assert.Equal(source + 0xE000u, state.ReadDword(0x255DC08));
            Assert.Equal(source + 0xE000u, state.ReadDword(0x255CD64));
            Assert.Equal(source + 0x1C000u, state.ReadDword(0x255CD60));
            if (root.Value is not null && root.Value.ReadByte(0x29) >= 3)
            {
                Assert.Equal(source + 0x1C000u, state.ReadDword(0x255EA44));
                Assert.Equal(state.ReadDword(0x255CD60), state.ReadDword(0x255EA44));
                Assert.Equal(source + 0x1C870u, state.ReadDword(0x255EA88));
            }
            if (scanned)
            {
                Assert.Equal(JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()), JsonSerializer.Serialize(current.NativeEffectMemory.SharedEffectView2793E58));
                Assert.Equal(0x2793E58u, state.ReadDword(0x2557208));
                Assert.Equal(0x2793E58u, state.ReadDword(ScratchCursorGlobal));
                copiedView = true;
                Mark("effect345-current-view-and-shared-scratch-restored", current, cold);
                int parity = (root.Value!.ReadWord(0x5C) - 1) & 1;
                uint first = source + (parity == 0 ? 0xE000u : 0);
                uint end = state.ReadDword(PrimaryCursorGlobal);
                Assert.True(end >= first);
                NativeBattleLogicalCommand[] commands = current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= (long)first - 0x20DFAB8 && command.Packet.ByteOffset < (long)end - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                foreach (NativeBattleLogicalCommand command in commands)
                {
                    opcodes.Add(command.Opcode);
                    Assert.NotNull(command.OrderingBucket);
                    if (command.VertexPayload4.Length > 0)
                    {
                        Assert.Equal(4, command.VertexPayload4.Length);
                        Assert.All(command.VertexPayload4, depth => Assert.InRange(depth, 0, 65535));
                    }
                }
                if (commands.Any(command => command.VertexPayload4.Length == 4))
                {
                    geometryParities.Add(parity);
                    Mark("effect345-linked-geometry-parity-" + parity, current, cold);
                }
                bool body = commands.Any(command => command.VertexPayload4.Length == 4 && (command.Opcode & 0xFC) is 0x24 or 0x2C);
                if (body && tasks.Values.Any(task => task.Callback == 0x706130 && (task.ReadByte(0x26) & 12) == 8))
                {
                    appearanceGeometry = true;
                    Mark("effect345-posed-appearance-ft3-ft4", current, cold);
                }
                if (body && tasks.Values.Any(task => task.Callback == 0x706130 && (task.ReadByte(0x26) & 12) == 0))
                {
                    normalGeometry = true;
                    Mark("effect345-posed-normal-ft3-ft4", current, cold);
                }
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(pair => pair.Value.Callback == 0x706130))
        {
            Assert.True(state.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model));
            Assert.NotNull(model!.DecodedPose);
            Assert.Equal(NativeBattleEffect345Resources.ModelAddress, model.ResourceAddress);
            Assert.Equal(28, model.DecodedPose!.Bones.Length);
            NativeBattleEffectOwnedModel.Validate(task, model, resources.Model);
            NativeBattleEffectArena skeleton = NativeBattleTemporaryMemory.ReadView(current.NativeFileStaging!, NativeBattleEffectOwnedModelRendering.SkeletonAddress(resources.Model), resources.Model.PoseSource.Skeleton.Length);
            NativeBattleDecodedPose staged = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, 28);
            Assert.Equal(JsonSerializer.Serialize(model.DecodedPose), JsonSerializer.Serialize(staged));
            modelAnimations.Add(task.ReadByte(0x9C));
            decodedPose = true;
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                NativeBattleActionMatrix expected = NativeBattleGfMatrices.Euler56CE30(new(unchecked((short)task.ReadWord(0x3C)), unchecked((short)task.ReadWord(0x3E)), unchecked((short)task.ReadWord(0x40))));
                expected = NativeBattlePoseMatrices.Scale56BEF0(expected, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = unchecked((short)task.ReadWord(0x4C)), Y = unchecked((short)task.ReadWord(0x4E)), Z = unchecked((short)task.ReadWord(0x50)) };
                NativeBattleActionMatrix actual = NativeBattleEffect345Controller.Packed(NativeBattleEffect345Controller.TaskView(task, 0x70, 32)).ReadMatrix();
                Assert.Equal(expected.Rotation, actual.Rotation);
                Assert.Equal((expected.X, expected.Y, expected.Z), (actual.X, actual.Y, actual.Z));
                if ((task.ReadByte(0x26) & 8) == 0)
                {
                    NativeBattleEffectLocalBounds bounds = NativeBattleEffectOwnedModelDrawing.ScaleShadowBounds7156EE(NativeBattlePoseMatrices.ShadowBounds5086F0(resources.Model.PoseSource, model.DecodedPose), unchecked((short)task.ReadWord(0x13E)));
                    Assert.Equal(new short[] { bounds.Minimum.X, bounds.Minimum.Y, bounds.Minimum.Z, bounds.Maximum.X, bounds.Maximum.Y, bounds.Maximum.Z }, Enumerable.Range(0, 6).Select(index => unchecked((short)task.ReadWord(0x64 + index * 2))).ToArray());
                    normalBounds = true;
                }
            }
            if (before.TryGetValue(address, out NativeBattleModulePoolTask? previous) && previous.Callback == 0x706130 && previous.ReadByte(0x29) is 2 or 3)
            {
                uint[] colors = new[] { prior.NativeStage!.ModelColors!.Color40, current.NativeStage!.ModelColors!.Color40 }.Distinct().ToArray();
                short fade = unchecked((short)task.ReadWord(0x13A));
                Assert.Contains(colors, color => Material(color, fade) == task.ReadDword(0x58));
                material = true;
            }
            Mark("effect345-owned-decoded-pose-current-staged-owner", current, cold);
        }
        foreach (NativeBattleEffect345NodeResourceBank bank in resources.Nodes)
        {
            if (state.NodeData is null || !state.NodeData.TryGetValue(bank.Address, out ImmutableArray<byte> data) || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) != 1)
            {
                continue;
            }
            for (int table = 0; table < 4; table++)
            {
                for (int index = 0; index < 16; index++)
                {
                    uint original = bank.Directory(table, index);
                    Assert.Equal(original == 0 ? 0u : bank.Address + original, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(4 + table * 64 + index * 4, 4)));
                    if (table == 0 && original != 0)
                    {
                        for (int field = 0xC8; field < 0x148; field += 4)
                        {
                            Assert.Equal(unchecked(bank.Address + BinaryPrimitives.ReadUInt32LittleEndian(bank.Data.AsSpan().Slice(checked((int)original + field), 4))), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(checked((int)original + field), 4)));
                        }
                    }
                }
            }
            if (relocated.Add(bank.Address))
            {
                Mark("effect345-current-relocation-" + bank.Address.ToString("X"), current, cold);
            }
            for (int index = 0; index < 16; index++)
            {
                uint mesh = bank.Directory(2, index);
                if (mesh == 0)
                {
                    continue;
                }
                int count = BinaryPrimitives.ReadInt32LittleEndian(bank.Data.AsSpan().Slice(checked((int)mesh + 4), 4));
                int bytes = checked(count * 8);
                if (bytes > 0 && !data.AsSpan().Slice(checked((int)mesh + 8), bytes).SequenceEqual(bank.Data.AsSpan().Slice(checked((int)mesh + 8), bytes)))
                {
                    mutableGeometry = true;
                    Mark("effect345-current-mutable-morph-vertices", current, cold);
                }
            }
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            cameraRegistered = true;
            if (effect.ReturnedDescriptor > 0 && current.NativeGraphics.ActionGraphics?.UseActionCamera is bool decision)
            {
                cameraDecision ??= decision;
                Assert.Equal(cameraDecision, decision);
            }
            bool track = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[324..]));
            cameraTrack |= track;
            cameraChanged |= track && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
        }
        if (prior.NativeGraphics?.OriginalEffect?.ReturnedDescriptor > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.BankStorage, bank => Assert.NotNull(bank));
            detached = true;
            Mark("effect345-eight-physical-banks-detached", current, cold);
        }
        (byte[] temporary, bool[] known) = current.NativeFileStaging!.ReadSnapshot(NativeBattleTemporaryMemory.Address + 0x44, 4);
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, RootScanned = scanned, RootParityAfter = root.Value?.ReadWord(0x5C), Primary = state.ReadDword(PrimaryCursorGlobal), Secondary = state.ReadDword(SecondaryCursorGlobal), Models = state.OwnedModels.Count, Appearance = appearanceGeometry, Normal = normalGeometry, Relocated = relocated.Order().ToArray(), MutableGeometry = mutableGeometry, RetainedTemporary44 = temporary, RetainedTemporary44Known = known, TemporaryWitness = "Post-callback physical bytes; later bone and particle helpers may have overwritten the earlier quad input" });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        File.WriteAllText(Path.Combine(output, "effect345-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), GeometryParities = geometryParities.Order(), Opcodes = opcodes.Order(), ModelAnimations = modelAnimations.Order(), Relocated = relocated.Order(), CopiedView = copiedView, DecodedPose = decodedPose, AppearanceGeometry = appearanceGeometry, NormalGeometry = normalGeometry, NormalBounds = normalBounds, Material = material, Detached = detached, MutableGeometry = mutableGeometry, CameraRegistered = cameraRegistered, CameraDecision = cameraDecision, CameraTrack = cameraTrack, CameraViewChanged = cameraChanged, Observations = observations }));
        Assert.True(copiedView);
        Assert.True(decodedPose);
        Assert.True(appearanceGeometry);
        Assert.True(normalGeometry);
        Assert.True(normalBounds);
        Assert.True(material);
        Assert.True(detached);
        Assert.Equal(new[] { 0, 1 }, geometryParities.Order());
        Assert.Equal(new byte[] { 5, 6 }, modelAnimations.Order());
        Assert.Equal(new uint[] { 0x1643698, 0x164808C }, relocated.Order());
        Assert.True(cameraRegistered);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(cameraTrack);
            Assert.True(cameraChanged);
        }
        else
        {
            Assert.False(cameraTrack);
        }
        Assert.True(NativeBattleEffect345PresentationTests.Released(completed));
    }

    private static uint Material(uint color, short fade)
    {
        if (fade == 0)
        {
            return color & 0xFFFFFF;
        }
        uint result = 0x02000000;
        for (int lane = 0; lane < 3; lane++)
        {
            int channel = (byte)(color >> (lane * 8)) >> 1;
            result |= (uint)unchecked((byte)(channel - channel * fade / 4096)) << (lane * 8);
        }
        return result;
    }

    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots.Select((task, index) => (Address: checked(pool.BankAddress + (uint)(index * pool.Stride)), Task: task))).Where(pair => pair.Task.Callback != 0).ToDictionary(pair => pair.Address, pair => pair.Task) ?? [];

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
