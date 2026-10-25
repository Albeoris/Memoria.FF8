using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect257PresentationObserver(BattleSnapshot source)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<string> packetBanks = [];
    private readonly HashSet<int> renderKinds = [];
    private readonly HashSet<byte> primitiveOpcodes = [];
    private readonly HashSet<int> particleCounts = [];
    private readonly HashSet<int> particlePlacements = [];
    private readonly HashSet<int> programmeCapacities = [];
    private readonly HashSet<short> cameraChoices = [];
    private readonly HashSet<int> textureClutIndices = [];
    private readonly HashSet<int> texturePixelIndices = [];
    private readonly List<object> uploads = [];
    private readonly List<object> observations = [];
    private readonly HashSet<ushort> rawFiles = [];
    private readonly HashSet<ushort> completedRawFiles = [];
    private readonly HashSet<ushort> rawUploads = [];
    private readonly Dictionary<int, ushort> pendingIo = [];
    private readonly Dictionary<ushort, uint> rawDestinations = [];
    private readonly List<object> ioObservations = [];
    private int ioStarts;
    private int ioCompletions;
    private bool privateSetupObserved;
    private bool initialized;
    private bool drained;
    private bool cameraRegistered;
    private bool cameraPlaying;
    private bool skippedByRetainedSelector;
    private bool cameraSkipVerified;
    private short? cameraSelectorAtRegistration;
    private string cameraScope = "not yet registered";
    private bool cameraViewChanged;
    private bool textureQueued;
    private bool vramChanged;
    private bool vramUploadVerified;
    private bool randomAdvanced;
    private bool geometry;
    private bool arenaGeometry;
    private bool commonGeometry;
    private int hpTransitions;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(current.Random));
        ushort beforeHp = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
        ushort afterHp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
        if (beforeHp != afterHp)
        {
            Assert.Equal(Math.Max(0, beforeHp - 7), afterHp);
            hpTransitions++;
            Mark("contact-seven-presentation", current, cold);
        }
        for (int physical = 0; physical < 7; physical++)
        {
            NativeBattleGraphicsModel initial = source.NativeGraphics!.Models[physical];
            NativeBattleGraphicsModel model = current.NativeGraphics!.Models[physical];
            Assert.Equal(initial.ModelId4, model.ModelId4);
            Assert.Equal(initial.HasLinkedModelRing140, model.HasLinkedModelRing140);
            Assert.Equal(initial.LinkedModelIndex140, model.LinkedModelIndex140);
            if (physical is not (0 or 3))
            {
                Assert.Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(model));
            }
            if (physical is 0 or 3)
            {
                Assert.Equal(initial.CurrentBodyBoneMatrices!.Matrices.Length, model.CurrentBodyBoneMatrices!.Matrices.Length);
                Assert.Equal(initial.CurrentLinkedBoneMatrices?.Matrices.Length, model.CurrentLinkedBoneMatrices?.Matrices.Length);
                model.GeometryWorldMatrix!.Validate();
            }
        }
        NativeBattleOriginalEffectState? effect = current.NativeGraphics?.OriginalEffect;
        if (effect?.Binding.Effect != 257 || effect.ReturnedDescriptor is null || !initialized && effect.ReturnedDescriptor == 0)
        {
            return;
        }
        bool live = effect.ReturnedDescriptor > 0;
        int? entryCommonAvailable = null;
        int? entryCommonCursor = null;
        uint? priorArenaBase = null;
        uint? priorArenaCursor = null;
        uint? priorArenaHalf = null;
        uint? priorRootProgrammePc = null;
        uint? priorFile0 = null;
        object? commonEntry = null;
        if (live)
        {
            RequireNoEarlierPacketProducer(prior);
            NativeBattlePrimitiveProcess beforeProcess = Assert.IsType<NativeBattlePrimitiveProcess>(prior.NativeEffectMemory!.PrimitiveProcess);
            NativeBattlePrimitiveProcess afterProcess = Assert.IsType<NativeBattlePrimitiveProcess>(current.NativeEffectMemory!.PrimitiveProcess);
            Assert.NotNull(beforeProcess.ByteCursor);
            Assert.True(beforeProcess.ByteCursor >= 0);
            Assert.Equal(beforeProcess.PacketBank, afterProcess.PacketBank);
            Assert.Equal(beforeProcess.PacketBuffer, afterProcess.PacketBuffer);
            entryCommonCursor = beforeProcess.ByteCursor!.Value;
            entryCommonAvailable = beforeProcess.CommonPacketWriteCapacity - entryCommonCursor.Value;
            Assert.True(entryCommonAvailable >= 2448, "The actual callback-entry COMMON owner does not meet the conditional packet bound.");
            if (prior.NativeEffectMemory.ScriptedEffects is { } entryCore)
            {
                uint entryManager = Dword(entryCore, 0x27973EC);
                if (entryManager != 0)
                {
                    priorArenaBase = Dword(entryCore, entryManager + 0xD4);
                    priorArenaCursor = Dword(entryCore, entryManager + 0xD8);
                    priorArenaHalf = Dword(entryCore, entryManager + 0xDC);
                    uint entryBank = Dword(entryCore, entryManager + 0x90);
                    if (entryBank >= 0x20DFAB8 && prior.NativeGraphics?.OriginalEffect is { Binding.Effect: 257, ReturnedDescriptor: > 0 } priorEffect)
                    {
                        priorRootProgrammePc = BinaryPrimitives.ReadUInt32LittleEndian(prior.NativeEffectMemory.Arena!.Read(checked((int)(entryBank - 0x20DFAB8)), 4));
                        priorFile0 = checked(0x20DFAB8u + (uint)Assert.Single(priorEffect.ResourcePointers, pointer => pointer.GlobalAddress == 0x279650C).ArenaOffset);
                    }
                }
            }
            NativeBattleGraphicsState entryGraphics = prior.NativeGraphics!;
            commonEntry = new
            {
                PriorFrame = prior.Frame,
                ArenaBase = priorArenaBase,
                ArenaCursor = priorArenaCursor,
                ArenaHalf = priorArenaHalf,
                RootProgrammePc = priorRootProgrammePc,
                File0 = priorFile0,
                Cursor = beforeProcess.ByteCursor,
                Bank = beforeProcess.PacketBank,
                Buffer = beforeProcess.PacketBuffer,
                WriteCapacity = beforeProcess.CommonPacketWriteCapacity,
                LiveModuleFlags = entryGraphics.ModuleEffects.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Flags).ToArray(),
                LiveAuxKinds = entryGraphics.Auxiliary.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Kind).ToArray(),
                QueuedDescriptorCommands = entryGraphics.Descriptors.Queue.Select(index => entryGraphics.Descriptors.Packets[index].Command).ToArray(),
                ActiveDescriptorTaskKinds = entryGraphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0).Select(task => task.Kind).ToArray()
            };
            Assert.Equal(257, current.NativeEffectMemory!.Cinematic!.Effect);
            Assert.Equal((byte)0, current.NativeEffectMemory.Cinematic.Filter145);
            int header = effect.Parameters!.HeaderIndex7C;
            Assert.Equal((byte)1, current.NativeActionPackets!.Headers[header].ReadByte(0x10));
            Assert.Equal((byte)0, current.NativeActionPackets.Headers[header].ReadByte(0x11));
            Assert.Equal(44, current.NativeGraphics!.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.False(current.NativeGraphics.Models[3].HasLinkedModelRing140);
            Assert.Equal(23, current.NativeGraphics.Models[0].CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.Equal(2, current.NativeGraphics.Models[0].CurrentLinkedBoneMatrices!.Matrices.Length);
            if (!initialized)
            {
                var initializerAssets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
                Assert.Equal(26092, initializerAssets[0].Data.Length);
                Assert.Equal(227440, initializerAssets[1].Data.Length);
                NativeBattleCinematicResources resources = effect.Binding.CinematicResources!;
                Assert.Equal(8, resources.TextureProfile.Count);
                Assert.All(Enumerable.Range(0, resources.TextureProfile.Count), texture =>
                {
                    Assert.Equal((byte)1, resources.Source(texture, false));
                    Assert.Equal((byte)1, resources.Source(texture, true));
                });
                initialized = true;
                Mark("initialized-header11-zero", current, cold);
            }
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            Assert.Equal(0x2796510u, pool.DescriptorAddress);
            Assert.Equal(0x2796520u, pool.BankAddress);
            Assert.Equal((ushort)0x10, pool.Stride);
            Assert.Single(pool.Slots);
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(0x96F900u, task.Callback);
                callbacks.Add(task.Callback);
            }
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        if (live)
        {
            Assert.InRange(Dword(core, Dword(core, 0x27973EC) + 0x7C), checked((uint)entryCommonCursor!.Value), checked((uint)entryCommonCursor.Value + 2404));
        }
        NativeBattleFileIoState io = current.NativeFileIo!;
        ObserveIo(prior, current, effect, core, io, cold);
        randomAdvanced |= current.NativeCrtRandom != source.NativeCrtRandom;
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        textureQueued |= textures.Count > (prior.NativeEffectMemory!.Textures?.Count ?? 0);
        int file1Offset = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796508).ArenaOffset;
        uint managerAddress = Dword(core, 0x27973EC);
        int staticTextureReservations = core.Read(managerAddress + 0xD2, 1)[0];
        Assert.InRange(staticTextureReservations, 0, textures.Slots.Length);
        HashSet<int> submittedSlots = Enumerable.Range(0, staticTextureReservations).ToHashSet();
        for (int slot = 0; slot < textures.Slots.Length; slot++)
        {
            NativeBattleEffectTimUpload? upload = textures.Slots[slot];
            if (upload != prior.NativeEffectMemory!.Textures!.Slots[slot] && upload is { Kind: 0, Rectangle: { X: 704 or 768 or 832, Y: 256, Width: 64, Height: 256 }, ArenaOffset: int offset })
            {
                ushort fileId = checked((ushort)(363 + (upload.Rectangle.X - 704) / 64));
                if (completedRawFiles.Contains(fileId) && rawDestinations.TryGetValue(fileId, out uint destination) && destination == checked(0x20DFAB8u + (uint)offset))
                {
                    submittedSlots.Add(slot);
                }
            }
        }
        if (live && submittedSlots.Count > 0)
        {
            Assert.Equal(0, textures.Count);
            byte[] currentVram = textures.ReadVram();
            foreach (int uploadIndex in submittedSlots.Order())
            {
                NativeBattleEffectTimUpload upload = Assert.IsType<NativeBattleEffectTimUpload>(textures.Slots[uploadIndex]);
                Assert.Equal(0, upload.Kind);
                NativeBattleEffectTextureRectangle rectangle = Assert.IsType<NativeBattleEffectTextureRectangle>(upload.Rectangle);
                int offset = Assert.IsType<int>(upload.ArenaOffset);
                Assert.InRange(offset, file1Offset, file1Offset + 227440 - 2 * rectangle.Width * rectangle.Height);
                byte[] expected = current.NativeEffectMemory.Arena!.Read(offset, 2 * rectangle.Width * rectangle.Height);
                for (int row = 0; row < rectangle.Height; row++)
                {
                    Assert.True(currentVram.AsSpan(2 * ((rectangle.Y + row) * 1024 + rectangle.X), 2 * rectangle.Width).SequenceEqual(expected.AsSpan(2 * row * rectangle.Width, 2 * rectangle.Width)));
                }
                NativeBattleCinematicResources resources = effect.Binding.CinematicResources!;
                List<int> pixels = [];
                List<int> cluts = [];
                for (int texture = 0; texture < resources.TextureProfile.Count; texture++)
                {
                    if (rectangle == resources.Rectangle(texture, false) && offset == CurrentTextureOffset(current, file1Offset, texture, false))
                    {
                        Assert.Equal((byte)1, resources.Source(texture, false));
                        texturePixelIndices.Add(texture);
                        pixels.Add(texture);
                    }
                    if (rectangle == resources.Rectangle(texture, true) && offset == CurrentTextureOffset(current, file1Offset, texture, true))
                    {
                        Assert.Equal((byte)1, resources.Source(texture, true));
                        textureClutIndices.Add(texture);
                        cluts.Add(texture);
                    }
                }
                ushort? commonFile = null;
                if (rectangle.Y == 256 && rectangle.Width == 64 && rectangle.Height == 256 && rectangle.X is 704 or 768 or 832)
                {
                    ushort fileId = checked((ushort)(363 + (rectangle.X - 704) / 64));
                    if (completedRawFiles.Contains(fileId) && rawDestinations.TryGetValue(fileId, out uint destination) && destination == checked(0x20DFAB8u + (uint)offset))
                    {
                        rawUploads.Add(fileId);
                        commonFile = fileId;
                        Mark("common-file-upload-" + fileId, current, cold);
                    }
                }
                Assert.True(pixels.Count + cluts.Count > 0 || commonFile.HasValue, "Submission does not match an actual selected texture or completed common-file source owner.");
                uploads.Add(new { current.Frame, Slot = uploadIndex, Pixels = pixels, Cluts = cluts, CommonFile = commonFile, Rectangle = rectangle, ArenaOffset = offset, ByteLength = expected.Length, CurrentArenaWordsEqualVram = true });
            }
            textureQueued = true;
            vramUploadVerified = true;
            Mark("current-texture-upload", current, cold);
        }
        bool retainedArenaUpload = textures.Slots.Any(slot => slot is { Kind: 0, Rectangle: not null, ArenaOffset: int offset } && offset >= file1Offset && offset < file1Offset + 227440);
        if (retainedArenaUpload && JsonSerializer.Serialize(textures.Slots) != JsonSerializer.Serialize(prior.NativeEffectMemory!.Textures!.Slots))
        {
            textureQueued = true;
            Mark("texture-upload-retained", current, cold);
        }
        if (!textures.ReadVram().AsSpan().SequenceEqual(source.NativeEffectMemory!.Textures!.ReadVram()))
        {
            vramChanged = true;
            Mark("current-texture-vram", current, cold);
        }
        if (live && current.NativeCamera?.ControlResource is { } control)
        {
            byte[] file0Bytes = NativeBattleEffectInitializerPolicy.Assets(effect.Binding)[0].Data.ToArray();
            int cameraOffset = BinaryPrimitives.ReadInt32LittleEndian(file0Bytes.AsSpan(0x20));
            Assert.Equal(0xE8C, cameraOffset);
            byte[] expected = file0Bytes.AsSpan(cameraOffset).ToArray();
            if (control.Source.Data.AsSpan().SequenceEqual(expected))
            {
                bool firstRegistration = !cameraRegistered;
                if (firstRegistration)
                {
                    cameraSelectorAtRegistration = current.NativeCamera.Variables[0];
                    Assert.Equal(prior.NativeCamera!.Variables[0], cameraSelectorAtRegistration.Value);
                    skippedByRetainedSelector = cameraSelectorAtRegistration.Value < 0;
                    cameraScope = skippedByRetainedSelector ? "registered control terminated by retained negative selector; no track started" : "registered nonnegative selector; static-track playback required";
                }
                cameraRegistered = true;
                Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(textures.Animation));
                bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(control.Source.Data.AsSpan()[control.ModelTableOffset..]));
                if (playing)
                {
                    foreach (int slot in current.NativeCamera.Order)
                    {
                        NativeBattleCameraTask task = current.NativeCamera.Tasks[slot]!;
                        if (task.Bank.Data.AsSpan().SequenceEqual(control.Source.Data.AsSpan()[control.ModelTableOffset..]))
                        {
                            Assert.Contains(task.Channel, new byte[] { 0, 1 });
                            cameraChoices.Add(task.Channel);
                        }
                    }
                }
                if (skippedByRetainedSelector)
                {
                    Assert.Null(current.NativeCamera.ControlCursor);
                    Assert.False(playing);
                    if (firstRegistration)
                    {
                        Assert.Equal(cameraSelectorAtRegistration, current.NativeCamera.Scratch[0]);
                        Assert.Equal(JsonSerializer.Serialize(prior.NativeCamera!.Tasks), JsonSerializer.Serialize(current.NativeCamera.Tasks));
                        Assert.Equal(JsonSerializer.Serialize(prior.NativeCamera.Order), JsonSerializer.Serialize(current.NativeCamera.Order));
                        Assert.Equal(prior.NativeCamera.Mask, current.NativeCamera.Mask);
                        Assert.Equal(prior.NativeCamera.CurrentPose, current.NativeCamera.CurrentPose);
                        cameraSkipVerified = true;
                        Mark("camera-skipped-retained-selector", current, cold);
                    }
                }
                if (playing)
                {
                    cameraScope = "registered nonnegative selector; selected static-track playback observed";
                }
                cameraPlaying |= playing;
                cameraViewChanged |= playing && JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778()) != JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778());
                Mark("camera-registration", current, cold);
            }
        }
        foreach (BattleEvent item in current.Events.Skip(prior.Events.Length).Where(item => item.NativeModuleSound is not null))
        {
            sounds.Add(item.NativeModuleSound!.Sound);
            Mark("module-sound", current, cold);
        }
        NativeBattleLogicalCommand[] commands = CurrentCommands(current, entryCommonCursor ?? 0);
        foreach (NativeBattleLogicalCommand command in commands)
        {
            packetBanks.Add(command.Packet.Bank);
            primitiveOpcodes.Add(command.Opcode);
            if (command.Packet.Bank == "20DFAB8" && IsGeometry(command))
            {
                arenaGeometry = true;
                Mark("current-arena-geometry", current, cold);
            }
            else if (IsGeometry(command))
            {
                commonGeometry = true;
                Mark("current-common-geometry", current, cold);
            }
        }
        if (commands.Any(IsGeometry))
        {
            geometry = true;
            Mark("current-selected-geometry", current, cold);
        }
        if (live)
        {
            uint manager = Dword(core, 0x27973EC);
            byte pause35 = core.Read(manager + 0x35, 1)[0];
            Assert.Equal((byte)0, pause35);
            uint bank = Dword(core, manager + 0x90);
            int currentFile1 = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796508).ArenaOffset;
            int workspaceRelative = BinaryPrimitives.ReadInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(checked(currentFile1 + 0x1C), 4));
            Assert.Equal(0x70, workspaceRelative);
            Assert.Equal(checked(0x20DFAB8u + (uint)currentFile1 + (uint)workspaceRelative), bank);
            uint header = Dword(core, 0x279744C);
            int capacity = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(header + 0x38, 2));
            Assert.Contains(capacity, new[] { 8, 64 });
            programmeCapacities.Add(capacity);
            Mark("programme-current-owner", current, cold);
            if (capacity == 64)
            {
                Mark("programme-capacity64-current-alias", current, cold);
            }
            uint file0 = checked(0x20DFAB8u + (uint)Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x279650C).ArenaOffset);
            List<object> slots = [];
            for (int index = 0; index < 128; index++)
            {
                byte entry = core.Read(0x2797454u + (uint)index, 1)[0];
                if (entry == 0xFF)
                {
                    break;
                }
                uint address = checked(bank + (uint)(entry & 0x7F) * 0x100);
                var slotSnapshot = current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(address - 0x20DFAB8)), 0x100);
                byte[] bytes = slotSnapshot.Data;
                uint pc = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                ushort commandTickC8 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0xC8));
                Assert.Equal((ushort)128, commandTickC8);
                renderKinds.Add(bytes[0x1C]);
                if (bytes[0x1C] is 2 or 6 or 27)
                {
                    Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)) & 1);
                }
                if (bytes[0x1C] is 4 or 5 or 21)
                {
                    Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)) & 1);
                }
                List<object> particles = [];
                if (bytes[0x1C] == 6)
                {
                    uint particleBank = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xB8));
                    int particleOffset = checked((int)(particleBank - 0x20DFAB8));
                    byte[] particleHeader = current.NativeEffectMemory.Arena.Read(particleOffset, 2);
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(particleHeader);
                    Assert.Contains(count, new[] { 16, 32, 64 });
                    particleCounts.Add(count);
                    for (int particle = 0; particle < count; particle++)
                    {
                        var node = current.NativeEffectMemory.Arena.ReadSnapshot(checked(particleOffset + 0x10 + particle * 0x50), 0x50);
                        Assert.All(node.Known.Skip(4).Take(4), known => Assert.True(known));
                        uint particlePc = BinaryPrimitives.ReadUInt32LittleEndian(node.Data.AsSpan(4));
                        if (particlePc == 0)
                        {
                            continue;
                        }
                        Assert.True(node.Known[0x37]);
                        int placement = unchecked((sbyte)node.Data[0x37]);
                        Assert.Contains(placement, new[] { 1, 2 });
                        particlePlacements.Add(placement);
                        particles.Add(new { Index = particle, Address = particleBank + 0x10u + (uint)particle * 0x50, Pc = particlePc, Placement = placement, Sequence = BinaryPrimitives.ReadUInt32LittleEndian(node.Data.AsSpan(8)), FramePointer = BinaryPrimitives.ReadUInt32LittleEndian(node.Data.AsSpan(0x30)), Bytes = Convert.ToBase64String(node.Data), UnknownOffsets = Enumerable.Range(0, node.Known.Length).Where(offset => !node.Known[offset]).ToArray() });
                    }
                    Mark("particle-bank-" + count, current, cold);
                }
                slots.Add(new { Index = entry & 0x7F, Address = address, Pc = pc, RelativePc = (long)pc - file0, Render = bytes[0x1C], Actor = bytes[0x1B], Flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)), CommandTickC8 = commandTickC8, Bytes = Convert.ToBase64String(bytes), UnknownOffsets = Enumerable.Range(0, slotSnapshot.Known.Length).Where(offset => !slotSnapshot.Known[offset]).ToArray(), Particles = particles });
            }
            if (slots.Count > 1)
            {
                Mark("programme-children", current, cold);
            }
            Assert.InRange(slots.Count, 0, 63);
            uint headerWord2Address = Dword(core, 0x279744C) + 2;
            ushort headerWord2 = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(headerWord2Address, 2));
            if ((headerWord2 & 0x10) != 0)
            {
                Mark("phase10-live-midpoint", current, cold);
            }
            uint arenaStart = Dword(core, manager + 0xD4);
            uint arenaEnd = Dword(core, manager + 0xD8);
            Assert.True(arenaEnd >= arenaStart);
            uint arenaHalf = Dword(core, manager + 0xDC);
            ushort callbackCounter = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(manager + 0x32, 2));
            int callbackParity = callbackCounter & 1;
            uint currentRootProgrammePc = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(checked((int)(bank - 0x20DFAB8)), 4));
            int file0Offset = checked((int)(file0 - 0x20DFAB8));
            Assert.Equal((ushort)0xA021, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x8A), 2)));
            uint privateRelative = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x8C), 4));
            short privateSizeWord = BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x90), 2));
            uint expectedPrivateBase = checked(bank + privateRelative);
            uint expectedPrivateHalf = checked((uint)(privateSizeWord << 7));
            Assert.Equal(49152u, expectedPrivateHalf);
            bool beforePrivateSetter = priorRootProgrammePc.HasValue && priorFile0 == file0 && priorRootProgrammePc.Value >= checked(file0 + 0x70) && priorRootProgrammePc.Value <= checked(file0 + 0x8A);
            bool afterPrivateSetter = currentRootProgrammePc >= checked(file0 + 0x92) && currentRootProgrammePc < checked(file0 + 26092);
            bool privatePublicationMatches = arenaStart == expectedPrivateBase && arenaHalf == expectedPrivateHalf;
            bool privateSetterExecuted = beforePrivateSetter && afterPrivateSetter && privatePublicationMatches;
            if (beforePrivateSetter && afterPrivateSetter)
            {
                Assert.True(privatePublicationMatches, "The observed programme progress requires the actual private base and half-size publication.");
            }
            privateSetupObserved |= privateSetterExecuted;
            Assert.True(arenaStart == 0 && arenaHalf == 0 || arenaStart >= 0x20DFAB8 && arenaHalf == 49152, "The private packet owner is only partially published or has an unsupported physical extent.");
            uint arenaPacketStart = privateSetterExecuted ? checked(arenaStart + (uint)callbackParity * arenaHalf) : arenaStart;
            if (privateSetupObserved && arenaStart >= 0x20DFAB8 && (!priorArenaBase.HasValue || priorArenaBase.Value < 0x20DFAB8))
            {
                Assert.True(privateSetterExecuted, "The new private packet owner requires its observed pre-callback programme setter.");
            }
            Assert.True(arenaEnd >= arenaPacketStart);
            if (privateSetupObserved && arenaStart >= 0x20DFAB8)
            {
                Mark("private-half-" + (privateSetterExecuted ? callbackParity : 0), current, cold);
            }
            Assert.True(arenaEnd - arenaPacketStart <= 45852);
            if (privateSetupObserved && arenaStart >= 0x20DFAB8)
            {
                Assert.Equal(49152u, Dword(core, manager + 0xDC));
            }
            if (privateSetupObserved && priorArenaBase is >= 0x20DFAB8 && !privateSetterExecuted)
            {
                Assert.Equal(priorArenaBase!.Value, arenaStart);
                Assert.Equal(priorArenaHalf!.Value, arenaHalf);
            }
            Assert.True(arenaPacketStart >= arenaStart);
            Assert.True(arenaPacketStart - arenaStart == 0 || arenaPacketStart - arenaStart == arenaHalf);
            uint arenaUsed = checked(arenaEnd - arenaPacketStart);
            uint arenaAllocationEnd = checked(arenaStart + 2 * arenaHalf);
            if (privateSetupObserved)
            {
                Assert.True(arenaStart >= 0x20DFAB8);
                Assert.True(arenaUsed <= arenaHalf);
                Assert.True(arenaHalf - arenaUsed >= 52);
                Assert.True(arenaStart >= checked(0x20DFAB8u + (uint)currentFile1));
                Assert.True(arenaAllocationEnd <= checked(0x20DFAB8u + (uint)currentFile1 + 227440u));
            }
            else
            {
                Assert.DoesNotContain(commands, command => command.Packet.Bank == "20DFAB8");
            }
            byte[] drawOrderBytes = core.Read(0x2797554, 128);
            int drawOrderLength = Array.IndexOf(drawOrderBytes, (byte)0xFF);
            Assert.InRange(drawOrderLength, 0, 127);
            byte[] drawOrder = drawOrderBytes.AsSpan(0, drawOrderLength).ToArray();
            uint projectedWork = Dword(core, manager + 0x74);
            bool projectedWorkAvailable = projectedWork >= 0x20DFAB8 && (ulong)projectedWork + 193 * 16 <= 0x20DFAB8ul + NativeBattleEffectArena.Length;
            var projected = projectedWorkAvailable ? current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(projectedWork - 0x20DFAB8)), 193 * 16) : (Data: Array.Empty<byte>(), Known: Array.Empty<bool>());
            var depths = current.NativeEffectMemory.ScriptedGraphics!.Depths.ReadSnapshot(0, 193 * 4);
            uint temporaryAddress = Dword(core, 0x2797624);
            var temporary = current.NativeFileStaging!.ReadSnapshot(temporaryAddress, 0x180);
            uint temporaryCursor = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeFileStaging.Read(NativeBattleTemporaryMemory.Cursor, 4));
            int? cameraVariable22 = current.NativeGraphics!.ActionContext is not null ? NativeBattleCameraGraphics.ReadVariable(current.NativeGraphics, 22) : null;
            NativeBattlePrimitiveProcess process = current.NativeEffectMemory.PrimitiveProcess!;
            observations.Add(new { current.Frame, Callback = current.NativeGraphics!.Callback, Returned = effect.ReturnedDescriptor, Manager = manager, Pause35 = pause35, ProgrammeBank = bank, ProgrammeCapacity = capacity, Resource01Offset = currentFile1, WorkspaceRelative = workspaceRelative, CurrentPixel0Start = checked(0x20DFAB8u + (uint)currentFile1 + 0x870), ProgrammeEnd = checked(bank + (uint)capacity * 0x100), Entry = commonEntry, CommonPrefixStart = entryCommonCursor, CommonPrefixEnd = Dword(core, manager + 0x7C), CommonBank = process.PacketBank, CommonBuffer = process.PacketBuffer, CallbackEntryCommonAvailable = entryCommonAvailable, FrameEndResetCursor = process.ByteCursor, ArenaCursorStart = Dword(core, manager + 0xD4), ArenaCursorEnd = Dword(core, manager + 0xD8), ArenaPacketStart = arenaPacketStart, ArenaHalf = arenaHalf, ArenaUsed = arenaUsed, ArenaRemaining = arenaHalf - arenaUsed, ArenaReserve = 52, ArenaAllocationEnd = arenaAllocationEnd, CallbackCounter = callbackCounter, CallbackParity = callbackParity, PrivateSetterExecuted = privateSetterExecuted, PrivateCapacityGateApplied = privateSetupObserved, CurrentRootProgrammePc = currentRootProgrammePc, ExpectedPrivateBase = expectedPrivateBase, ExpectedPrivateHalf = expectedPrivateHalf, PriorRootProgrammePc = priorRootProgrammePc, PriorFile0 = priorFile0, PriorArenaBase = priorArenaBase, PriorArenaCursor = priorArenaCursor, PriorArenaHalf = priorArenaHalf, ActualPackets = commands, PacketBase = Dword(core, manager + 0x88), Header = Dword(core, manager + 0xCC), HeaderWord2 = headerWord2, Slots = slots, DrawOrder = drawOrder, ProjectedWork = projectedWork, ProjectedBytes = Convert.ToBase64String(projected.Data), ProjectedUnknownOffsets = Enumerable.Range(0, projected.Known.Length).Where(offset => !projected.Known[offset]).ToArray(), DepthBytes = Convert.ToBase64String(depths.Data), DepthUnknownOffsets = Enumerable.Range(0, depths.Known.Length).Where(offset => !depths.Known[offset]).ToArray(), Commands = commands.Length, ArenaCommands = commands.Count(command => command.Packet.Bank == "20DFAB8"), CommonCommands = commands.Count(command => command.Packet.Bank != "20DFAB8"), IoActive = io.ActiveCount, StaticTextureReservations = staticTextureReservations, VerifiedSubmittedSlots = submittedSlots.Order().ToArray(), CameraScope = cameraScope, SkippedByRetainedSelector = skippedByRetainedSelector, CameraSelectorAtRegistration = cameraSelectorAtRegistration, CameraSkipVerified = cameraSkipVerified, CameraVariable0 = current.NativeCamera?.Variables[0], CameraVariable22 = cameraVariable22, CameraScratch127 = current.NativeCamera?.Scratch[0], CameraControlCursor = current.NativeCamera?.ControlCursor, CrtRandom = current.NativeCrtRandom, Hp = afterHp, CurrentGte = current.NativeEffectMemory.GeometryRegisters, CurrentColors = current.NativeEffectMemory.ScriptedColorRegisters, TemporaryAddress = temporaryAddress, TemporaryCursor = temporaryCursor, TemporaryBytes = Convert.ToBase64String(temporary.Data), TemporaryUnknownOffsets = Enumerable.Range(0, temporary.Known.Length).Where(offset => !temporary.Known[offset]).ToArray(), CurrentRigs = new[] { current.NativeGraphics.Models[0], current.NativeGraphics.Models[3] }, IoRows = io.Rows, IoBusy = core.Read(0x2798219, 1)[0], RetainedTextureUploads = textures.Slots.Where(slot => slot is not null).Select(slot => new { slot!.Kind, slot.ArenaOffset, slot.Rectangle, slot.Copy }).ToArray() });
        }
        else
        {
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            pool.Validate();
            Assert.True(NativeBattleEffect257PresentationTests.TaskPoolDrained(effect), "The retained native descriptor must have null head/tail and no live task flags.");
            drained = true;
            Mark("native-task-drained", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Write(output);
        Assert.True(initialized);
        Assert.True(drained);
        Assert.Contains(0x96F900u, callbacks);
        Assert.Equal(1, hpTransitions);
        Assert.Contains(410151u, sounds);
        Assert.Contains(410154u, sounds);
        Assert.True(textureQueued);
        Assert.True(vramUploadVerified);
        Assert.True(randomAdvanced);
        if (source.NativeEffectMemory!.Cinematic?.Effect != 257)
        {
            Assert.True(vramChanged);
        }
        Assert.True(cameraRegistered);
        if (skippedByRetainedSelector)
        {
            Assert.True(cameraSkipVerified);
            Assert.Empty(cameraChoices);
        }
        else
        {
            Assert.True(cameraPlaying);
            Assert.True(cameraViewChanged);
            Assert.NotEmpty(cameraChoices);
        }
        Assert.True(geometry);
        Assert.NotEmpty(packetBanks);
        Assert.Contains("20DFAB8", packetBanks);
        Assert.True(arenaGeometry);
        Assert.True(commonGeometry);
        Assert.Contains(2, renderKinds);
        Assert.Contains(4, renderKinds);
        Assert.Contains(6, renderKinds);
        Assert.Contains(21, renderKinds);
        Assert.Contains(5, renderKinds);
        Assert.Equal(new[] { 16, 32, 64 }, particleCounts.Order());
        Assert.Contains(2, particlePlacements);
        Assert.Contains(64, programmeCapacities);
        Assert.Equal(Enumerable.Range(0, 8), textureClutIndices.Order());
        Assert.Equal(Enumerable.Range(0, 8), texturePixelIndices.Order());
        Assert.Equal(new ushort[] { 363, 364, 365 }, rawFiles.Order());
        Assert.Equal(new ushort[] { 363, 364, 365 }, completedRawFiles.Order());
        Assert.Equal(new ushort[] { 363, 364, 365 }, rawUploads.Order());
        Assert.Equal(3, ioStarts);
        Assert.Equal(3, ioCompletions);
        Assert.Empty(pendingIo);
        Assert.Equal((short?)0, completed.NativeFileIo!.ActiveCount);
        Assert.Equal((byte)0, completed.NativeEffectMemory!.ScriptedEffects!.Read(0x2798219, 1)[0]);
        Assert.True(NativeBattleEffect257PresentationTests.Released(completed));
    }

    public void Write(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect257-native-observations.json"), JsonSerializer.Serialize(new { Initialized = initialized, Drained = drained, Callbacks = callbacks.Order(), Sounds = sounds.Order(), PacketBanks = packetBanks.Order(), RenderKinds = renderKinds.Order(), ProgrammeCapacities = programmeCapacities.Order(), PrimitiveOpcodes = primitiveOpcodes.Order(), ParticleCounts = particleCounts.Order(), ParticlePlacements = particlePlacements.Order(), ArenaRequired = 45904, ArenaCapacity = 49152, CommonRequiredEntry = 2448, ProgrammeActiveBound = 63, CameraChoices = cameraChoices.Order(), TextureClutIndices = textureClutIndices.Order(), TexturePixelIndices = texturePixelIndices.Order(), VerifiedCurrentUploads = uploads, RawFiles = rawFiles.Order(), RawUploads = rawUploads.Order(), IoStarts = ioStarts, IoCompletions = ioCompletions, IoObservations = ioObservations, CameraRegistered = cameraRegistered, CameraScope = cameraScope, SkippedByRetainedSelector = skippedByRetainedSelector, CameraSelectorAtRegistration = cameraSelectorAtRegistration, CameraSkipVerified = cameraSkipVerified, CameraPlaying = cameraPlaying, CameraViewChanged = cameraViewChanged, TextureQueued = textureQueued, VramChanged = vramChanged, VramUploadVerified = vramUploadVerified, RandomAdvanced = randomAdvanced, Geometry = geometry, ArenaGeometry = arenaGeometry, CommonGeometry = commonGeometry, HpTransitions = hpTransitions, Markers = markers.Order(), Observations = observations, TraceScope = "Frame-end slot observations and independently observed side effects; not an opcode execution trace" }));
    }

    private static int CurrentTextureOffset(BattleSnapshot snapshot, int file1Offset, int index, bool clut)
    {
        NativeBattleEffectArena arena = snapshot.NativeEffectMemory!.Arena!;
        int directory = checked(file1Offset + BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked(file1Offset + (clut ? 8 : 0x14)), 4)));
        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked(directory + 4 * index), 4));
        return checked(directory + (int)(clut ? relative : relative & 0xFFFFFF));
    }

    private void ObserveIo(BattleSnapshot prior, BattleSnapshot current, NativeBattleOriginalEffectState effect, NativeBattleScriptedEffectMemory core, NativeBattleFileIoState io, Action<string, BattleSnapshot> cold)
    {
        Assert.NotNull(io.ActiveCount);
        Assert.InRange(io.ActiveCount!.Value, (short)0, (short)16);
        NativeBattleFileIoState previous = prior.NativeFileIo!;
        for (int slot = 0; slot < io.Rows.Length; slot++)
        {
            NativeBattleFileIoRow row = io.Rows[slot];
            NativeBattleFileIoRow before = previous.Rows[slot];
            bool wasOwned = before.SchedulerCallback is > 0 && before.CompletionCallback == 0x974E80;
            bool isOwned = row.SchedulerCallback is > 0 && row.CompletionCallback == 0x974E80;
            ushort? currentFile = null;
            uint destination = 0;
            if (isOwned)
            {
                Assert.Equal(32768, row.FileResult);
                destination = Dword(core, Dword(core, 0x27973EC) + 0xB4);
                byte[] delivered = current.NativeEffectMemory!.Arena!.Read(checked((int)(destination - 0x20DFAB8)), 32768);
                NativeBattleArchiveFile file = Assert.Single(effect.Binding.CinematicResources!.Files, candidate => delivered.AsSpan().SequenceEqual(candidate.Bytes.AsSpan()));
                Assert.Contains(file.FileId, new ushort[] { 363, 364, 365 });
                currentFile = file.FileId;
            }
            bool replaced = wasOwned && isOwned && pendingIo.TryGetValue(slot, out ushort pending) && (pending != currentFile!.Value || row.Countdown!.Value > before.Countdown!.Value);
            if (wasOwned && (!isOwned || replaced))
            {
                Assert.True(pendingIo.Remove(slot, out ushort completedFile));
                completedRawFiles.Add(completedFile);
                callbacks.Add(0x974E80);
                ioCompletions++;
                if (!isOwned)
                {
                    Assert.Equal((byte)0, core.Read(0x2798219, 1)[0]);
                }
                ioObservations.Add(new { current.Frame, Slot = slot, Kind = "complete", File = completedFile, Callback = before.CompletionCallback, row.Status, row.Countdown, Busy = core.Read(0x2798219, 1)[0], RequeuedSameCallback = replaced });
                Mark("file-completion-" + ioCompletions, current, cold);
            }
            if (isOwned && (!wasOwned || replaced))
            {
                ushort file = currentFile!.Value;
                Assert.False(pendingIo.ContainsKey(slot));
                pendingIo.Add(slot, file);
                rawFiles.Add(file);
                rawDestinations[file] = destination;
                ioStarts++;
                Assert.Equal((byte)0xFF, core.Read(0x2798219, 1)[0]);
                ioObservations.Add(new { current.Frame, Slot = slot, Kind = "start", File = file, row.SchedulerCallback, row.CompletionCallback, row.FileResult, Destination = destination, Busy = core.Read(0x2798219, 1)[0] });
                Mark("file-read-" + ioStarts, current, cold);
            }
            else if (isOwned)
            {
                ioObservations.Add(new { current.Frame, Slot = slot, Kind = "wait", File = currentFile, row.Countdown, Busy = core.Read(0x2798219, 1)[0] });
            }
        }
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot, int commonStart)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 257, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return [];
        }
        uint manager = Dword(core, 0x27973EC);
        uint arenaStart = Dword(core, manager + 0xD4);
        uint arenaEnd = Dword(core, manager + 0xD8);
        uint arenaHalf = Dword(core, manager + 0xDC);
        if (arenaHalf > 0 && arenaEnd >= arenaStart && arenaEnd - arenaStart >= arenaHalf)
        {
            arenaStart = checked(arenaStart + arenaHalf);
        }
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory.PrimitiveProcess!;
        uint commonEnd = Dword(core, manager + 0x7C);
        Assert.InRange(commonEnd, 0u, checked((uint)process.CommonPacketWriteCapacity));
        int commonBase = process.PacketBuffer * process.CommonPacketNominalCapacity;
        return primitive.LastCommands.Select(item => item.Command).Where(IsOwned).ToArray();

        bool IsOwned(NativeBattleLogicalCommand command)
        {
            if (command.Packet.ByteOffset < 0 || command.Opcode is < 0x20 or >= 0x80)
            {
                return false;
            }
            if (command.Packet.Bank == "20DFAB8")
            {
                uint address = checked(0x20DFAB8u + (uint)command.Packet.ByteOffset);
                if (command.Packet.Buffer != 0 || arenaStart < 0x20DFAB8 || address < arenaStart || address >= arenaEnd)
                {
                    return false;
                }
                byte[] arenaTag = snapshot.NativeEffectMemory!.Arena!.Read(command.Packet.ByteOffset, 4);
                int length = checked((1 + arenaTag[3]) * 4);
                Assert.True(address + length <= arenaEnd);
                return true;
            }
            NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet);
            if (normalized.Bank != process.PacketBank || normalized.Buffer != 0 || normalized.ByteOffset < commonBase || normalized.ByteOffset - commonBase < commonStart || (uint)(normalized.ByteOffset - commonBase) >= commonEnd)
            {
                return false;
            }
            NativeBattlePrimitiveProcess physical = process with { PacketBank = command.Packet.Bank, PacketBuffer = command.Packet.Buffer };
            byte[] tag = physical.ReadCommonPacket(command.Packet.ByteOffset, 4);
            int packetLength = checked((1 + tag[3]) * 4);
            Assert.True(normalized.ByteOffset - commonBase + packetLength <= commonEnd);
            return true;
        }
    }

    private static void RequireNoEarlierPacketProducer(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        Assert.All(graphics.ModuleEffects.Slots, task => Assert.Equal(0, task.Flags & 1));
        Assert.All(graphics.Auxiliary.Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Contains(task.Kind, new[] { NativeBattleAuxiliaryKind.CameraBlend509930, NativeBattleAuxiliaryKind.StageFade501F90 }));
        Assert.All(graphics.Descriptors.Queue, index => Assert.Equal((ushort)104, graphics.Descriptors.Packets[index].Command));
        Assert.All(graphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0), task => Assert.Contains(task.Kind, new[] { NativeBattleDescriptorTaskKind.Pump500CC0, NativeBattleDescriptorTaskKind.Action104, NativeBattleDescriptorTaskKind.DeferredCompletion500BC0 }));
    }

    internal static bool IsGeometry(NativeBattleLogicalCommand command)
    {
        return (command.Opcode & 0xFC) is 0x20 or 0x24 or 0x2C or 0x30 or 0x34 or 0x38 or 0x3C or 0x64 or 0x74 or 0x7C;
    }

    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
