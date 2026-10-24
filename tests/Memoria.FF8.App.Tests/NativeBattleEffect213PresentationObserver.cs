using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect213PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<uint> programmes = [];
    private readonly List<object> observations = [];
    private bool registered;
    private bool cameraTrack;
    private bool cameraViewChanged;
    private bool streamChanged;
    private bool sourceCopy;
    private bool morph;
    private bool streamGeometry;
    private bool sortedContacts;
    private bool mainRetired;
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
        NativeBattleEffect213State state = current.NativeEffectMemory!.Effect213!;
        NativeBattleEffect213Resources resources = effect.Binding.Effect213Resources!;
        state.Validate();
        Assert.False(state.MutableStreamPending);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        uint arena = state.ReadDword(ArenaGlobal);
        Assert.Equal((byte)147, graphics.Models[3].ModelId4);
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
        NativeBattleEffectAnimationRegisters? animation = current.NativeEffectMemory.Textures?.Animation;
        if (animation?.Source.Identity == resources.Camera.Identity && current.NativeCamera?.ControlResource?.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, animation.StreamOffset);
            Assert.Equal(32, animation.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(animation), JsonSerializer.Serialize(current.NativeCamera.ControlResource));
            registered = true;
            cameraTrack |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[32..]));
            cameraViewChanged |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect213-camera-control-and-current-tracks", current, cold);
        }
        if (age is 1 or 8 or 20 or 24 or 25 or 29 or 30 or 33 or 35 or 42 or 64 or 69 or 74 or 75)
        {
            Mark("effect213-before-main-age-" + age, current, cold);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            callbacks.Add(task.Callback);
        }
        if (main is not null)
        {
            int count = main.ReadWord(14);
            Assert.InRange(count, 1, 3);
            Assert.Equal((ushort)0, main.ReadWord(0x20));
            if (!sortedContacts)
            {
                Assert.Equal(1, count);
                Assert.Equal(0x1D972C0u, main.ReadDword(16));
                Assert.Equal((byte)0, main.ReadByte(0x1C));
                Assert.True(main.ReadWord(0x2C) > 0);
                sortedContacts = true;
                Mark("effect213-current-sorted-contact-and-vertex-count", current, cold);
            }
            uint programme = main.ReadDword(0x64);
            if (resources.Programmes.ContainsKey(programme))
            {
                programmes.Add(programme);
                Mark("effect213-current-programme-" + programme.ToString("X8"), current, cold);
            }
            if (age >= 26)
            {
                Assert.True(unchecked((int)main.ReadDword(0x30)) > 0, "Current source and target must produce the actual nonzero distance divisor.");
            }
        }
        if (arena != 0 && effect.Pools.Any(pool => pool.DescriptorAddress == MainDescriptor))
        {
            NativeBattleEffectArena common = current.NativeEffectMemory.Arena!;
            byte[] copied = common.Read(checked((int)(arena - 0x20DFAB8 + StreamCopyOffset)), MutableStreamLength);
            sourceCopy = true;
            byte[] currentVertices = current.NativeFileStaging!.Read(MutableStreamAddress, MutableStreamLength);
            byte[] original = resources.OriginalMutableVertices.ToArray();
            for (int index = 0; index < StreamVertexCount; index++)
            {
                Assert.Equal(original.AsSpan(index * 8 + 6, 2).ToArray(), currentVertices.AsSpan(index * 8 + 6, 2).ToArray());
            }
            if (!currentVertices.SequenceEqual(original))
            {
                streamChanged = true;
                Mark("effect213-current-mutated-stream-retains-W", current, cold);
            }
            if (oldAge is >= 25 and < 75)
            {
                Assert.Contains(copied, value => value != 0);
                var scratch = common.ReadSnapshot(checked((int)(arena - 0x20DFAB8 + MorphOffset)), 512);
                if (Enumerable.Range(0, 64).All(index => scratch.Known.AsSpan(index * 8, 6).ToArray().All(value => value)))
                {
                    morph = true;
                    Mark("effect213-current-morph-XYZ-retained-W", current, cold);
                }
            }
        }
        if (before is not null && oldAge is >= 30 and < 75 && before.ReadWord(0x24) + before.ReadWord(0x26) >= 4096)
        {
            Contacts++;
            Mark("effect213-current-original-contact-index-" + before.ReadByte(0x1C + before.ReadWord(0x20)), current, cold);
        }
        if (before is not null && oldAge == 75 && main is null)
        {
            mainRetired = true;
            Mark("effect213-main-retired-children-drain", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect213-current-contact-glyph", current, cold);
            }
        }
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena != 0 && cursor >= arena + FirstPacketOffset)
        {
            uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
            Assert.InRange(cursor, start, start + 0x10000);
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (CurrentCommands.Length > 0)
            {
                packetBanks.Add(start == arena + FirstPacketOffset ? 0 : 1);
            }
        }
        if (oldAge is >= 25 and < 75)
        {
            CurrentMeshCommands = CurrentCommands.Where(command => MatchesStream(command, resources.SourceStream, oldAge - 25)).ToArray();
            foreach (NativeBattleLogicalCommand command in CurrentMeshCommands)
            {
                Assert.Equal(4, command.VertexPayload4.Length);
                Assert.All(command.VertexPayload4, value => Assert.InRange(value, 0, 65535));
                Assert.NotNull(command.OrderingBucket);
                int step = command.Opcode is 0x3C or 0x3E ? 3 : 2;
                uint[] screens = Enumerable.Range(0, 4).Select(index => command.Words[1 + step * index]).ToArray();
                Assert.Contains(screens, value => unchecked((short)value) is >= 0 and <= 2560);
                Assert.Contains(screens, value => unchecked((short)(value >> 16)) is >= 0 and <= 1728);
            }
            if (CurrentMeshCommands.Length > 0)
            {
                streamGeometry = true;
                Mark("effect213-current-stream-source-UV-RGB-SXY-SZ", current, cold);
            }
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Cursor = cursor, CurrentCommands = CurrentCommands.Length, SourceStreamCommands = CurrentMeshCommands.Length, Contacts, ContactGlyphs, CameraCursor = current.NativeCamera?.ControlCursor, CurrentSourceBound = state.MutableStreamPending == false, Distance = main?.ReadDword(0x30), Programme = main?.ReadDword(0x64) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(registered);
        Assert.True(cameraTrack);
        Assert.True(cameraViewChanged);
        Assert.True(sourceCopy);
        Assert.True(sortedContacts);
        Assert.True(streamChanged);
        Assert.True(morph);
        Assert.True(streamGeometry);
        Assert.True(mainRetired);
        Assert.Equal(2, programmes.Count);
        Assert.Equal(2, packetBanks.Count);
        Assert.Contains(0x6A0760u, callbacks);
        Assert.Contains(0x6A08D0u, callbacks);
        Assert.Contains(0x6A0AF0u, callbacks);
        Assert.Contains(0x6A0BE0u, callbacks);
        Assert.Contains(0x6A0C80u, callbacks);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect213!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.False(completed.NativeEffectMemory.Effect213.MutableStreamPending);
        File.WriteAllText(Path.Combine(output, "effect213-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), Programmes = programmes.Order(), Registered = registered, CameraTrack = cameraTrack, CameraViewChanged = cameraViewChanged, SourceCopy = sourceCopy, SortedContacts = sortedContacts, StreamChanged = streamChanged, Morph = morph, StreamGeometry = streamGeometry, MainRetired = mainRetired, Contacts, ContactGlyphs, Observations = observations }));
    }

    private static bool MatchesStream(NativeBattleLogicalCommand command, NativeBattleStaticMeshResource source, int phase)
    {
        bool gouraud = command.Opcode is 0x3C or 0x3E;
        if (!gouraud && command.Opcode is not (0x2C or 0x2E))
        {
            return false;
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan()) + 12;
        for (int group = 0; group < 2; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(cursor, 4));
            cursor += 4;
            int stride = group == 0 ? 24 : 36;
            if (gouraud == (group == 1))
            {
                for (int face = 0; face < count; face++)
                {
                    int row = cursor + face * stride;
                    uint Word(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(row + offset, 4));
                    uint final = Word(20);
                    uint[] uv = [Word(12), Word(16), final, final >> 16];
                    int[] shifted = uv.Select(value => (byte)value + ((phase & 7) << 4)).ToArray();
                    int adjustment = shifted.Any(value => value > 255) ? -128 : 0;
                    int[] positions = gouraud ? [2, 5, 8, 11] : [2, 4, 6, 8];
                    bool matches = command.Words.Length > positions[3] && (command.Words[0] & 0xFF000000) == (Word(0) & 0xFF000000);
                    for (int corner = 0; corner < 4 && matches; corner++)
                    {
                        uint expected = (uv[corner] & 0xFFFFFF00) | unchecked((byte)(shifted[corner] + adjustment));
                        if (corner >= 2)
                        {
                            expected &= 0xFFFF;
                        }
                        matches &= command.Words[positions[corner]] == expected;
                    }
                    if (matches && phase is >= 8 and <= 42)
                    {
                        matches &= (command.Words[0] & 0xFFFFFF) == (Word(0) & 0xFFFFFF);
                        if (gouraud)
                        {
                            matches &= command.Words[3] == (Word(24) & 0xFFFFFF) && command.Words[6] == (Word(28) & 0xFFFFFF) && command.Words[9] == (Word(32) & 0xFFFFFF);
                        }
                    }
                    if (matches)
                    {
                        return true;
                    }
                }
            }
            cursor += count * stride;
            if (group == 0)
            {
                cursor += 12;
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
