using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect76PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<int> textIndices = [];
    private readonly HashSet<byte> sequences = [];
    private readonly List<object> observations = [];
    private bool music;
    private bool stage;
    private bool audio;
    private bool camera;
    private bool records;
    private bool commonMesh;
    private bool customMesh;
    private bool pcRender;
    private bool cleanup;
    private bool retired;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentFadeCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect76State state = current.NativeEffectMemory!.Effect76!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == DriverCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == DriverCallback);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        int actor = checked((int)state.ReadDword(CasterGlobal));
        Assert.Equal(3, actor);
        Assert.Equal((byte)142, graphics.Models[actor].ModelId4);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal((int)shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks)
        {
            callbacks.Add(task.Callback);
            if (task.Callback == MainCallback && task.ReadWord(12) is 1 or 10 or 38 or 39)
            {
                Mark("effect76-before-startup-age-" + task.ReadWord(12), current, cold);
            }
            if (task.Callback == 0x622560)
            {
                if (task.ReadWord(12) == 1)
                {
                    Mark("effect76-before-common-texture-request", current, cold);
                }
                if (task.ReadWord(12) > 1 && task.ReadWord(14) == 0)
                {
                    Mark("effect76-current-common-texture-word-wait", current, cold);
                }
            }
        }
        if (age is 0 or 40 or 90 or 160 or 180 or 270 or 350 or 430 or 502 or 510 or 515 or 518 or 605 or 662 or 665)
        {
            Mark("effect76-before-main-age-" + age, current, cold);
        }
        foreach ((uint address, NativeBattleGuardianCameraTask task) in state.CameraTasks)
        {
            NativeBattleModulePoolTask slot = tasks[address];
            Assert.Equal(0x63E9C0u, slot.Callback);
            Assert.Equal(task.Frame, slot.ReadWord(12));
            Assert.Equal(CameraAddress + (uint)task.Offset, slot.ReadDword(20));
            Assert.Equal(CapturedWorldAddress, slot.ReadDword(24));
            if (task.Offset > 0)
            {
                camera = true;
                Mark("effect76-paired-camera-current-program", current, cold);
            }
        }
        if (state.Read(MusicCompletionGlobal, 1)[0] != 0 && !music)
        {
            NativeBattleMusicProcessState process = current.NativeMusicProcess!;
            NativeBattleMusicLoadPointers source = process.LoadSource!;
            ImmutableArray<byte> data = effect.Binding.Effect76Resources!.Music.Data;
            NativeBattleMusicLoadSource expected = NativeBattleMusicLoadSource.Read501A20(data);
            Assert.Equal(MusicAddress + (uint)NativeBattleDatDirectory.Section(data.AsSpan(), 0), source.SoundBankAddress);
            Assert.Equal(MusicAddress + (uint)NativeBattleDatDirectory.Section(data.AsSpan(), 1), source.MusicAddress);
            Assert.Equal(expected.Music.Length, source.MusicLength);
            byte buffer = process.Commands?.TargetBufferAddress switch
            {
                0x1CE075C => 0,
                0x1CDC750 => 1,
                _ => throw new InvalidDataException("Effect76 requires its actual music destination buffer.")
            };
            Assert.Equal(expected.ReadIdentity(), process.Buffers.ReadBuffer(buffer));
            music = true;
            Mark("effect76-addressed-music-completion", current, cold);
        }
        if (previous.Values.Any(task => task.Callback == MainCallback && task.ReadWord(12) == 38) && tasks.Values.Any(task => task.Callback == MainCallback && task.ReadWord(12) == 39))
        {
            NativeBattleStageState currentStage = current.NativeStage!;
            Assert.NotNull(currentStage.SourceAddress1D9A820);
            Assert.NotNull(currentStage.ReloadColorSeed1D9A828);
            uint directory = currentStage.SourceAddress1D9A820 ?? throw new InvalidDataException("Effect76 lost its current physical stage directory.");
            Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(current.NativeFileStaging!.Read(directory, 4).AsSpan()));
            Assert.Equal(effect.Binding.Effect76Resources!.ReadFile(StageFileId).Data.AsSpan(StageDirectoryOffset, 32).ToArray(), current.NativeFileStaging!.Read(directory, 32));
            Assert.Equal((short)2, currentStage.RotationSpeed);
            Assert.NotNull(currentStage.MutableGeometry);
            stage = true;
            Mark("effect76-current-stage-index1-replacement", current, cold);
        }
        foreach (BattleEvent item in current.Events.Where(item => item.Frame > prior.Frame))
        {
            if (item.NativeFileAudio is { } request)
            {
                Assert.Equal(0, request.Channel);
                Assert.Equal("field2.fs", request.ResourceName);
                Assert.Equal(127, request.Gain);
                Assert.Equal(0, request.Result);
                Assert.Equal(NativeGlobalSoundBackend.Bufferless, request.Backend);
                audio = true;
                Mark("effect76-file-audio-request", current, cold);
            }
        }
        foreach ((int nativeAge, byte sequence) in new (int, byte)[] { (40, 15), (160, 16), (510, 17) })
        {
            if (oldAge == nativeAge && age == nativeAge + 1)
            {
                Assert.Equal(sequence, graphics.Models[actor].Sequence);
                sequences.Add(sequence);
                Mark("effect76-native-sequence-" + sequence, current, cold);
            }
        }
        ObserveText(current, actor, cold);
        if (oldAge == 605 && age == 606)
        {
            Contacts++;
            Mark("effect76-current-self-contact605", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == actor && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect76-self-contact-glyph", current, cold);
            }
        }
        int firstActive = Enumerable.Range(0, FirstRecordCount).Count(index => state.ReadDword(FirstRecordsAddress + (uint)(index * RecordStride)) != 0);
        int secondActive = Enumerable.Range(0, SecondRecordCount).Count(index => state.ReadDword(SecondRecordsAddress + (uint)(index * RecordStride)) != 0);
        if (firstActive > 0 || secondActive > 0)
        {
            records = true;
            Mark("effect76-current-record-bank-" + (secondActive > 0 ? "200" : "100"), current, cold);
        }
        NativeBattlePcRenderTailReceipt? tail = current.NativeEffectMemory.PcRenderRequest?.PublishedTail;
        if (tail is not null && previous.Values.Any(task => task.Callback is 0x6200F0 or 0x620140))
        {
            tail.Validate();
            pcRender = true;
            Mark("effect76-current-pc-render-tail", current, cold);
        }
        if (previous.Any(pair => pair.Value.Callback == 0x622560 && pair.Value.ReadWord(12) > 1 && !tasks.ContainsKey(pair.Key)))
        {
            cleanup = true;
            Mark("effect76-common-texture-byte-completion-word-poll", current, cold);
        }
        if (oldAge == 665 && main is null)
        {
            NativeBattleCameraState final = current.NativeCamera!;
            Assert.Equal(final.CurrentPose!.Eye, final.CapturedPose!.Eye);
            Assert.Equal(final.CurrentPose.Target, final.CapturedPose.Target);
            Assert.Equal(final.FocalLength, final.CapturedPose.FocalLength);
            Assert.Equal(prior.NativeCamera!.CapturedPose!.Roll, final.CapturedPose.Roll);
            Assert.Equal(prior.NativeCamera.PoseCaptureCount, final.PoseCaptureCount);
            Assert.Equal(final.EyePaddingF6, final.CapturedEyePadding806);
            Assert.Equal(final.TargetPaddingFE, final.CapturedTargetPadding80E);
            retired = true;
            Mark("effect76-selective-camera-retirement", current, cold);
        }
        NativeBattleLogicalCommand[] common = CommonMeshCommands(current, tasks.Values);
        commonMesh |= common.Length > 0;
        customMesh |= common.Any(command => Commands(current).Any(inline => inline.Packet == command.Packet && inline.Opcode == 0xE1 && inline.Words[0] == 0xE1000220));
        if (customMesh)
        {
            Mark("effect76-current-custom-FT4-inline-E1", current, cold);
        }
        CurrentFadeCommands = FadeCommands(current, oldAge);
        CurrentCommands = PrivateCommands(current, state).Concat(common).Concat(CurrentFadeCommands).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.Empty(state.CameraTasks);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            Mark("effect76-retained-native-completion", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, Pools = effect.Pools.Length, CameraTasks = state.CameraTasks.Count, FirstActive = firstActive, SecondActive = secondActive, CommonMeshPackets = common.Length, PrivatePackets = CurrentCommands.Count(command => command.Packet.Bank == "20DFAB8"), FadePackets = CurrentFadeCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(music);
        Assert.True(stage);
        Assert.True(audio);
        Assert.True(camera);
        Assert.True(records);
        Assert.True(commonMesh);
        Assert.True(customMesh);
        Assert.True(pcRender);
        Assert.True(cleanup);
        Assert.True(retired);
        Assert.Equal(new byte[] { 15, 16, 17 }, sequences.Order().ToArray());
        Assert.Equal(new[] { 1, 3, 4 }, textIndices.Order().ToArray());
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Contains("effect76-retained-native-completion", markers);
        File.WriteAllText(Path.Combine(output, "effect76-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), Sequences = sequences.Order(), TextIndices = textIndices.Order(), Music = music, Stage = stage, FileAudio = audio, PairedCamera = camera, Records = records, CommonMesh = commonMesh, CustomMesh = customMesh, PcRender = pcRender, CommonTextureCompletion = cleanup, CameraRetirement = retired, Contacts, ContactGlyphs, completed.Frame, Observations = observations }));
    }

    private void ObserveText(BattleSnapshot current, int actor, Action<string, BattleSnapshot> cold)
    {
        string id = current.NativeGraphics!.ActorIds[actor]!;
        NativeBattleGraphicsBinding binding = current.Definition.NativeGraphics!.Bindings.Single(item => item.ActorId == id);
        foreach (int index in new[] { 1, 3, 4 })
        {
            string expected = binding.Resource.ReadText91(index);
            foreach (NativeBattleDescriptorPacket packet in current.NativeGraphics.Descriptors.Packets.Where(packet => packet.Message is { Kind: "NativeText" } message && message.ActorId == id && message.Message == expected && packet.PriorityState != 0))
            {
                Assert.Equal(240, packet.PriorityState & 0xF0);
                Assert.Equal((short)60, packet.Index);
                Assert.Equal((ushort)3, packet.Value);
                Mark("effect76-original-text-descriptor-" + index, current, cold);
            }
            if (current.Events.Any(item => item.Kind == "NativeText" && item.ActorId == id && item.Message == expected && item.Amount == 60 && item.NativeMessageMode == 3))
            {
                textIndices.Add(index);
                Mark("effect76-original-text-" + index, current, cold);
            }
        }
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect76State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        uint start = cursor > arena + 0xC000 ? arena + 0xC000 : arena;
        Assert.InRange(cursor, start, start + 0xC000);
        int first = checked((int)(start - 0x20DFAB8u));
        int end = checked((int)(cursor - 0x20DFAB8u));
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= first && command.Packet.ByteOffset < end).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private readonly record struct UvFace(uint First, uint Second, ushort Third, ushort Fourth);

    private static NativeBattleLogicalCommand[] CommonMeshCommands(BattleSnapshot current, IEnumerable<NativeBattleModulePoolTask> tasks)
    {
        uint[] sources = tasks.Any(task => task.Callback == 0x6215E0) ? [0xDE2DE0, 0xDE2470] : tasks.Any(task => task.Callback == 0x6201A0) ? [0xDE2DE0] : [];
        HashSet<UvFace> faces = [];
        foreach (uint address in sources)
        {
            ImmutableArray<byte> bytes = current.NativeGraphics!.OriginalEffect!.Binding.Effect76Resources!.ReadMesh(address).Bytes;
            int cursor = checked((int)Dword(bytes.AsSpan(), 0));
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = checked((int)Dword(bytes.AsSpan(), cursor));
                cursor += 4;
                if (group == 3)
                {
                    for (int face = 0; face < count; face++)
                    {
                        int offset = cursor + face * strides[group];
                        faces.Add(new(Dword(bytes.AsSpan(), offset + 12), Dword(bytes.AsSpan(), offset + 16), Word(bytes.AsSpan(), offset + 20), Word(bytes.AsSpan(), offset + 22)));
                    }
                }
                cursor += count * strides[group];
            }
        }
        return Commands(current).Where(command => Common(command) && (command.Opcode is 0x2C or 0x2E) && command.Words.Length == 9 && faces.Contains(new(command.Words[2], command.Words[4], unchecked((ushort)command.Words[6]), unchecked((ushort)command.Words[8])))).ToArray();
    }

    private static NativeBattleLogicalCommand[] FadeCommands(BattleSnapshot current, int oldAge) => oldAge is >= 502 and <= 530 ? Commands(current).Where(command => Common(command) && command.Opcode == 0x62 && command.Words.Length == 3 && command.Words[1] == 0 && command.Words[2] == 0x00D80140).ToArray() : [];
    private static bool Common(NativeBattleLogicalCommand command) => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1;
    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSnapshot snapshot) => snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command);
    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static uint Dword(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));

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
            while (index is int selected)
            {
                Assert.InRange(selected, 0, pool.Slots.Length - 1);
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(checked(pool.BankAddress + (uint)(selected * pool.Stride)), task);
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
