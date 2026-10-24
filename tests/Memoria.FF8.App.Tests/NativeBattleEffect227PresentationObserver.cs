using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect227PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<int> texts = [];
    private readonly HashSet<uint> programmes = [];
    private readonly List<object> observations = [];
    private bool registered;
    private bool cameraTrack;
    private bool cameraViewChanged;
    private bool sprite;
    private bool particles;
    private bool overlapTwo;
    private bool overlapThree;
    private bool mainRetired;
    private bool entryFlag;
    private bool terminalFlag;
    private bool vibrationStarted;
    private bool vibrationStopped;
    private int? vibrationHandle;
    private byte[]? morphData;
    private bool[]? morphKnown;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentMeshCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentMeshCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect227State state = current.NativeEffectMemory!.Effect227!;
        NativeBattleEffect227Resources resources = effect.Binding.Effect227Resources!;
        state.Validate();
        Assert.Equal((byte)0, current.NativeActionPackets!.Headers[effect.Parameters!.HeaderIndex7C].ReadByte(16));
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        uint arena = state.ReadDword(ArenaGlobal);
        Assert.Equal((byte)137, graphics.Models[3].ModelId4);
        Assert.Equal((byte)139, graphics.Models[4].ModelId4);
        string companion = graphics.ActorIds[4]!;
        BattleCombatantSnapshot companionState = current.Combatants.Single(actor => actor.Id == companion);
        Assert.True(companionState.NativeParticipant!.Hidden);
        Assert.True(companionState.NativeParticipant.Suspended);
        Assert.True(companionState.NativeParticipant.Untargetable);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            if (pool.DescriptorAddress == RootDescriptor)
            {
                Assert.Equal(RootBank, pool.BankAddress);
                Assert.Null(state.FixedBanks[RootBank]);
            }
            else
            {
                NativeBattleCureFamilyPool shape = CommonPools.Single(item => item.Descriptor == pool.DescriptorAddress);
                Assert.Equal(arena + shape.Bank, pool.BankAddress);
                Assert.Equal(shape.Stride, pool.Stride);
                Assert.Equal(shape.Capacity, pool.Slots.Length);
            }
        }
        if (!entryFlag && effect.ReturnedDescriptor > 0)
        {
            Assert.Equal(0, graphics.Models[3].Flags0 & 4);
            entryFlag = true;
            Mark("effect227-entry-cleared-model-WORD-bit4", current, cold);
        }
        NativeBattleEffectAnimationRegisters? animation = current.NativeEffectMemory.Textures?.Animation;
        if (animation?.Source.Identity == resources.Camera.Identity && current.NativeCamera?.ControlResource?.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, animation.StreamOffset);
            Assert.Equal(24, animation.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(animation), JsonSerializer.Serialize(current.NativeCamera.ControlResource));
            registered = true;
            cameraTrack |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            cameraViewChanged |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect227-current-camera-control-and-tracks", current, cold);
        }
        if (age is 1 or 38 or 85 or 95 or 115 or 165 or 180 or 202 or 212 or 222 or 245 or 258 or 260 or 262 or 268 or 270 or 278 or 280 or 284)
        {
            Mark("effect227-before-main-age-" + age, current, cold);
        }
        if (main is not null)
        {
            int handle = unchecked((int)main.ReadDword(16));
            vibrationHandle ??= handle;
            Assert.Equal(vibrationHandle.Value, handle);
            if (!vibrationStarted)
            {
                Assert.True(current.NativeVibrationProcess!.IsPlaying(handle));
                NativeVibrationSlot slot = current.NativeVibrationProcess.Slots[handle & 3];
                Assert.Equal(handle, slot.Generation * 16 | slot.Id);
                vibrationStarted = true;
                Mark("effect227-native-vibration-returned-handle", current, cold);
            }
            foreach ((int offset, uint source) in new (int, uint)[] { (0x1C, 0x112DCF8), (0x358, 0x112FE80), (0x418, 0x1130368) })
            {
                if (main.ReadDword(offset) == source)
                {
                    programmes.Add(source);
                    Mark("effect227-current-programme-" + source.ToString("X8"), current, cold);
                }
            }
            if (oldAge is 260 or 261)
            {
                Assert.Equal(0x112DCF8u, main.ReadDword(0x1C));
                Assert.Equal(0x112FE80u, main.ReadDword(0x358));
                overlapTwo = true;
                Mark("effect227-two-current-programmes-overlap", current, cold);
            }
            if (oldAge is 268 or 269)
            {
                Assert.Equal(0x112DCF8u, main.ReadDword(0x1C));
                Assert.Equal(0x112FE80u, main.ReadDword(0x358));
                Assert.Equal(0x1130368u, main.ReadDword(0x418));
                overlapThree = true;
                Mark("effect227-three-current-programmes-overlap", current, cold);
            }
        }
        if (oldAge == 280)
        {
            Assert.NotNull(vibrationHandle);
            Assert.False(current.NativeVibrationProcess!.IsPlaying(vibrationHandle!.Value));
            vibrationStopped = true;
            Mark("effect227-native-vibration-stop280", current, cold);
        }
        if (arena != 0)
        {
            var morph = current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(arena - 0x20DFAB8 + MorphOffset)), 8);
            morphData ??= morph.Data;
            morphKnown ??= morph.Known;
            Assert.Equal(morphData, morph.Data);
            Assert.Equal(morphKnown, morph.Known);
            if (main is not null)
            {
                uint free = state.ReadDword(RecordCursorGlobal);
                Assert.InRange(free, arena + RecordOffset, arena + RecordOffset + (RecordCount - 1) * RecordStride);
                Assert.Equal(0u, (free - arena - RecordOffset) % RecordStride);
                int live = 0;
                for (int index = 0; index < RecordCount; index++)
                {
                    int offset = checked((int)(arena - 0x20DFAB8 + RecordOffset + index * RecordStride));
                    if (current.NativeEffectMemory.Arena.Read(offset + 15, 1)[0] == 0)
                    {
                        continue;
                    }
                    live++;
                    Assert.Equal(0x112D2E0u, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeEffectMemory.Arena.Read(offset, 4)));
                    Assert.Equal((short)512, BinaryPrimitives.ReadInt16LittleEndian(current.NativeEffectMemory.Arena.Read(offset + 10, 2)));
                }
                if (live > 0)
                {
                    Mark("effect227-current-sprite-records", current, cold);
                }
            }
        }
        if (oldAge == 284 && main is null)
        {
            Assert.Equal(4, graphics.Models[3].Flags0 & 4);
            terminalFlag = true;
            mainRetired = true;
            NativeBattleEffectTaskPool? previousParticles = prior.NativeGraphics!.OriginalEffect!.Pools.FirstOrDefault(pool => pool.DescriptorAddress == ParticleDescriptor);
            if (previousParticles is not null)
            {
                NativeBattleEffectArena bank = NativeBattleEffectTaskMemory.CaptureBank(previousParticles);
                int length = previousParticles.Stride * previousParticles.Slots.Length;
                var expected = bank.ReadSnapshot(0, length);
                var detached = current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(previousParticles.BankAddress - 0x20DFAB8)), length);
                Assert.Equal(expected.Data, detached.Data);
                Assert.Equal(expected.Known, detached.Known);
            }
            Mark("effect227-main-only-retirement-retains-detached-particle-bank", current, cold);
        }
        ObserveTexts(current, oldAge, cold);
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        uint cursor = state.ReadDword(CursorGlobal);
        uint end = state.ReadDword(SecondCursorGlobal);
        if (arena != 0 && cursor >= arena + FirstPacketOffset)
        {
            uint start = end == arena + ThirdPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
            Assert.InRange(cursor, start, end);
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            CurrentMeshCommands = CurrentCommands.Where(command => command.Opcode is >= 0x20 and <= 0x3F).ToArray();
            if (CurrentCommands.Length > 0)
            {
                packetBanks.Add(start == arena + FirstPacketOffset ? 0 : 1);
            }
            if (CurrentMeshCommands.Any(command => MatchesSprite(command, resources.Sprite(0x112D2E0))))
            {
                sprite = true;
                Mark("effect227-current-original-sprite-RGB-material-SXY-SZ", current, cold);
            }
            if (previous.Values.Any(task => task.Callback == ParticleCallback) && CurrentMeshCommands.Any(command => MatchesParticle(command, resources.ReadMesh(0x112DAA8))))
            {
                particles = true;
                Mark("effect227-current-static-particles", current, cold);
            }
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Cursor = cursor, End = end, Commands = CurrentCommands.Length, Geometry = CurrentMeshCommands.Length, Texts = texts.Order(), VibrationHandle = vibrationHandle, NativeFlags = graphics.Models[3].Flags0, TemporaryByte25 = current.NativeFileStaging!.Read(0x1A7725D, 1)[0], Contacts, ContactGlyphs });
    }

    private void ObserveTexts(BattleSnapshot current, int oldAge, Action<string, BattleSnapshot> cold)
    {
        string actorId = current.NativeGraphics!.ActorIds[3]!;
        NativeBattleGraphicsResource original = current.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == actorId).Resource;
        foreach ((int index, int age, int duration) in new[] { (0, 38, 42), (1, 85, 30), (2, 115, 30), (3, 165, 15) })
        {
            string text = original.ReadText91(index);
            if (oldAge == age)
            {
                Assert.Contains(current.NativeGraphics.Descriptors.Packets, packet => packet.Message is { Kind: "NativeText" } message && message.ActorId == actorId && message.Message == text && packet.Index == duration && packet.Value == 3 && (packet.PriorityState & 0xF0) == 240);
                Mark("effect227-native-text-" + index + "-queued-age-" + age, current, cold);
            }
            if (current.Events.Any(message => message.Kind == "NativeText" && message.ActorId == actorId && message.Message == text && message.Amount == duration && message.NativeMessageMode == 3) && texts.Add(index))
            {
                Mark("effect227-original-actor-text-" + index + "-published", current, cold);
            }
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(registered);
        Assert.True(cameraTrack);
        Assert.True(cameraViewChanged);
        Assert.True(sprite);
        Assert.True(particles);
        Assert.True(overlapTwo);
        Assert.True(overlapThree);
        Assert.True(mainRetired);
        Assert.True(entryFlag);
        Assert.True(terminalFlag);
        Assert.True(vibrationStarted);
        Assert.True(vibrationStopped);
        Assert.Equal(3, programmes.Count);
        Assert.Equal(4, texts.Count);
        Assert.Equal(2, packetBanks.Count);
        Assert.Equal(0, Contacts);
        Assert.Equal(0, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect227!.FixedBanks.Values, bank => Assert.NotNull(bank));
        File.WriteAllText(Path.Combine(output, "effect227-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = packetBanks.Order(), Programmes = programmes.Order(), Texts = texts.Order(), Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, Sprite = sprite, Particles = particles, OverlapTwo = overlapTwo, OverlapThree = overlapThree, MorphPointerOnly = morphData is not null, MainRetired = mainRetired, EntryFlag = entryFlag, TerminalFlag = terminalFlag, VibrationStarted = vibrationStarted, VibrationStopped = vibrationStopped, VibrationHandle = vibrationHandle, Contacts, ContactGlyphs, Observations = observations }));
    }

    private static bool MatchesSprite(NativeBattleLogicalCommand command, NativeBattleModuleResource source)
    {
        if (command.Words.Length != 9 || command.Opcode is not (0x2C or 0x2E) || command.OrderingBucket != 19)
        {
            return false;
        }
        int frames = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(8, 2));
        for (int frame = 0; frame < frames; frame++)
        {
            int offset = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan(10 + frame * 2, 2));
            ReadOnlySpan<byte> quad = source.Bytes.AsSpan(offset + 4, 20);
            byte right = unchecked((byte)Math.Min(quad[4] + quad[0], 255));
            byte bottom = unchecked((byte)Math.Min(quad[5] + quad[2], 255));
            uint color = (uint)quad[3] << 24 | (uint)quad[1] * 0x010101;
            uint uv0 = BinaryPrimitives.ReadUInt32LittleEndian(quad.Slice(4, 4));
            uint uv1 = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14, 2)) & 0x1FF) << 16 | (uint)quad[5] << 8 | right;
            if (command.Words[0] == color && command.Words[2] == uv0 && command.Words[4] == uv1 && command.Words[6] == ((uint)bottom << 8 | quad[4]) && command.Words[8] == ((uint)bottom << 8 | right))
            {
                Assert.Equal(4, command.VertexPayload4.Length);
                Assert.All(command.VertexPayload4, depth => Assert.InRange(depth, 0, 65535));
                return true;
            }
        }
        return false;
    }

    private static bool MatchesParticle(NativeBattleLogicalCommand command, NativeBattleStaticMeshResource source)
    {
        bool gouraud = command.Opcode is 0x3C or 0x3E;
        if (!gouraud && command.Opcode is not (0x2C or 0x2E))
        {
            return false;
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan());
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(cursor, 4));
            cursor += 4;
            if (group == (gouraud ? 7 : 3))
            {
                for (int face = 0; face < count; face++)
                {
                    int row = cursor + face * strides[group];
                    uint Word(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(row + offset, 4));
                    uint final = Word(20);
                    uint[] uv = [Word(12), Word(16), final & 0xFFFF, final >> 16];
                    int[] positions = gouraud ? [2, 5, 8, 11] : [2, 4, 6, 8];
                    if (command.Words.Length > positions[3] && (command.Words[0] & 0xFC000000) == (Word(0) & 0xFC000000) && Enumerable.Range(0, 4).All(index => command.Words[positions[index]] == uv[index]))
                    {
                        return true;
                    }
                }
            }
            cursor += count * strides[group];
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
