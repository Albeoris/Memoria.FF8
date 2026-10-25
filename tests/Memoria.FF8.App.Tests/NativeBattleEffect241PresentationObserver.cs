using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect241PresentationObserver(BattleSnapshot source)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<string> packetBanks = [];
    private readonly HashSet<int> renderKinds = [];
    private readonly HashSet<int> programmeCapacities = [];
    private readonly HashSet<short> cameraChoices = [];
    private readonly HashSet<int> textureClutIndices = [];
    private readonly HashSet<int> texturePixelIndices = [];
    private readonly List<object> uploads = [];
    private readonly List<object> observations = [];
    private bool initialized;
    private bool drained;
    private bool cameraRegistered;
    private bool cameraPlaying;
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
        if (effect?.Binding.Effect != 241 || effect.ReturnedDescriptor is null || !initialized && effect.ReturnedDescriptor == 0)
        {
            return;
        }
        bool live = effect.ReturnedDescriptor > 0;
        int? entryCommonAvailable = null;
        int? entryCommonCursor = null;
        object? commonEntry = null;
        if (live)
        {
            RequireNoEarlierPacketProducer(prior);
            NativeBattlePrimitiveProcess beforeProcess = Assert.IsType<NativeBattlePrimitiveProcess>(prior.NativeEffectMemory!.PrimitiveProcess);
            NativeBattlePrimitiveProcess afterProcess = Assert.IsType<NativeBattlePrimitiveProcess>(current.NativeEffectMemory!.PrimitiveProcess);
            Assert.Equal(0, beforeProcess.ByteCursor);
            Assert.Equal(beforeProcess.PacketBank, afterProcess.PacketBank);
            Assert.Equal(beforeProcess.PacketBuffer, afterProcess.PacketBuffer);
            entryCommonCursor = beforeProcess.ByteCursor!.Value;
            entryCommonAvailable = beforeProcess.CommonPacketWriteCapacity - entryCommonCursor.Value;
            Assert.True(entryCommonAvailable >= 0, "Actual callback-entry COMMON extent is invalid.");
            NativeBattleGraphicsState entryGraphics = prior.NativeGraphics!;
            commonEntry = new
            {
                PriorFrame = prior.Frame,
                Cursor = beforeProcess.ByteCursor,
                Bank = beforeProcess.PacketBank,
                Buffer = beforeProcess.PacketBuffer,
                WriteCapacity = beforeProcess.CommonPacketWriteCapacity,
                LiveModuleFlags = entryGraphics.ModuleEffects.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Flags).ToArray(),
                LiveAuxKinds = entryGraphics.Auxiliary.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Kind).ToArray(),
                QueuedDescriptorCommands = entryGraphics.Descriptors.Queue.Select(index => entryGraphics.Descriptors.Packets[index].Command).ToArray(),
                ActiveDescriptorTaskKinds = entryGraphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0).Select(task => task.Kind).ToArray()
            };
            Assert.Equal(241, current.NativeEffectMemory!.Cinematic!.Effect);
            int header = effect.Parameters!.HeaderIndex7C;
            Assert.Equal((byte)1, current.NativeActionPackets!.Headers[header].ReadByte(0x10));
            Assert.Equal((byte)0, current.NativeActionPackets.Headers[header].ReadByte(0x11));
            Assert.Equal(45, current.NativeGraphics!.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
            if (!initialized)
            {
                var initializerAssets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
                Assert.Equal(7756, initializerAssets[0].Data.Length);
                Assert.Equal(143960, initializerAssets[1].Data.Length);
                NativeBattleCinematicResources resources = effect.Binding.CinematicResources!;
                Assert.Equal(5, resources.TextureProfile.Count);
                NativeBattleEffectTextureRectangle[] pixels = [new(512, 256, 128, 256), new(384, 256, 64, 128), new(384, 384, 64, 128), new(640, 256, 32, 128), new(448, 256, 64, 256)];
                short[] clutRows = [252, 255, 254, 251, 253];
                for (int texture = 0; texture < pixels.Length; texture++)
                {
                    Assert.Equal(pixels[texture], resources.Rectangle(texture, false));
                    Assert.Equal(new NativeBattleEffectTextureRectangle(320, clutRows[texture], 256, 1), resources.Rectangle(texture, true));
                }
                initialized = true;
                Mark("initialized-header11-zero", current, cold);
            }
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            Assert.Equal(0x2796860u, pool.DescriptorAddress);
            Assert.Equal(0x2796878u, pool.BankAddress);
            Assert.Equal((ushort)0x10, pool.Stride);
            Assert.Single(pool.Slots);
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(0xA02480u, task.Callback);
                callbacks.Add(task.Callback);
            }
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        if (live)
        {
            Assert.InRange(Dword(core, Dword(core, 0x27973EC) + 0x7C), 0u, checked((uint)entryCommonAvailable!.Value));
        }
        NativeBattleFileIoState io = current.NativeFileIo!;
        Assert.Equal((short?)0, io.ActiveCount);
        randomAdvanced |= current.NativeCrtRandom != source.NativeCrtRandom;
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        textureQueued |= textures.Count > (prior.NativeEffectMemory!.Textures?.Count ?? 0);
        int file1Offset = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796870).ArenaOffset;
        uint managerAddress = Dword(core, 0x27973EC);
        int submittedTextures = core.Read(managerAddress + 0xD2, 1)[0];
        if (live && submittedTextures > 0)
        {
            Assert.InRange(submittedTextures, 1, textures.Slots.Length);
            Assert.Equal(0, textures.Count);
            byte[] currentVram = textures.ReadVram();
            for (int uploadIndex = 0; uploadIndex < submittedTextures; uploadIndex++)
            {
                NativeBattleEffectTimUpload upload = Assert.IsType<NativeBattleEffectTimUpload>(textures.Slots[uploadIndex]);
                Assert.Equal(0, upload.Kind);
                NativeBattleEffectTextureRectangle rectangle = Assert.IsType<NativeBattleEffectTextureRectangle>(upload.Rectangle);
                int offset = Assert.IsType<int>(upload.ArenaOffset);
                Assert.InRange(offset, file1Offset, file1Offset + 143960 - 2 * rectangle.Width * rectangle.Height);
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
                    if (rectangle == resources.Rectangle(texture, false))
                    {
                        Assert.Equal((byte)1, resources.Source(texture, false));
                        texturePixelIndices.Add(texture);
                        pixels.Add(texture);
                    }
                    if (rectangle == resources.Rectangle(texture, true))
                    {
                        Assert.Equal((byte)1, resources.Source(texture, true));
                        textureClutIndices.Add(texture);
                        cluts.Add(texture);
                    }
                }
                Assert.True(pixels.Count + cluts.Count > 0, "Submission does not match a selected current texture rectangle.");
                uploads.Add(new { current.Frame, Slot = uploadIndex, Pixels = pixels, Cluts = cluts, Rectangle = rectangle, ArenaOffset = offset, ByteLength = expected.Length, CurrentArenaWordsEqualVram = true });
            }
            textureQueued = true;
            vramUploadVerified = true;
            Mark("current-texture-upload-" + current.Frame, current, cold);
        }
        bool retainedArenaUpload = textures.Slots.Any(slot => slot is { Kind: 0, Rectangle: not null, ArenaOffset: int offset } && offset >= file1Offset && offset < file1Offset + 143960);
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
            byte[] expected = NativeBattleEffectInitializerPolicy.Assets(effect.Binding)[0].Data.AsSpan()[0x6F0..].ToArray();
            if (control.Source.Data.AsSpan().SequenceEqual(expected))
            {
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
                            Assert.Contains(task.Channel, new byte[] { 0, 4 });
                            cameraChoices.Add(task.Channel);
                        }
                    }
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
        NativeBattleLogicalCommand[] commands = CurrentCommands(current);
        foreach (NativeBattleLogicalCommand command in commands)
        {
            packetBanks.Add(command.Packet.Bank);
            if (command.Packet.Bank == "20DFAB8")
            {
                arenaGeometry = true;
                Mark("current-arena-geometry", current, cold);
            }
            else
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
            int currentFile1 = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796870).ArenaOffset;
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
            uint file0 = checked(0x20DFAB8u + (uint)Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796874).ArenaOffset);
            List<object> slots = [];
            for (int index = 0; index < 128; index++)
            {
                byte entry = core.Read(0x2797454u + (uint)index, 1)[0];
                if (entry == 0xFF)
                {
                    break;
                }
                uint address = checked(bank + (uint)(entry & 0x7F) * 0x100);
                byte[] bytes = current.NativeEffectMemory.Arena!.Read(checked((int)(address - 0x20DFAB8)), 0x100);
                uint pc = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                ushort commandTickC8 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0xC8));
                Assert.Equal((ushort)128, commandTickC8);
                renderKinds.Add(bytes[0x1C]);
                if (bytes[0x1C] == 2)
                {
                    Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)) & 1);
                }
                if (bytes[0x1C] == 5)
                {
                    Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)) & 1);
                    Assert.Equal((byte)9, bytes[0x1E]);
                }
                slots.Add(new { Index = entry & 0x7F, Address = address, Pc = pc, RelativePc = (long)pc - file0, Render = bytes[0x1C], Actor = bytes[0x1B], Flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)), CommandTickC8 = commandTickC8 });
            }
            if (slots.Count > 1)
            {
                Mark("programme-children", current, cold);
            }
            int? cameraVariable22 = current.NativeGraphics!.ActionContext is not null ? NativeBattleCameraGraphics.ReadVariable(current.NativeGraphics, 22) : null;
            NativeBattlePrimitiveProcess process = current.NativeEffectMemory.PrimitiveProcess!;
            observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, Returned = effect.ReturnedDescriptor, Manager = manager, Pause35 = pause35, ProgrammeBank = bank, ProgrammeCapacity = capacity, Resource01Offset = currentFile1, WorkspaceRelative = workspaceRelative, CurrentPixel0Start = checked(0x20DFAB8u + (uint)currentFile1 + 0x858), ProgrammeEnd = checked(bank + (uint)capacity * 0x100), Entry = commonEntry, CommonPrefixStart = entryCommonCursor, CommonPrefixEnd = Dword(core, manager + 0x7C), CommonBank = process.PacketBank, CommonBuffer = process.PacketBuffer, CallbackEntryCommonAvailable = entryCommonAvailable, FrameEndResetCursor = process.ByteCursor, ArenaCursorStart = Dword(core, manager + 0xD4), ArenaCursorEnd = Dword(core, manager + 0xD8), PacketBase = Dword(core, manager + 0x88), Header = Dword(core, manager + 0xCC), Slots = slots, Commands = commands.Length, ArenaCommands = commands.Count(command => command.Packet.Bank == "20DFAB8"), CommonCommands = commands.Count(command => command.Packet.Bank != "20DFAB8"), IoActive = io.ActiveCount, SubmittedTextures = submittedTextures, CameraVariable0 = current.NativeCamera?.Variables[0], CameraVariable22 = cameraVariable22, CameraScratch127 = current.NativeCamera?.Scratch[0], CameraControlCursor = current.NativeCamera?.ControlCursor, CrtRandom = current.NativeCrtRandom, Hp = afterHp, RetainedTextureUploads = textures.Slots.Where(slot => slot is not null).Select(slot => new { slot!.Kind, slot.ArenaOffset, slot.Rectangle, slot.Copy }).ToArray() });
        }
        else
        {
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            pool.Validate();
            Assert.True(NativeBattleEffect241PresentationTests.TaskPoolDrained(effect), "The retained native descriptor must have null head/tail and no live task flags.");
            drained = true;
            Mark("native-task-drained", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Write(output);
        Assert.True(initialized);
        Assert.True(drained);
        Assert.Contains(0xA02480u, callbacks);
        Assert.Equal(1, hpTransitions);
        Assert.NotEmpty(sounds);
        Assert.True(textureQueued);
        Assert.True(vramUploadVerified);
        Assert.True(cameraRegistered);
        Assert.True(cameraPlaying);
        Assert.True(cameraViewChanged);
        Assert.True(geometry);
        Assert.NotEmpty(packetBanks);
        Assert.Contains("20DFAB8", packetBanks);
        Assert.True(arenaGeometry);
        Assert.True(commonGeometry);
        Assert.Contains(2, renderKinds);
        Assert.Contains(5, renderKinds);
        Assert.Contains(64, programmeCapacities);
        Assert.NotEmpty(cameraChoices);
        Assert.Equal(Enumerable.Range(0, 5), textureClutIndices.Order());
        Assert.Equal(Enumerable.Range(0, 5), texturePixelIndices.Order());
        Assert.True(NativeBattleEffect241PresentationTests.Released(completed));
    }

    public void Write(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect241-native-observations.json"), JsonSerializer.Serialize(new { Initialized = initialized, Drained = drained, Callbacks = callbacks.Order(), Sounds = sounds.Order(), PacketBanks = packetBanks.Order(), RenderKinds = renderKinds.Order(), ProgrammeCapacities = programmeCapacities.Order(), CameraChoices = cameraChoices.Order(), TextureClutIndices = textureClutIndices.Order(), TexturePixelIndices = texturePixelIndices.Order(), VerifiedCurrentUploads = uploads, CameraRegistered = cameraRegistered, CameraPlaying = cameraPlaying, CameraViewChanged = cameraViewChanged, TextureQueued = textureQueued, VramChanged = vramChanged, VramUploadVerified = vramUploadVerified, RandomAdvanced = randomAdvanced, Geometry = geometry, ArenaGeometry = arenaGeometry, CommonGeometry = commonGeometry, HpTransitions = hpTransitions, Markers = markers.Order(), Observations = observations, TraceScope = "Frame-end slot observations and independently observed side effects; not an opcode execution trace" }));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 241, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
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
            if (command.Packet.ByteOffset < 0 || command.Opcode is < 0x20 or >= 0x80)
            {
                return false;
            }
            if (command.Packet.Bank == "20DFAB8")
            {
                uint address = checked(0x20DFAB8u + (uint)command.Packet.ByteOffset);
                return command.Packet.Buffer == 0 && arenaStart >= 0x20DFAB8 && address >= arenaStart && address < arenaEnd;
            }
            NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet);
            if (normalized.Bank != process.PacketBank || normalized.Buffer != 0 || normalized.ByteOffset < commonBase || (uint)(normalized.ByteOffset - commonBase) >= commonEnd)
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
        return (command.Opcode & 0xFC) is 0x20 or 0x24 or 0x28 or 0x2C or 0x30 or 0x34 or 0x38 or 0x3C or 0x64 or 0x74 or 0x7C;
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
