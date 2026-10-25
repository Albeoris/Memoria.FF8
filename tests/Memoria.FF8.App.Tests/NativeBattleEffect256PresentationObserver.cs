using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect256PresentationObserver(BattleSnapshot source)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<string> packetBanks = [];
    private readonly HashSet<int> renderKinds = [];
    private readonly HashSet<ushort> rawFiles = [];
    private readonly HashSet<uint> particleBanks = [];
    private readonly List<object> observations = [];
    private bool initialized;
    private bool drained;
    private bool cameraRegistered;
    private bool cameraPlaying;
    private bool cameraViewChanged;
    private bool textureQueued;
    private bool vramChanged;
    private bool randomAdvanced;
    private bool geometry;
    private bool frameCopyCapture;
    private bool frameCopyOverlay;
    private int ioStarts;
    private int ioCompletions;
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
        if (effect?.Binding.Effect != 256 || effect.ReturnedDescriptor is null || !initialized && effect.ReturnedDescriptor == 0)
        {
            return;
        }
        bool live = effect.ReturnedDescriptor > 0;
        if (live)
        {
            Assert.Equal(256, current.NativeEffectMemory!.Cinematic!.Effect);
            int header = effect.Parameters!.HeaderIndex7C;
            Assert.Equal((byte)1, current.NativeActionPackets!.Headers[header].ReadByte(0x10));
            Assert.Equal((byte)0, current.NativeActionPackets.Headers[header].ReadByte(0x11));
            if (!initialized)
            {
                initialized = true;
                Mark("initialized-header11-zero", current, cold);
            }
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            Assert.Equal(0x2796548u, pool.DescriptorAddress);
            Assert.Equal(0x2796560u, pool.BankAddress);
            Assert.Equal((ushort)0x10, pool.Stride);
            Assert.Single(pool.Slots);
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(0x979A70u, task.Callback);
                callbacks.Add(task.Callback);
            }
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        NativeBattleFileIoState io = current.NativeFileIo!;
        NativeBattleFileIoState previousIo = prior.NativeFileIo!;
        for (int slot = 0; slot < io.Rows.Length; slot++)
        {
            NativeBattleFileIoRow row = io.Rows[slot];
            NativeBattleFileIoRow before = previousIo.Rows[slot];
            if (row.SchedulerCallback != 0 && row.CompletionCallback == 0x97E0A0 && (before.SchedulerCallback == 0 || before.CompletionCallback != row.CompletionCallback))
            {
                Assert.Equal(32768, row.FileResult);
                uint manager = Dword(core, 0x27973EC);
                uint destination = Dword(core, manager + 0xB4);
                byte[] delivered = current.NativeEffectMemory.Arena!.Read(checked((int)(destination - 0x20DFAB8)), 32768);
                NativeBattleArchiveFile file = Assert.Single(effect.Binding.CinematicResources!.Files, candidate => delivered.AsSpan().SequenceEqual(candidate.Bytes.AsSpan()));
                rawFiles.Add(file.FileId);
                ioStarts++;
                Mark("file-read-" + ioStarts, current, cold);
            }
            if (before.SchedulerCallback != 0 && before.CompletionCallback == 0x97E0A0 && row.SchedulerCallback == 0)
            {
                ioCompletions++;
                callbacks.Add(0x97E0A0);
                Assert.Equal((byte)0, core.Read(0x2798219, 1)[0]);
                Mark("file-completion-" + ioCompletions, current, cold);
            }
        }
        randomAdvanced |= current.NativeCrtRandom != source.NativeCrtRandom;
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        textureQueued |= textures.Count > (prior.NativeEffectMemory!.Textures?.Count ?? 0);
        if (!textures.ReadVram().AsSpan().SequenceEqual(source.NativeEffectMemory!.Textures!.ReadVram()))
        {
            vramChanged = true;
            Mark("current-texture-vram", current, cold);
        }
        if (live && current.NativeCamera?.ControlResource is { } control)
        {
            byte[] expected = NativeBattleEffectInitializerPolicy.Assets(effect.Binding)[0].Data.AsSpan()[0x84C..].ToArray();
            if (control.Source.Data.AsSpan().SequenceEqual(expected))
            {
                cameraRegistered = true;
                Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(textures.Animation));
                bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(control.Source.Data.AsSpan()[control.ModelTableOffset..]));
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
        if (commands.Count(IsFrameCopyStrip) >= 5)
        {
            frameCopyOverlay = true;
            Mark("framecopy-flat-strips", current, cold);
        }
        foreach (NativeBattleLogicalCommand command in commands)
        {
            packetBanks.Add(command.Packet.Bank);
        }
        if (commands.Any(IsGeometry))
        {
            geometry = true;
            Mark("current-common-geometry", current, cold);
        }
        if (live)
        {
            uint manager = Dword(core, 0x27973EC);
            uint bank = Dword(core, manager + 0x90);
            uint file0 = checked(0x20DFAB8u + (uint)Assert.Single(effect.ResourcePointers, pointer => pointer.GlobalAddress == 0x2796558).ArenaOffset);
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
                if (bytes[0x1C] == 26 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xC4)) == uint.MaxValue)
                {
                    frameCopyCapture = true;
                    Mark("framecopy-capture-state", current, cold);
                }
                uint? particleBank = null;
                int? liveParticles = null;
                if (bytes[0x1C] == 6)
                {
                    particleBank = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xB8));
                    byte[] headerBytes = current.NativeEffectMemory.Arena.Read(checked((int)(particleBank.Value - 0x20DFAB8)), 4);
                    int capacity = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes);
                    Assert.Equal(128, capacity);
                    int count = 0;
                    for (int particle = 0; particle < capacity; particle++)
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
                slots.Add(new { Index = entry & 0x7F, Address = address, Pc = pc, RelativePc = (long)pc - file0, Render = bytes[0x1C], Actor = bytes[0x1B], ParticleBank = particleBank, LiveParticles = liveParticles });
            }
            if (slots.Count > 1)
            {
                Mark("programme-children", current, cold);
            }
            observations.Add(new { current.Frame, Callback = current.NativeGraphics!.Callback, Returned = effect.ReturnedDescriptor, Manager = manager, ProgrammeBank = bank, Cursor = Dword(core, manager + 0x7C), PacketBase = Dword(core, manager + 0x88), Header = Dword(core, manager + 0xCC), IoBusy = core.Read(0x2798219, 1)[0], Slots = slots, Commands = commands.Length, IoActive = io.ActiveCount, Hp = afterHp });
        }
        else
        {
            NativeBattleEffectTaskPool pool = Assert.Single(effect.Pools);
            pool.Validate();
            Assert.True(NativeBattleEffect256PresentationTests.TaskPoolDrained(effect), "The retained native descriptor must have null head/tail and no live task flags.");
            drained = true;
            Mark("native-task-drained", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Write(output);
        Assert.True(initialized);
        Assert.True(drained);
        Assert.Contains(0x979A70u, callbacks);
        Assert.Contains(0x97E0A0u, callbacks);
        Assert.Equal(3, ioStarts);
        Assert.Equal(3, ioCompletions);
        Assert.Equal(new ushort[] { 0x16B, 0x16C, 0x16D }, rawFiles.Order());
        Assert.Equal(1, hpTransitions);
        Assert.NotEmpty(sounds);
        Assert.True(randomAdvanced);
        Assert.True(textureQueued);
        Assert.True(vramChanged);
        Assert.True(cameraRegistered);
        Assert.True(cameraPlaying);
        Assert.True(cameraViewChanged);
        Assert.True(geometry);
        Assert.True(frameCopyCapture);
        Assert.True(frameCopyOverlay);
        Assert.Equal(2, particleBanks.Count);
        Assert.Contains(6, renderKinds);
        Assert.Contains(26, renderKinds);
        Assert.True(NativeBattleEffect256PresentationTests.Released(completed));
    }

    public void Write(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect256-native-observations.json"), JsonSerializer.Serialize(new { Initialized = initialized, Drained = drained, Callbacks = callbacks.Order(), Sounds = sounds.Order(), PacketBanks = packetBanks.Order(), RenderKinds = renderKinds.Order(), RawFiles = rawFiles.Order(), ParticleBanks = particleBanks.Order(), CameraRegistered = cameraRegistered, CameraPlaying = cameraPlaying, CameraViewChanged = cameraViewChanged, TextureQueued = textureQueued, VramChanged = vramChanged, RandomAdvanced = randomAdvanced, Geometry = geometry, FrameCopyCapture = frameCopyCapture, FrameCopyOverlay = frameCopyOverlay, IoStarts = ioStarts, IoCompletions = ioCompletions, HpTransitions = hpTransitions, Markers = markers.Order(), Observations = observations, TraceScope = "Frame-end slot observations and independently observed side effects; not an opcode execution trace" }));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 256, ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return [];
        }
        uint manager = Dword(core, 0x27973EC);
        uint end = Dword(core, manager + 0x7C);
        return primitive.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is "off_B6D070" or "off_B6D094" && command.Packet.ByteOffset >= 0 && (uint)command.Packet.ByteOffset < end && command.Opcode is >= 0x20 and < 0x80).ToArray();
    }

    internal static bool IsGeometry(NativeBattleLogicalCommand command)
    {
        return (command.Opcode & 0xFC) is 0x24 or 0x2C or 0x34 or 0x3C or 0x64 or 0x74 or 0x7C;
    }

    private static bool IsFrameCopyStrip(NativeBattleLogicalCommand command)
    {
        if (command.Opcode != 0x2A || command.Words.Length != 5)
        {
            return false;
        }
        uint color = command.Words[0];
        short left = unchecked((short)command.Words[1]);
        short top = unchecked((short)(command.Words[1] >> 16));
        short right = unchecked((short)(left + 64));
        short bottom = unchecked((short)(top + 224));
        return (byte)color == (byte)(color >> 8) && (byte)color == (byte)(color >> 16) && command.Words[2] == Screen(right, top) && command.Words[3] == Screen(left, bottom) && command.Words[4] == Screen(right, bottom);

        static uint Screen(short x, short y) => unchecked((ushort)x | ((uint)(ushort)y << 16));
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
