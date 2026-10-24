using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect323PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<string> positions = [];
    private readonly HashSet<int> sampledAges = [];
    private readonly HashSet<int> spritePhases = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool disabledCamera;
    private bool? cameraDecision;
    private bool hidden;
    private bool restored;
    private bool mainGeometry;
    private bool meshGeometry;
    private bool actGeometry;
    private bool morph;
    private bool particles;
    private bool recipientTriggered;
    private bool retired;
    private bool eulerGeometry;
    private bool eulerReached;
    private int particleBirths;
    private bool released;
    private bool detachedBanks;
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
        NativeBattleEffect323State state = current.NativeEffectMemory!.Effect323!;
        NativeBattleEffect323Resources resources = effect.Binding.Effect323Resources!;
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
                        Mark("effect323-native-camera-declined-selector-and-terminated-control", current, cold);
                    }
                }
            }
            Mark("effect323-current-camera-control-and-bank", current, cold);
        }
        NativeBattleModulePoolTask? retainedMain = effect.Pools.FirstOrDefault(pool => pool.DescriptorAddress == MainDescriptor)?.Slots[0];
        if (before is not null && oldAge is >= 12 and <= 63)
        {
            sampledAges.Add(oldAge);
            Assert.NotNull(retainedMain);
            Assert.Equal((ushort)1, before.ReadWord(14));
            positions.Add(Convert.ToHexString(retainedMain!.Payload.AsSpan().Slice(0xA4 - 12, 6)));
            if (oldAge < 63)
            {
                Assert.Equal(0u, graphics.Models[4].SequenceState!.MeshMask124 & 2);
                hidden = true;
            }
            int priorRecipient = before.ReadWord(0x10);
            if (retainedMain.ReadWord(0x10) != priorRecipient)
            {
                Assert.InRange(oldAge, 35, 44);
                Assert.Equal(priorRecipient + 1, retainedMain.ReadWord(0x10));
                uint model = before.ReadDword(0x20 + priorRecipient * 4);
                int actor = checked((int)((model - 0x1D972C0) / 156));
                NativeBattleActionHeaderSlot header = current.NativeActionPackets!.Headers[effect.Parameters!.HeaderIndex7C];
                int first = header.FirstContactIndex!.Value;
                Contacts += Enumerable.Range(first, header.ReadByte(16)).Count(index => current.NativeActionPackets.ReadContact(index).Primary.ActorIndex == actor);
                Assert.Equal(21, tasks.Values.Count(task => task.Callback == 0x65A660 && task.ReadWord(0x12) == 1));
                recipientTriggered = true;
                Mark("effect323-sorted-recipient-all-matching-contact-and-21-particle-births", current, cold);
            }
        }
        if (oldAge == 63 && main is null)
        {
            Assert.Equal(2u, graphics.Models[4].SequenceState!.MeshMask124 & 2);
            restored = true;
            retired = true;
            Mark("effect323-main-retired-restored-mask", current, cold);
        }
        foreach (NativeBattleModulePoolTask particle in tasks.Values.Where(task => task.Callback == 0x65A660))
        {
            int phase = particle.ReadWord(0x12);
            Assert.InRange(phase, 1, 9);
            spritePhases.Add(phase);
            if (phase == 1)
            {
                particleBirths++;
            }
            particles = true;
        }
        foreach ((uint address, NativeBattleModulePoolTask particle) in previous.Where(item => item.Value.Callback == 0x65A660))
        {
            if (particle.ReadWord(0x12) == 9 && !tasks.ContainsKey(address))
            {
                spritePhases.Add(10);
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
            if (cursor >= arena + FirstPacketOffset && cursor <= arena + SecondPacketOffset + 0x10000)
            {
                uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                if (CurrentCommands.Length > 0)
                {
                    banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                    mainGeometry |= oldAge is >= 12 and <= 63 && CurrentCommands.Any(command => Matches(command, resources.ReadMesh(0xFE13EC)));
                    meshGeometry |= oldAge is >= 12 and <= 70 && CurrentCommands.Any(command => Matches(command, resources.ReadMesh(0xFE2974)));
                    if (tasks.Values.Any(task => task.Callback == 0x65A720) || previous.Values.Any(task => task.Callback == 0x65A720))
                    {
                        actGeometry |= CurrentCommands.Any(command => resources.Geometry.Values.Any(mesh => Matches(command, mesh)));
                    }
                }
            }
            NativeBattleModulePoolTask? act = tasks.Values.FirstOrDefault(task => task.Callback == 0x65A720);
            if (act is not null)
            {
                Assert.Equal(ProgrammeAddress, act.ReadDword(WorkspaceOffset));
                int frame = checked((int)act.ReadDword(WorkspaceOffset + 4));
                if (frame is >= 3 and <= 12)
                {
                    eulerReached = true;
                    eulerGeometry |= CurrentCommands.Any(command => Matches(command, resources.ReadGeometry(0xFE30E8)));
                }
                if (frame is >= 7 and <= 10)
                {
                    var bytes = current.NativeEffectMemory.Arena!.ReadSnapshot(offset + MorphOffset, MorphLength);
                    morph |= Enumerable.Range(0, MorphLength / 8).All(row => bytes.Known.AsSpan(row * 8, 6).ToArray().All(known => known)) && bytes.Data.Any(value => value != 0);
                    Mark("effect323-current-ACT-and-current-morph208", current, cold);
                }
            }
        }
        if (age is 1 or 11 or 12 or 13 or 27 or 29 or 30 or 35 or 36 or 40 or 44 or 45 or 52 or 63)
        {
            Mark("effect323-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && prior.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: > 0 } entering)
        {
            foreach (NativeBattleEffectTaskPool pool in entering.Pools.Where(pool => pool.DescriptorAddress != RootDescriptor))
            {
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                {
                    int slot = pool.Slots.IndexOf(task);
                    int offset = checked((int)(pool.BankAddress - 0x20DFAB8) + slot * pool.Stride);
                    Assert.Equal(task.Callback, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(offset + 8, 4)));
                }
            }
            detachedBanks = true;
            Mark("effect323-current-COMMON-banks-detached-without-clearing", current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            released = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Cursor = cursor, Commands = CurrentCommands.Length, Contacts, ContactGlyphs, ActiveCallbacks = tasks.Values.Select(task => task.Callback).Distinct().Order() });
    }

    private static bool Matches(NativeBattleLogicalCommand command, NativeBattleStaticMeshResource mesh)
    {
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                if (group == 7 && (command.Opcode & 0xFC) == 0x3C && command.Words.Length == 12 && command.VertexPayload4.Length == 4)
                {
                    ReadOnlySpan<byte> gouraud = mesh.Bytes.AsSpan().Slice(cursor + face * strides[group], strides[group]);
                    if (command.Words[2] == BinaryPrimitives.ReadUInt32LittleEndian(gouraud[12..]) && command.Words[5] == BinaryPrimitives.ReadUInt32LittleEndian(gouraud[16..]) && (command.Words[8] & 0xFFFF) == BinaryPrimitives.ReadUInt16LittleEndian(gouraud[20..]) && (command.Words[11] & 0xFFFF) == BinaryPrimitives.ReadUInt16LittleEndian(gouraud[22..]))
                    {
                        return true;
                    }
                }
                if (group is not (2 or 3) || (command.Opcode & 0xFC) != (group == 2 ? 0x24 : 0x2C) || command.Words.Length != (group == 2 ? 6 : 8) + 1 || command.VertexPayload4.Length != 4)
                {
                    continue;
                }
                ReadOnlySpan<byte> data = mesh.Bytes.AsSpan().Slice(cursor + face * strides[group], strides[group]);
                uint first = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
                uint second = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]);
                if (command.Words[2] == first && command.Words[4] == second && (command.Words[6] & 0xFFFF) == BinaryPrimitives.ReadUInt16LittleEndian(data[(group == 2 ? 10 : 20)..]))
                {
                    return group == 2 || (command.Words[8] & 0xFFFF) == BinaryPrimitives.ReadUInt16LittleEndian(data[22..]);
                }
            }
            cursor += count * strides[group];
        }
        return false;
    }

    public void Publish(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect323-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Camera = camera, CameraTrack = track, CameraViewChanged = view, CameraDecision = cameraDecision, DisabledCameraBranch = disabledCamera, HiddenCasterMesh = hidden, RestoredCasterMesh = restored, TrajectoryPositions = positions.Count, MainGeometry = mainGeometry, MeshGeometry = meshGeometry, ActGeometry = actGeometry, Morph208 = morph, Particles = particles, RecipientTriggered = recipientTriggered, MainRetired = retired, Euler7014B0SourceOutputsReached = eulerReached, Euler7014B0GeometryVisible = eulerGeometry, SampledAges = sampledAges.Order(), ParticleBirths = particleBirths, SpritePhases = spritePhases.Order(), Released = released, DetachedBanksRetained = detachedBanks, Contacts, ContactGlyphs, Observations = observations }));
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera, "Both actual camera registrations must retain the original resource.");
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track && view, "A chosen action camera must play its actual track and advance its view.");
        }
        else
        {
            Assert.True(disabledCamera && !track, "A declined action camera must preserve selector -1, copy scratch127, terminate its control and start no selected track.");
        }
        Assert.True(hidden && restored && positions.Count > 10, "The current caster trajectory must hide and restore its selected body mesh.");
        Assert.True(mainGeometry && meshGeometry && actGeometry, "Each selected mesh phase must publish actual source-matching packets.");
        Assert.True(morph && particles && recipientTriggered && retired && released && detachedBanks);
        Assert.True(eulerReached);
        Assert.Equal(Enumerable.Range(12, 52), sampledAges.Order());
        Assert.Equal(21, particleBirths);
        Assert.Equal(Enumerable.Range(1, 10), spritePhases.Order());
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect323!.FixedBanks.Values, bank => Assert.NotNull(bank));
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
