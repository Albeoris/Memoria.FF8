using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect316PresentationObserver
{
    private readonly HashSet<int> ages = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<string> boundaries = [];
    private readonly List<object> observations = [];
    private readonly HashSet<int> textureVariants = [];
    private bool uploaded;
    private bool markersCleared;
    private bool firstGeometry;
    private bool secondGeometry;
    private bool cameraRegistered;
    private bool cameraTrack;
    private bool cameraView;
    private bool declinedCamera;
    private bool? cameraDecision;
    private bool terminal;
    private bool music;
    private bool vibration;
    private bool stoppedVibration;
    private int births;
    private int recordDraws;
    private int retirements;
    private int localRandomDraws;
    private int firstFrames;
    private int secondFrames;
    private uint? originalColor;
    private int? vibrationHandle;
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return;
        }
        NativeBattleEffect316State state = current.NativeEffectMemory!.Effect316!;
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect316Resources resources = effect.Binding.Effect316Resources!;
        VerifyCurrentTextureCopy(prior, current);
        NativeBattleModulePoolTask? main = ReadMain(current);
        NativeBattleModulePoolTask? before = ReadMain(prior);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? (main is not null ? 0 : -1);
        if (oldAge >= 0)
        {
            Assert.True(ages.Add(oldAge), "A native main age executed twice.");
            Assert.InRange(oldAge, 0, 312);
        }
        if (main is not null)
        {
            originalColor ??= main.ReadDword(0x14);
            vibrationHandle ??= unchecked((int)main.ReadDword(0x10));
            int born = 0;
            int live = 0;
            for (int index = 0; index < RecordCapacity; index++)
            {
                int offset = RecordOffset + index * RecordStride;
                int life = main.ReadWord(offset + 6);
                int previousLife = before?.ReadWord(offset + 6) ?? 0;
                Assert.InRange(life, 0, 10);
                if (life == 10)
                {
                    Assert.Equal(0, previousLife);
                    born++;
                    Assert.InRange(main.ReadWord(offset + 8), (ushort)1024, (ushort)1031);
                    Assert.InRange(unchecked((sbyte)main.ReadByte(offset + 12)), (sbyte)-2, (sbyte)1);
                    Assert.InRange(unchecked((sbyte)main.ReadByte(offset + 13)), (sbyte)-18, (sbyte)-3);
                    Assert.InRange(unchecked((sbyte)main.ReadByte(offset + 14)), (sbyte)-2, (sbyte)1);
                }
                else if (previousLife > 0)
                {
                    Assert.Equal(previousLife - 1, life);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = unchecked((sbyte)before!.ReadByte(offset + 12 + axis));
                        Assert.Equal(unchecked((ushort)(before!.ReadWord(offset + 2 * axis) + (velocity << 2))), main.ReadWord(offset + 2 * axis));
                    }
                    if (life == 0)
                    {
                        retirements++;
                    }
                }
                if (life > 0)
                {
                    live++;
                }
                if (life > 0 || previousLife > 0)
                {
                    recordDraws++;
                }
            }
            births += born;
            uint enteringRandom = prior.NativeEffectMemory!.Effect316!.ReadDword(RandomGlobal);
            uint expected = enteringRandom;
            uint actual = state.ReadDword(RandomGlobal);
            int draws = 0;
            while (expected != actual && draws <= 1661 + 4 * 128)
            {
                expected = unchecked((125 * expected + 14) & 0x7FFF);
                draws++;
            }
            Assert.Equal(actual, expected);
            Assert.InRange(draws - 4 * born, 0, 1661);
            if (oldAge is < 150 or >= 300)
            {
                Assert.Equal(0, draws);
                Assert.Equal(0, born);
            }
            localRandomDraws += draws;
            Assert.Equal(unchecked((uint)NativeBattleEffect316Controller.Amplitude((short)oldAge)), state.ReadDword(AmplitudeGlobal));
            observations.Add(new { current.Frame, graphics.Callback, MainAge = age, ProcessedAge = oldAge, Births = born, Live = live, EnteringRandom = enteringRandom, CurrentRandom = actual, LocalDraws = draws, AcceptedMixedFaces = draws - 4 * born, Hint = main.ReadDword(RecordHintOffset) });
        }
        if (oldAge is >= 20 and < 150)
        {
            firstFrames++;
        }
        if (oldAge is >= 150 and < 300)
        {
            secondFrames++;
        }
        if (oldAge == 312)
        {
            terminal = true;
            Assert.Null(main);
            Assert.Equal(originalColor, graphics.Models[3].Color44);
            Assert.Equal(0, graphics.Models[3].Flags0 & 8);
            Assert.All(graphics.Models.Take(3), model => Assert.Equal(0, model.Flags0 & 4));
        }
        if (oldAge >= 60 && oldAge < 312)
        {
            Assert.NotEqual(0, graphics.Models[3].Flags0 & 8);
        }
        if (main is not null)
        {
            Assert.All(graphics.Models.Take(3), model => Assert.NotEqual(0, model.Flags0 & 4));
        }
        if (oldAge == 0)
        {
            NativeMusicPlaybackState expected = prior.NativeGlobalSoundProcess!.MusicPlayback!;
            if ((BattleSession.Restore(current).ResolvedNativeFlags & 2) == 0)
            {
                ushort channel = current.NativeMusicProcess!.ReturnChannel!.Value;
                expected = expected.FadeGain46BBC0(unchecked((uint)(short)channel), 240, 0);
            }
            Assert.Equal(expected.Gain0, current.NativeGlobalSoundProcess!.MusicPlayback!.Gain0);
            Assert.Equal(expected.Gain1, current.NativeGlobalSoundProcess.MusicPlayback.Gain1);
            music = true;
            VerifyMarkers(current);
            markersCleared = true;
        }
        if (!uploaded && main is not null && current.NativeEffectMemory.Textures!.Count == 0)
        {
            VerifyTim(resources.MainTim.Data, current.NativeEffectMemory.Textures);
            VerifyTim(resources.EmbeddedTim, current.NativeEffectMemory.Textures);
            uploaded = true;
        }
        if (vibrationHandle is int handle && current.NativeVibrationProcess is { } process)
        {
            vibration |= handle != 0;
            if (oldAge == 308)
            {
                Assert.False(process.IsPlaying(handle));
                stoppedVibration = true;
            }
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            cameraRegistered = true;
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            bool track = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            cameraTrack |= track;
            cameraView |= track && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
            {
                cameraDecision = decision;
                if (!decision)
                {
                    Assert.False(track);
                    declinedCamera |= current.NativeCamera.ControlCursor is null && current.NativeCamera.Order.IsEmpty;
                }
            }
        }
        if (effect.ReturnedDescriptor > 0)
        {
            uint work = state.ReadDword(WorkGlobal);
            uint cursor = state.ReadDword(CursorGlobal);
            NativeBattleModulePoolTask? root = Tasks(current).Values.FirstOrDefault(task => task.Callback == RootCallback);
            if (root is not null)
            {
                int side = root.ReadDword(0x10) == 0 ? 0 : 1;
                uint start = checked(work + (uint)(side == 0 ? FirstPacketOffset : SecondPacketOffset));
                if (cursor >= start)
                {
                    banks.Add(side);
                    CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                    bool geometry = CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length > 0);
                    firstGeometry |= oldAge is >= 20 and < 150 && geometry;
                    secondGeometry |= oldAge is >= 150 and < 300 && geometry;
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
            }
        }
        if (age is 1 or 20 or 21 or 45 or 46 or 60 or 120 or 149 or 150 or 151 or 289 or 290 or 299 or 300 or 308 or 309 or 312)
        {
            Mark("before-main-age-" + age, current, cold);
        }
        if (terminal)
        {
            Mark("native-terminal", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.Equal(Enumerable.Range(0, 313), ages.Order());
        Assert.Equal(130, firstFrames);
        Assert.Equal(150, secondFrames);
        Assert.True(firstGeometry);
        Assert.True(secondGeometry);
        Assert.True(cameraRegistered);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(cameraTrack);
            Assert.True(cameraView);
        }
        else
        {
            Assert.True(declinedCamera);
        }
        Assert.True(terminal);
        Assert.True(music);
        Assert.True(vibration);
        Assert.True(stoppedVibration);
        Assert.True(uploaded);
        Assert.True(markersCleared);
        Assert.Equal(Enumerable.Range(0, 11), textureVariants.Order());
        Assert.True(births > 0);
        Assert.Equal(births, retirements);
        Assert.Equal(births * 11, recordDraws);
        Assert.Equal(2, banks.Count);
        Assert.Contains(completed.Events, item => item.NativeModuleSound?.Sound == completed.NativeGraphics!.OriginalEffect!.Binding.Effect316Resources!.Sound);
        Assert.All(completed.NativeEffectMemory!.Effect316!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect316-native-observations.json"), JsonSerializer.Serialize(new { MainAges = ages.Order(), PacketBanks = banks.Order(), FirstFrames = firstFrames, SecondFrames = secondFrames, Births = births, RecordDraws = recordDraws, Retirements = retirements, LocalRandomDraws = localRandomDraws, FirstGeometry = firstGeometry, SecondGeometry = secondGeometry, CameraRegistered = cameraRegistered, CameraDecision = cameraDecision, CameraTrack = cameraTrack, CameraView = cameraView, DeclinedCamera = declinedCamera, Music = music, Vibration = vibration, StoppedVibration = stoppedVibration, BothTimUploads = uploaded, CurrentMarkerWordsCleared = markersCleared, NativeTextureVariants = textureVariants.Order(), Terminal = terminal, Observations = observations }));

    private void VerifyCurrentTextureCopy(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleGraphicsModel model = current.NativeGraphics!.Models[3];
        if (model.Sequence != 15 || model.SequenceState is not { Offset46: 55 } sequence || sequence.Locals[7] is not short nextVariant)
        {
            return;
        }
        int variant = nextVariant - 1;
        if (textureVariants.Contains(variant))
        {
            return;
        }
        Assert.Equal(textureVariants.Count, variant);
        Assert.InRange(variant, 0, 10);
        NativeBattleModelCpuResources cpu = current.NativeModelCpuResources!;
        NativeBattleModelCpuResourceRow row = cpu.Rows[model.ActiveResourceRow100!.Value];
        byte[] source = NativeBattleModelEnemyHeap.Read(cpu, current.NativeFileStaging!, row.Require(48), row.Lengths[48]);
        NativeBattleTextureCopy copy = NativeBattleTextureMotion.Plan50C780(source.ToImmutableArray(), model.TextureAllocationMask, 1, variant)!;
        NativeBattleEffectArena previousVram = new(prior.NativeEffectMemory!.Textures!.VramRegions);
        NativeBattleEffectArena currentVram = new(current.NativeEffectMemory!.Textures!.VramRegions);
        for (int y = 0; y < copy.Height; y++)
        {
            int from = checked(((copy.SourceY + y) * 1024 + copy.SourceX) * 2);
            int to = checked(((copy.DestinationY + y) * 1024 + copy.DestinationX) * 2);
            Assert.Equal(previousVram.Read(from, copy.Width * 2), currentVram.Read(to, copy.Width * 2));
        }
        textureVariants.Add(variant);
    }

    private static void VerifyTim(ImmutableArray<byte> data, NativeBattleEffectTextureState textures)
    {
        NativeBattleEffectArena vram = new(textures.VramRegions);
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 4, 2));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 6, 2));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8, 2));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10, 2));
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.AsSpan(offset + 12 + row * width * 2, width * 2).ToArray(), vram.Read(((y + row) * 1024 + x) * 2, width * 2));
            }
            offset += size;
        }
    }

    private static void VerifyMarkers(BattleSnapshot snapshot)
    {
        NativeBattleModelCpuResources cpu = snapshot.NativeModelCpuResources!;
        NativeBattleModelCpuResourceRow row = cpu.Rows[snapshot.NativeGraphics!.Models[3].ActiveResourceRow100!.Value];
        byte[] geometry = NativeBattleModelEnemyHeap.Read(cpu, snapshot.NativeFileStaging!, row.Require(16), row.Lengths[16]);
        int markers = 0;
        int objects = BinaryPrimitives.ReadInt32LittleEndian(geometry);
        for (int item = 0; item < objects; item++)
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan(4 + 4 * item, 4));
            int groups = BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan(cursor, 2));
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                cursor += 4 + 6 * BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan(cursor + 2, 2));
            }
            cursor = (cursor + 3) & ~3;
            int triangles = BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan(cursor, 2));
            int quads = BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan(cursor + 2, 2));
            cursor += 12;
            for (int kind = 0; kind < 2; kind++)
            {
                int count = kind == 0 ? triangles : quads;
                int stride = kind == 0 ? 16 : 20;
                for (int face = 0; face < count; face++)
                {
                    Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan(cursor + face * stride + 14, 2)) & 0x8000);
                    markers++;
                }
                cursor += count * stride;
            }
        }
        Assert.Equal(1661, markers);
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (boundaries.Add(name))
        {
            cold(name, snapshot);
        }
    }

    internal static NativeBattleModulePoolTask? ReadMain(BattleSnapshot snapshot) => Tasks(snapshot).Values.FirstOrDefault(task => task.Callback == MainCallback);

    internal static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
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
                result.Add(checked(pool.BankAddress + (uint)(slot * pool.Stride)), pool.Slots[slot]);
                index = pool.Slots[slot].Next;
            }
        }
        return result;
    }
}
