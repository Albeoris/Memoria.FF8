using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect54PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<uint> emitters = [];
    private readonly HashSet<uint> rows = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> spriteSources = [];
    private readonly List<object> observations = [];
    private bool bound;
    private bool relocated;
    private bool copiedView;
    private bool camera;
    private bool cameraTrack;
    private bool cameraView;
    private bool uploaded;
    private bool geometry;
    private bool drained;
    private bool modelRetired;
    private int maximumEmitters;
    private int maximumRows;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect54State state = current.NativeEffectMemory!.Effect54!;
        NativeBattleEffect54Resources resources = effect.Binding.Effect54Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> before = Tasks(prior);
        callbacks.UnionWith(tasks.Values.Select(task => task.Callback));
        Assert.Equal((byte)72, graphics.Models[3].ModelId4);
        Assert.Equal(26, graphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
        if (before.Values.Any(task => task.Callback == RootCallback) && tasks.Values.Any(task => task.Callback == RootCallback))
        {
            Assert.Equal(0x2793E58u, state.ReadDword(ViewPointerGlobal));
            Assert.Equal(JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()), JsonSerializer.Serialize(current.NativeEffectMemory.SharedEffectView2793E58));
            copiedView = true;
        }
        if (state.ResourceSourcePending == false)
        {
            bound = true;
            NativeBattleFileStaging staging = current.NativeFileStaging!;
            uint flag = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadResource(staging, SourceAddress, 4));
            if (flag == 1)
            {
                foreach (uint address in NativeBattleEffect54Resources.MutableDwordAddresses.Where(address => address != SourceAddress))
                {
                    uint expected = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(resources.OriginalMutableDword(address)) + SourceAddress);
                    Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4)));
                }
                relocated = true;
                Mark("effect54-current-69-cell-relocation", current, cold);
            }
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(40, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool track = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[40..]));
            cameraTrack |= track;
            cameraView |= track && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect54-current-camera8-40", current, cold);
        }
        if (!uploaded && effect.ReturnedDescriptor > 0)
        {
            VerifyTim(resources.MainTim.Data.AsSpan(), current.NativeEffectMemory.Textures!.ReadVram());
            uploaded = true;
            Mark("effect54-original-tim-upload", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in before.Where(pair => pair.Value.Callback == DriverCallback))
        {
            int phase = task.ReadWord(0x28) >> 8;
            if (phase == 2 && task.ReadWord(0x24) >= 30 && (!tasks.TryGetValue(address, out NativeBattleModulePoolTask? after) || (after.ReadWord(0x28) >> 8) == 3))
            {
                Contacts++;
                Mark("effect54-current-header-relative-contact", current, cold);
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
        NativeBattleModulePoolTask? root = tasks.Values.SingleOrDefault(task => task.Callback == RootCallback);
        int rootPhase = root is null ? -1 : root.ReadWord(0x28) >> 8;
        bool currentRecordsInitialized = rootPhase >= 3;
        uint emitterBase = state.ReadDword(EmitterRecordsGlobal);
        uint drawBase = state.ReadDword(DrawRecordsGlobal);
        int liveEmitters = 0;
        int liveRows = 0;
        if (currentRecordsInitialized)
        {
            Assert.NotEqual(0u, emitterBase);
            Assert.NotEqual(0u, drawBase);
        }
        if (currentRecordsInitialized && emitterBase != 0 && drawBase != 0)
        {
            NativeBattleEffectArena arena = current.NativeEffectMemory.Arena ?? throw new InvalidDataException("Effect54 lost its current COMMON records.");
            for (int index = 0; index < ReachableEmitterCount; index++)
            {
                uint address = emitterBase + (uint)(index * EmitterStride);
                if (arena.Read(checked((int)(address - 0x20DFAB8)), EmitterStride)[0x69] != 0)
                {
                    liveEmitters++;
                    emitters.Add(address);
                }
            }
            for (int index = 0; index < ReachableDrawCount; index++)
            {
                uint address = drawBase + (uint)(index * DrawStride);
                byte[] row = arena.Read(checked((int)(address - 0x20DFAB8)), DrawStride);
                if (row[0x1D7] != 0)
                {
                    liveRows++;
                    rows.Add(address);
                    Assert.InRange(row[0x1D6], 0, 1);
                    Assert.InRange(row[0x1D8], 1, 4);
                    uint source = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(0x170, 4));
                    if (source != 0)
                    {
                        Assert.Contains(source, NativeBattleEffect54Resources.SpriteAddresses);
                        spriteSources.Add(source);
                    }
                }
            }
            maximumEmitters = Math.Max(maximumEmitters, liveEmitters);
            maximumRows = Math.Max(maximumRows, liveRows);
            Assert.InRange(liveEmitters, 0, 13);
            Assert.InRange(liveRows, 0, 39);
            if (rows.Count == 48)
            {
                Mark("effect54-all-13-emitter-48-draw-births", current, cold);
            }
        }
        uint packetBase = state.ReadDword(InitializerGlobal);
        uint cursor = state.ReadDword(PrimaryCursorGlobal);
        if (currentRecordsInitialized && packetBase != 0 && cursor >= packetBase && cursor <= packetBase + 0x10000)
        {
            uint start = cursor >= packetBase + 0x8000 ? packetBase + 0x8000u : packetBase;
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8 && MatchesSprite(command, resources)).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (CurrentCommands.Length > 0)
            {
                geometry = true;
                banks.Add(start == packetBase ? 0 : 1);
                foreach (NativeBattleLogicalCommand command in CurrentCommands)
                {
                    Assert.Equal(4, command.VertexPayload4.Length);
                    Assert.All(command.VertexPayload4, depth => Assert.InRange(depth, 0, 65535));
                    Assert.NotNull(command.OrderingBucket);
                }
                Mark("effect54-current-sprite-geometry-bank-" + (start == packetBase ? 0 : 1), current, cold);
            }
        }
        foreach ((uint _, NativeBattleModulePoolTask task) in tasks.Where(pair => pair.Value.Callback == RootCallback))
        {
            int phase = task.ReadWord(0x28) >> 8;
            if (phase is 2 or 4 or 5 or 6 or 9)
            {
                Mark("effect54-root-phase-" + phase, current, cold);
            }
        }
        if (before.Values.Any(task => task.Callback == ModelCallback) && !tasks.Values.Any(task => task.Callback == ModelCallback))
        {
            modelRetired = true;
            Mark("effect54-model-retired-current-lists-empty", current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, RootPhase = rootPhase, CurrentRecordsInitialized = currentRecordsInitialized, LiveEmitters = liveEmitters, LiveRows = liveRows, Emitters = emitters.Count, Rows = rows.Count, Cursor = cursor, Commands = CurrentCommands.Length, Contacts, ContactGlyphs });
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect54-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Callbacks = callbacks.Order(), EmitterBirths = emitters.Count, DrawBirths = rows.Count, MaximumEmitters = maximumEmitters, MaximumRows = maximumRows, SpriteSources = spriteSources.Order(), Bound = bound, Relocated = relocated, CopiedView = copiedView, Camera = camera, CameraTrack = cameraTrack, CameraView = cameraView, Uploaded = uploaded, Geometry = geometry, ModelRetired = modelRetired, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(bound);
        Assert.True(relocated);
        Assert.True(copiedView);
        Assert.True(camera);
        Assert.True(cameraTrack);
        Assert.True(cameraView);
        Assert.True(uploaded);
        Assert.True(geometry);
        Assert.True(modelRetired);
        Assert.True(drained);
        Assert.Equal(13, emitters.Count);
        Assert.Equal(48, rows.Count);
        Assert.Equal(2, banks.Count);
        Assert.Equal(NativeBattleEffect54Resources.SpriteAddresses.Order(), spriteSources.Order());
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Contains(FadeInCallback, callbacks);
        Assert.Contains(FadeOutCallback, callbacks);
        uint expectedSound = BinaryPrimitives.ReadUInt32LittleEndian(completed.NativeGraphics!.OriginalEffect!.Binding.Effect54Resources!.ReadStatic(SoundAddress, 4));
        Assert.Contains(completed.Events, item => item.NativeModuleSound?.Sound == expectedSound);
        Assert.All(completed.NativeEffectMemory!.Effect54!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static bool MatchesSprite(NativeBattleLogicalCommand command, NativeBattleEffect54Resources resources)
    {
        if (command.Opcode != 0x2E || command.Words.Length != 9 || command.VertexPayload4.Length != 4)
        {
            return false;
        }
        foreach (NativeBattleModuleResource sprite in resources.Sprites.Values)
        {
            int frames = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(8, 2));
            for (int phase = 0; phase < frames; phase++)
            {
                byte[] quads = sprite.ProjectedFrame571C80(phase).Quads.ToArray();
                for (int offset = 0; offset < quads.Length; offset += 20)
                {
                    ReadOnlySpan<byte> quad = quads.AsSpan(offset, 20);
                    ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
                    int palette = unchecked((sbyte)sprite.Bytes[4 + (flags >> 12)]);
                    ushort clut = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad[6..]) + (palette << 6)));
                    int right = Math.Min(quad[0] + quad[4], 255);
                    int bottom = Math.Min(quad[2] + quad[5], 255);
                    uint color = (uint)quad[3] << 24 | (uint)quad[1] * 0x010101;
                    if (command.Words[0] == color && command.Words[2] == (uint)(quad[4] | quad[5] << 8 | clut << 16) && command.Words[4] == (uint)(right | quad[5] << 8 | (flags & 0x1FF) << 16) && (command.Words[6] & 0xFFFF) == (uint)(quad[4] | bottom << 8) && (command.Words[8] & 0xFFFF) == (uint)(right | bottom << 8))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static void VerifyTim(ReadOnlySpan<byte> data, byte[] vram)
    {
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 4)..]);
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 6)..]);
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 8)..]);
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 10)..]);
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.Slice(offset + 12 + row * width * 2, width * 2).ToArray(), vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2).ToArray());
            }
            offset += length;
        }
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
