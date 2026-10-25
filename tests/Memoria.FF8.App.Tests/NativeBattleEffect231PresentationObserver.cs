using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect231PresentationObserver(BattleSnapshot source)
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
        if (effect?.Binding.Effect != 231 || effect.ReturnedDescriptor is null || !initialized && effect.ReturnedDescriptor == 0)
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
            Assert.True(entryCommonAvailable >= 52, "The actual callback-entry COMMON owner does not meet the conditional packet bound.");
            if (prior.NativeEffectMemory.ScriptedEffects is { } entryCore)
            {
                uint entryManager = Dword(entryCore, 0x27973EC);
                if (entryManager != 0)
                {
                    priorArenaBase = Dword(entryCore, entryManager + 0xD4);
                    priorArenaCursor = Dword(entryCore, entryManager + 0xD8);
                    priorArenaHalf = Dword(entryCore, entryManager + 0xDC);
                    uint entryBank = Dword(entryCore, entryManager + 0x90);
                    if (entryBank >= 0x20DFAB8 && prior.NativeGraphics?.OriginalEffect is { Binding.Effect: 231, ReturnedDescriptor: > 0 } priorEffect)
                    {
                        priorRootProgrammePc = BinaryPrimitives.ReadUInt32LittleEndian(prior.NativeEffectMemory.Arena!.Read(checked((int)(entryBank - 0x20DFAB8)), 4));
                        priorFile0 = checked(0x20DFAB8u + (uint)Assert.Single(priorEffect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796AA4).ArenaOffset);
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
            Assert.Equal(231, current.NativeEffectMemory!.Cinematic!.Effect);
            Assert.Equal((byte)0, current.NativeEffectMemory.Cinematic.Filter145);
            int header = effect.Parameters!.HeaderIndex7C;
            Assert.Equal((byte)1, current.NativeActionPackets!.Headers[header].ReadByte(0x10));
            Assert.Equal((byte)0, current.NativeActionPackets.Headers[header].ReadByte(0x11));
            Assert.Equal(39, current.NativeGraphics!.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.False(current.NativeGraphics.Models[3].HasLinkedModelRing140);
            Assert.Equal(23, current.NativeGraphics.Models[0].CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.Equal(2, current.NativeGraphics.Models[0].CurrentLinkedBoneMatrices!.Matrices.Length);
            if (!initialized)
            {
                var initializerAssets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
                Assert.Equal(9268, initializerAssets[0].Data.Length);
                Assert.Equal(151608, initializerAssets[1].Data.Length);
                NativeBattleCinematicResources resources = effect.Binding.CinematicResources!;
                Assert.Equal(4, resources.TextureProfile.Count);
                Assert.All(Enumerable.Range(0, resources.TextureProfile.Count), texture =>
                {
                    Assert.Equal((byte)1, resources.Source(texture, false));
                    Assert.Equal((byte)1, resources.Source(texture, true));
                });
                initialized = true;
                Mark("initialized-header11-zero", current, cold);
            }
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            Assert.Equal(0x2796A90u, pool.DescriptorAddress);
            Assert.Equal(0x2796AA8u, pool.BankAddress);
            Assert.Equal((ushort)0x10, pool.Stride);
            Assert.Single(pool.Slots);
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(0xA66B10u, task.Callback);
                callbacks.Add(task.Callback);
            }
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        if (live)
        {
            Assert.InRange(Dword(core, Dword(core, 0x27973EC) + 0x7C), checked((uint)entryCommonCursor!.Value), checked((uint)entryCommonCursor.Value + 0));
        }
        randomAdvanced |= current.NativeCrtRandom != source.NativeCrtRandom;
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        textureQueued |= textures.Count > (prior.NativeEffectMemory!.Textures?.Count ?? 0);
        int file1Offset = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796AA0).ArenaOffset;
        uint managerAddress = Dword(core, 0x27973EC);
        int staticTextureReservations = core.Read(managerAddress + 0xD2, 1)[0];
        Assert.InRange(staticTextureReservations, 0, textures.Slots.Length);
        HashSet<int> submittedSlots = Enumerable.Range(0, staticTextureReservations).ToHashSet();
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
                Assert.InRange(offset, 0, NativeBattleEffectArena.Length - 2 * rectangle.Width * rectangle.Height);
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
                Assert.True(pixels.Count + cluts.Count > 0, "Submission must match a selected current texture owner.");
                uploads.Add(new { current.Frame, Slot = uploadIndex, Pixels = pixels, Cluts = cluts, Rectangle = rectangle, ArenaOffset = offset, ByteLength = expected.Length, CurrentArenaWordsEqualVram = true });
            }
            textureQueued = true;
            vramUploadVerified = true;
            Mark("current-texture-upload", current, cold);
        }
        bool retainedArenaUpload = textures.Slots.Any(slot => slot is { Kind: 0, Rectangle: not null, ArenaOffset: int offset } && offset >= file1Offset && offset < file1Offset + 151608);
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
            Assert.Equal(0x870, cameraOffset);
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
                            Assert.Contains(task.Channel, new byte[] { 0 });
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
            int currentFile1 = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796AA0).ArenaOffset;
            int workspaceRelative = BinaryPrimitives.ReadInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(checked(currentFile1 + 0x1C), 4));
            Assert.Equal(0x58, workspaceRelative);
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
            uint file0 = checked(0x20DFAB8u + (uint)Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796AA4).ArenaOffset);
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
                if (bytes[0x1C] is 2 or 6)
                {
                    Mark("render-kind-" + bytes[0x1C], current, cold);
                }
                if (bytes[0x1C] is 2 or 6)
                {
                    Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)) & 1);
                }
                List<object> particles = [];
                if (bytes[0x1C] == 6)
                {
                    uint particleBank = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xB8));
                    int particleOffset = checked((int)(particleBank - 0x20DFAB8));
                    byte[] particleHeader = current.NativeEffectMemory.Arena.Read(particleOffset, 2);
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(particleHeader);
                    Assert.Contains(count, new[] { 24, 32 });
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
                        Assert.Contains(placement, new[] { 1 });
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
            Assert.InRange(slots.Count, 0, 15);
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
            Assert.Equal((ushort)0xA021, BinaryPrimitives.ReadUInt16LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x6E), 2)));
            uint privateRelative = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x70), 4));
            short privateSizeWord = BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena.Read(checked(file0Offset + 0x74), 2));
            uint expectedPrivateBase = checked(bank + privateRelative);
            uint expectedPrivateHalf = checked((uint)(privateSizeWord << 7));
            Assert.Equal(0x4000u, privateRelative);
            Assert.Equal(32768u, expectedPrivateHalf);
            bool beforePrivateSetter = priorRootProgrammePc.HasValue && priorFile0 == file0 && priorRootProgrammePc.Value >= checked(file0 + 0x58) && priorRootProgrammePc.Value <= checked(file0 + 0x6E);
            bool afterPrivateSetter = currentRootProgrammePc >= checked(file0 + 0x76) && currentRootProgrammePc < checked(file0 + 9268);
            bool privatePublicationMatches = arenaStart == expectedPrivateBase && arenaHalf == expectedPrivateHalf;
            bool privateSetterExecuted = beforePrivateSetter && afterPrivateSetter && privatePublicationMatches;
            if (beforePrivateSetter && afterPrivateSetter)
            {
                Assert.True(privatePublicationMatches, "The observed programme progress requires the actual private base and half-size publication.");
            }
            privateSetupObserved |= privateSetterExecuted;
            Assert.True(arenaStart == 0 && arenaHalf == 0 || arenaStart >= 0x20DFAB8 && arenaHalf == 32768, "The private packet owner is only partially published or has an unsupported physical extent.");
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
            Assert.True(arenaEnd - arenaPacketStart <= 9616);
            if (privateSetupObserved && arenaStart >= 0x20DFAB8)
            {
                Assert.Equal(32768u, Dword(core, manager + 0xDC));
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
                Assert.Equal(65536u, 2 * arenaHalf);
                Assert.True(arenaUsed <= 9616);
                Assert.True(arenaAllocationEnd - arenaEnd >= 52);
                Assert.True(arenaStart >= checked(0x20DFAB8u + (uint)currentFile1));
                Assert.Equal(checked(bank + 0x14000u), arenaAllocationEnd);
                Assert.Equal(arenaAllocationEnd, Dword(core, manager + 0x70));
                uint dynamicCursor = Dword(core, manager + 0x74);
                Assert.InRange(dynamicCursor, arenaAllocationEnd, checked(arenaAllocationEnd + 7088));
                Assert.True((ulong)dynamicCursor + 2368 <= 0x20DFAB8ul + NativeBattleEffectArena.Length);
                if (privateSetterExecuted)
                {
                    Assert.DoesNotContain(commands, command => command.Packet.Bank == "20DFAB8");
                }
            }
            else
            {
                Assert.DoesNotContain(commands, command => command.Packet.Bank == "20DFAB8");
            }
            byte[] drawOrderBytes = core.Read(0x2797554, 128);
            int drawOrderLength = Array.IndexOf(drawOrderBytes, (byte)0xFF);
            Assert.InRange(drawOrderLength, 0, 127);
            byte[] drawOrder = drawOrderBytes.AsSpan(0, drawOrderLength).ToArray();
            if (drawOrder.Length > 0)
            {
                int lastDrawIndex = drawOrder[^1] & 0x7F;
                byte lastRenderKind = current.NativeEffectMemory.Arena!.Read(checked((int)(bank - 0x20DFAB8) + 0x100 * lastDrawIndex + 0x1C), 1)[0];
                if (lastRenderKind == 2)
                {
                    Mark("last-mesh-current-work", current, cold);
                }
            }
            uint projectedWork = Dword(core, manager + 0x74);
            bool projectedWorkAvailable = privateSetupObserved && projectedWork >= 0x20DFAB8 && (ulong)projectedWork + 2368 <= 0x20DFAB8ul + NativeBattleEffectArena.Length;
            var projected = projectedWorkAvailable ? current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(projectedWork - 0x20DFAB8)), 2368) : (Data: Array.Empty<byte>(), Known: Array.Empty<bool>());
            var depths = current.NativeEffectMemory.ScriptedGraphics!.Depths.ReadSnapshot(0, 148 * 4);
            uint temporaryAddress = Dword(core, 0x2797624);
            var temporary = current.NativeFileStaging!.ReadSnapshot(temporaryAddress, 0x180);
            uint temporaryCursor = BinaryPrimitives.ReadUInt32LittleEndian(current.NativeFileStaging.Read(NativeBattleTemporaryMemory.Cursor, 4));
            int? cameraVariable22 = current.NativeGraphics!.ActionContext is not null ? NativeBattleCameraGraphics.ReadVariable(current.NativeGraphics, 22) : null;
            NativeBattlePrimitiveProcess process = current.NativeEffectMemory.PrimitiveProcess!;
            observations.Add(new { current.Frame, Callback = current.NativeGraphics!.Callback, Returned = effect.ReturnedDescriptor, Manager = manager, Pause35 = pause35, ProgrammeBank = bank, ProgrammeCapacity = capacity, Resource01Offset = currentFile1, WorkspaceRelative = workspaceRelative, CurrentPixel0Start = checked(0x20DFAB8u + (uint)currentFile1 + 0x858), ProgrammeEnd = checked(bank + (uint)capacity * 0x100), Entry = commonEntry, CommonPrefixStart = entryCommonCursor, CommonPrefixEnd = Dword(core, manager + 0x7C), CommonBank = process.PacketBank, CommonBuffer = process.PacketBuffer, CallbackEntryCommonAvailable = entryCommonAvailable, FrameEndResetCursor = process.ByteCursor, ArenaCursorStart = Dword(core, manager + 0xD4), ArenaCursorEnd = Dword(core, manager + 0xD8), ArenaPacketStart = arenaPacketStart, ArenaHalf = arenaHalf, ArenaUsed = arenaUsed, ArenaRemaining = arenaAllocationEnd - arenaEnd, ArenaReserve = 52, ArenaReservedBytes = 2 * arenaHalf, ArenaAllocationEnd = arenaAllocationEnd, CallbackCounter = callbackCounter, CallbackParity = callbackParity, PrivateSetterExecuted = privateSetterExecuted, PrivateCapacityGateApplied = privateSetupObserved, CurrentRootProgrammePc = currentRootProgrammePc, ExpectedPrivateBase = expectedPrivateBase, ExpectedPrivateHalf = expectedPrivateHalf, PriorRootProgrammePc = priorRootProgrammePc, PriorFile0 = priorFile0, PriorArenaBase = priorArenaBase, PriorArenaCursor = priorArenaCursor, PriorArenaHalf = priorArenaHalf, ActualPackets = commands, PacketBase = Dword(core, manager + 0x88), Header = Dword(core, manager + 0xCC), HeaderWord2 = headerWord2, Slots = slots, DrawOrder = drawOrder, ProjectedWork = projectedWork, ProjectedBytes = Convert.ToBase64String(projected.Data), ProjectedUnknownOffsets = Enumerable.Range(0, projected.Known.Length).Where(offset => !projected.Known[offset]).ToArray(), DepthBytes = Convert.ToBase64String(depths.Data), DepthUnknownOffsets = Enumerable.Range(0, depths.Known.Length).Where(offset => !depths.Known[offset]).ToArray(), Commands = commands.Length, ArenaCommands = commands.Count(command => command.Packet.Bank == "20DFAB8"), CommonCommands = commands.Count(command => command.Packet.Bank != "20DFAB8"), StaticTextureReservations = staticTextureReservations, VerifiedSubmittedSlots = submittedSlots.Order().ToArray(), CameraScope = cameraScope, SkippedByRetainedSelector = skippedByRetainedSelector, CameraSelectorAtRegistration = cameraSelectorAtRegistration, CameraSkipVerified = cameraSkipVerified, CameraVariable0 = current.NativeCamera?.Variables[0], CameraVariable22 = cameraVariable22, CameraScratch127 = current.NativeCamera?.Scratch[0], CameraControlCursor = current.NativeCamera?.ControlCursor, CrtRandom = current.NativeCrtRandom, Hp = afterHp, CurrentGte = current.NativeEffectMemory.GeometryRegisters, CurrentColors = current.NativeEffectMemory.ScriptedColorRegisters, TemporaryAddress = temporaryAddress, TemporaryCursor = temporaryCursor, TemporaryBytes = Convert.ToBase64String(temporary.Data), TemporaryUnknownOffsets = Enumerable.Range(0, temporary.Known.Length).Where(offset => !temporary.Known[offset]).ToArray(), CurrentRigs = new[] { current.NativeGraphics.Models[0], current.NativeGraphics.Models[3] }, RetainedTextureUploads = textures.Slots.Where(slot => slot is not null).Select(slot => new { slot!.Kind, slot.ArenaOffset, slot.Rectangle, slot.Copy }).ToArray() });
        }
        else
        {
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            pool.Validate();
            Assert.True(NativeBattleEffect231PresentationTests.TaskPoolDrained(effect), "The retained native descriptor must have null head/tail and no live task flags.");
            drained = true;
            Mark("native-task-drained", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Write(output);
        Assert.True(initialized);
        Assert.True(drained);
        Assert.Contains(0xA66B10u, callbacks);
        Assert.Equal(1, hpTransitions);
        Assert.Contains(400168u, sounds);
        Assert.True(textureQueued);
        Assert.True(vramUploadVerified);
        Assert.True(randomAdvanced);
        if (source.NativeEffectMemory!.Cinematic?.Effect != 231)
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
        Assert.Contains(2, renderKinds);
        Assert.Contains(6, renderKinds);
        Assert.Equal(new[] { 24, 32 }, particleCounts.Order());
        Assert.Equal(new[] { 1 }, particlePlacements.Order());
        Assert.Contains(64, programmeCapacities);
        Assert.Equal(Enumerable.Range(0, 4), textureClutIndices.Order());
        Assert.Equal(Enumerable.Range(0, 4), texturePixelIndices.Order());
        Assert.True(NativeBattleEffect231PresentationTests.Released(completed));
    }

    public void Write(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect231-native-observations.json"), JsonSerializer.Serialize(new { Initialized = initialized, Drained = drained, Callbacks = callbacks.Order(), Sounds = sounds.Order(), PacketBanks = packetBanks.Order(), RenderKinds = renderKinds.Order(), ProgrammeCapacities = programmeCapacities.Order(), PrimitiveOpcodes = primitiveOpcodes.Order(), ParticleCounts = particleCounts.Order(), ParticlePlacements = particlePlacements.Order(), ArenaRequired = 9668, ArenaCapacity = 65536, CommonRequiredEntry = 52, ProgrammeActiveBound = 15, CameraChoices = cameraChoices.Order(), TextureClutIndices = textureClutIndices.Order(), TexturePixelIndices = texturePixelIndices.Order(), VerifiedCurrentUploads = uploads, DynamicAllocationBound = 7088, DynamicBorrowedWorkBound = 2368, DynamicRequired = 9456, CameraRegistered = cameraRegistered, CameraScope = cameraScope, SkippedByRetainedSelector = skippedByRetainedSelector, CameraSelectorAtRegistration = cameraSelectorAtRegistration, CameraSkipVerified = cameraSkipVerified, CameraPlaying = cameraPlaying, CameraViewChanged = cameraViewChanged, TextureQueued = textureQueued, VramChanged = vramChanged, VramUploadVerified = vramUploadVerified, RandomAdvanced = randomAdvanced, Geometry = geometry, ArenaGeometry = arenaGeometry, CommonGeometry = commonGeometry, HpTransitions = hpTransitions, Markers = markers.Order(), Observations = observations, TraceScope = "Frame-end current-owner observations; duplicate logical render passes retained; not an opcode trace" }));
    }

    private static int CurrentTextureOffset(BattleSnapshot snapshot, int file1Offset, int index, bool clut)
    {
        NativeBattleEffectArena arena = snapshot.NativeEffectMemory!.Arena!;
        int directory = checked(file1Offset + BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked(file1Offset + (clut ? 8 : 0x14)), 4)));
        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked(directory + 4 * index), 4));
        return checked(directory + (int)(clut ? relative : relative & 0xFFFFFF));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot, int commonStart)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 231, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return [];
        }
        uint manager = Dword(core, 0x27973EC);
        uint arenaStart = Dword(core, manager + 0xD4);
        uint arenaEnd = Dword(core, manager + 0xD8);
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory.PrimitiveProcess!;
        uint commonEnd = Dword(core, manager + 0x7C);
        Assert.InRange(commonEnd, 0u, checked((uint)process.CommonPacketWriteCapacity));
        int commonBase = process.PacketBuffer * process.CommonPacketNominalCapacity;
        return primitive.LastCommands.Select(item => item.Command).Where(IsOwned).ToArray();

        bool IsOwned(NativeBattleLogicalCommand command)
        {
            if (command.Packet.ByteOffset < 0 || command.Opcode is not (>= 0x20 and < 0x80) && command.Opcode != 0xE1)
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
