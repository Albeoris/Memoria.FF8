using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect232PresentationObserver(BattleSnapshot source)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<string> packetBanks = [];
    private readonly HashSet<int> renderKinds = [];
    private readonly HashSet<uint> particleBanks = [];
    private readonly HashSet<int> particleCapacities = [];
    private readonly HashSet<int> cloneAnimations = [];
    private readonly HashSet<uint> clones = [];
    private readonly HashSet<int> programmeCapacities = [];
    private readonly HashSet<short> cameraChoices = [];
    private readonly HashSet<int> textureClutIndices = [];
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
        if (effect?.Binding.Effect != 232 || effect.ReturnedDescriptor is null || !initialized && effect.ReturnedDescriptor == 0)
        {
            return;
        }
        bool live = effect.ReturnedDescriptor > 0;
        if (live)
        {
            Assert.Equal(232, current.NativeEffectMemory!.Cinematic!.Effect);
            int header = effect.Parameters!.HeaderIndex7C;
            Assert.Equal((byte)1, current.NativeActionPackets!.Headers[header].ReadByte(0x10));
            Assert.Equal((byte)0, current.NativeActionPackets.Headers[header].ReadByte(0x11));
            Assert.Equal(39, current.NativeGraphics!.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
            if (!initialized)
            {
                initialized = true;
                Mark("initialized-header11-zero", current, cold);
            }
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            Assert.Equal(0x2796A58u, pool.DescriptorAddress);
            Assert.Equal(0x2796A70u, pool.BankAddress);
            Assert.Equal((ushort)0x10, pool.Stride);
            Assert.Single(pool.Slots);
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(0xA5C950u, task.Callback);
                callbacks.Add(task.Callback);
            }
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        NativeBattleFileIoState io = current.NativeFileIo!;
        randomAdvanced |= current.NativeCrtRandom != source.NativeCrtRandom;
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        textureQueued |= textures.Count > (prior.NativeEffectMemory!.Textures?.Count ?? 0);
        int file1Offset = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796A68).ArenaOffset;
        uint managerAddress = Dword(core, 0x27973EC);
        int submittedTextures = core.Read(managerAddress + 0xD2, 1)[0];
        if (live && submittedTextures > 0)
        {
            Assert.InRange(submittedTextures, 1, 12);
            Assert.Equal(0, textures.Count);
            byte[] currentVram = textures.ReadVram();
            foreach (NativeBattleEffectTimUpload? upload in textures.Slots.Take(submittedTextures))
            {
                Assert.NotNull(upload);
                Assert.Equal(0, upload.Kind);
                NativeBattleEffectTextureRectangle rectangle = Assert.IsType<NativeBattleEffectTextureRectangle>(upload.Rectangle);
                int offset = Assert.IsType<int>(upload.ArenaOffset);
                Assert.InRange(offset, file1Offset, file1Offset + 153744 - 2 * rectangle.Width * rectangle.Height);
                byte[] expected = current.NativeEffectMemory.Arena!.Read(offset, 2 * rectangle.Width * rectangle.Height);
                for (int row = 0; row < rectangle.Height; row++)
                {
                    Assert.True(currentVram.AsSpan(2 * ((rectangle.Y + row) * 1024 + rectangle.X), 2 * rectangle.Width).SequenceEqual(expected.AsSpan(2 * row * rectangle.Width, 2 * rectangle.Width)));
                }
            }
            textureQueued = true;
            vramUploadVerified = true;
            Mark("current-texture-upload-verified", current, cold);
        }
        bool retainedArenaUpload = textures.Slots.Any(slot => slot is { Kind: 0, Rectangle: not null, ArenaOffset: int offset } && offset >= file1Offset && offset < file1Offset + 153744);
        for (int texture = 0; texture < 4; texture++)
        {
            NativeBattleEffectTextureRectangle rectangle = effect.Binding.CinematicResources!.Rectangle(texture, true);
            if (textures.Slots.Any(slot => slot is { Kind: 0, ArenaOffset: int offset } && offset >= file1Offset && offset < file1Offset + 153744 && slot.Rectangle == rectangle))
            {
                textureClutIndices.Add(texture);
            }
        }
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
            byte[] expected = NativeBattleEffectInitializerPolicy.Assets(effect.Binding)[0].Data.AsSpan()[0x8F0..].ToArray();
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
        }
        if (commands.Any(IsGeometry))
        {
            geometry = true;
            Mark("current-arena-geometry", current, cold);
        }
        if (live)
        {
            uint manager = Dword(core, 0x27973EC);
            uint bank = Dword(core, manager + 0x90);
            int currentFile1 = Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796A68).ArenaOffset;
            Assert.Equal(checked(0x20DFAB8u + (uint)currentFile1 + 0x50), bank);
            uint header = Dword(core, 0x279744C);
            int capacity = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(header + 0x38, 2));
            Assert.Contains(capacity, new[] { 8, 64 });
            programmeCapacities.Add(capacity);
            if (capacity == 64)
            {
                Mark("programme-capacity64-current-alias", current, cold);
            }
            uint file0 = checked(0x20DFAB8u + (uint)Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796A6C).ArenaOffset);
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
                renderKinds.Add(bytes[0x1C]);
                uint? particleBank = null;
                int? liveParticles = null;
                object? cloneObservation = null;
                if (bytes[0x1C] == 44)
                {
                    Assert.Equal((byte)3, bytes[0x1B]);
                    uint clone = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xBC));
                    if (clone != 0)
                    {
                        NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                        byte[] animation = arena.Read(checked((int)(clone - 0x20DFAB8) + 0x20), 8);
                        int selected = animation[0];
                        Assert.Contains(selected, new[] { 26, 27, 28 });
                        Assert.Equal(selected == 27 ? (byte)12 : (byte)32, animation[7]);
                        Assert.InRange(animation[6], (byte)1, animation[7]);
                        Assert.Equal(0, animation[1] & 3);
                        uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked((int)(clone - 0x20DFAB8) + 0x30), 4));
                        Assert.Equal((byte)39, arena.Read(checked((int)(skeleton - 0x20DFAB8)), 1)[0]);
                        uint actor = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked((int)(clone - 0x20DFAB8) + 0xC), 4));
                        uint animationOwner = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked((int)(clone - 0x20DFAB8) + 0x38), 4));
                        Assert.Equal(current.NativeModelCpuResources!.Rows[0].Require(20).PhysicalAddress, animationOwner);
                        clones.Add(clone);
                        cloneAnimations.Add(selected);
                        cloneObservation = new { Address = clone, Actor = actor, Animation = selected, CurrentFrame = animation[6], FrameLimit = animation[7], ByteOffset = BinaryPrimitives.ReadUInt16LittleEndian(animation.AsSpan(2)), BitOffset = BinaryPrimitives.ReadUInt16LittleEndian(animation.AsSpan(4)), Skeleton = skeleton, AnimationOwner = animationOwner, Bones = 39 };
                        Mark("actor-animation-" + selected, current, cold);
                    }
                }
                if (bytes[0x1C] == 6)
                {
                    particleBank = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xB8));
                    byte[] headerBytes = current.NativeEffectMemory.Arena.Read(checked((int)(particleBank.Value - 0x20DFAB8)), 4);
                    int particleCapacity = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes);
                    Assert.Contains(particleCapacity, new[] { 16, 32 });
                    particleCapacities.Add(particleCapacity);
                    int count = 0;
                    for (int particle = 0; particle < particleCapacity; particle++)
                    {
                        byte[] flags = current.NativeEffectMemory.Arena.Read(checked((int)(particleBank.Value - 0x20DFAB8) + 0x10 + particle * 0x50), 2);
                        count += (BinaryPrimitives.ReadUInt16LittleEndian(flags) & 1) != 0 ? 1 : 0;
                    }
                    liveParticles = count;
                    particleBanks.Add(particleBank.Value);
                    Mark("particle-bank-" + particleBanks.Count, current, cold);
                    if (count > 0)
                    {
                        Mark("particle-birth", current, cold);
                    }
                }
                slots.Add(new { Index = entry & 0x7F, Address = address, Pc = pc, RelativePc = (long)pc - file0, Render = bytes[0x1C], Actor = bytes[0x1B], Flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x4C)), ParticleBank = particleBank, LiveParticles = liveParticles, Clone = cloneObservation });
            }
            if (slots.Count > 1)
            {
                Mark("programme-children", current, cold);
            }
            int? cameraVariable22 = current.NativeGraphics!.ActionContext is not null ? NativeBattleCameraGraphics.ReadVariable(current.NativeGraphics, 22) : null;
            observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, Returned = effect.ReturnedDescriptor, Manager = manager, ProgrammeBank = bank, ProgrammeCapacity = capacity, CurrentPixel0Start = checked(0x20DFAB8u + (uint)currentFile1 + 0x850), ProgrammeEnd = checked(bank + (uint)capacity * 0x100), Cursor = Dword(core, manager + 0x7C), ArenaCursorStart = Dword(core, manager + 0xD4), ArenaCursorEnd = Dword(core, manager + 0xD8), PacketBase = Dword(core, manager + 0x88), Header = Dword(core, manager + 0xCC), Slots = slots, Commands = commands.Length, IoActive = io.ActiveCount, SubmittedTextures = submittedTextures, CameraVariable0 = current.NativeCamera?.Variables[0], CameraVariable22 = cameraVariable22, CameraControlCursor = current.NativeCamera?.ControlCursor, CrtRandom = current.NativeCrtRandom, Hp = afterHp, RetainedTextureUploads = textures.Slots.Where(slot => slot is not null).Select(slot => new { slot!.Kind, slot.ArenaOffset, slot.Rectangle, slot.Copy }).ToArray() });
        }
        else
        {
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            pool.Validate();
            Assert.True(NativeBattleEffect232PresentationTests.TaskPoolDrained(effect), "The retained native descriptor must have null head/tail and no live task flags.");
            drained = true;
            Mark("native-task-drained", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Write(output);
        Assert.True(initialized);
        Assert.True(drained);
        Assert.Contains(0xA5C950u, callbacks);
        Assert.Equal(1, hpTransitions);
        Assert.NotEmpty(sounds);
        Assert.True(randomAdvanced);
        Assert.True(textureQueued);
        Assert.True(vramUploadVerified);
        if (source.NativeEffectMemory!.Cinematic?.Effect != 232)
        {
            Assert.True(vramChanged);
        }
        Assert.True(cameraRegistered);
        Assert.True(cameraPlaying);
        Assert.True(cameraViewChanged);
        Assert.True(geometry);
        Assert.Equal(new[] { "20DFAB8" }, packetBanks.Order());
        Assert.Equal(2, particleBanks.Count);
        Assert.Contains(6, renderKinds);
        Assert.Contains(2, renderKinds);
        Assert.Contains(44, renderKinds);
        Assert.Equal(new[] { 16, 32 }, particleCapacities.Order());
        Assert.Equal(new[] { 26, 27, 28 }, cloneAnimations.Order());
        Assert.Contains(64, programmeCapacities);
        Assert.NotEmpty(cameraChoices);
        Assert.Equal(Enumerable.Range(0, 4), textureClutIndices.Order());
        Assert.True(NativeBattleEffect232PresentationTests.Released(completed));
    }

    public void Write(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect232-native-observations.json"), JsonSerializer.Serialize(new { Initialized = initialized, Drained = drained, Callbacks = callbacks.Order(), Sounds = sounds.Order(), PacketBanks = packetBanks.Order(), RenderKinds = renderKinds.Order(), ParticleBanks = particleBanks.Order(), ParticleCapacities = particleCapacities.Order(), CloneAnimations = cloneAnimations.Order(), Clones = clones.Order(), ProgrammeCapacities = programmeCapacities.Order(), CameraChoices = cameraChoices.Order(), TextureClutIndices = textureClutIndices.Order(), CameraRegistered = cameraRegistered, CameraPlaying = cameraPlaying, CameraViewChanged = cameraViewChanged, TextureQueued = textureQueued, VramChanged = vramChanged, VramUploadVerified = vramUploadVerified, RandomAdvanced = randomAdvanced, Geometry = geometry, HpTransitions = hpTransitions, Markers = markers.Order(), Observations = observations, TraceScope = "Frame-end slot observations and independently observed side effects; not an opcode execution trace" }));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 232, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return [];
        }
        uint manager = Dword(core, 0x27973EC);
        uint arenaStart = Dword(core, manager + 0xD4);
        uint arenaEnd = Dword(core, manager + 0xD8);
        return primitive.LastCommands.Select(item => item.Command).Where(IsOwned).ToArray();

        bool IsOwned(NativeBattleLogicalCommand command)
        {
            if (command.Packet.ByteOffset < 0 || command.Opcode is < 0x20 or >= 0x80)
            {
                return false;
            }
            uint address = checked(0x20DFAB8u + (uint)command.Packet.ByteOffset);
            return command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && arenaStart >= 0x20DFAB8 && address >= arenaStart && address < arenaEnd;
        }
    }

    internal static bool IsGeometry(NativeBattleLogicalCommand command)
    {
        return (command.Opcode & 0xFC) is 0x24 or 0x2C or 0x34 or 0x3C or 0x64 or 0x74 or 0x7C;
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
