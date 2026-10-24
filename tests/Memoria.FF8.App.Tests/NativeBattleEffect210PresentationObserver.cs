using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect210PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> sides = [];
    private readonly HashSet<uint> sources = [];
    private readonly List<object> observations = [];
    private bool registered;
    private bool ioInitialized;
    private bool cameraControlAdvanced;
    private bool cameraViewChanged;
    private bool earlyDetached;
    private bool overlayInitialized;
    private bool capture;
    private bool echo;
    private bool ribbon;
    private bool act;
    private bool motion;
    private bool retired;
    private short? originalTargetY;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentMeshCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentMeshCommands = [];
        CurrentFadeCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect210State state = current.NativeEffectMemory!.Effect210!;
        NativeBattleEffect210Resources resources = effect.Binding.Effect210Resources!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        KeyValuePair<uint, NativeBattleModulePoolTask>? mainPair = tasks.Where(pair => pair.Value.Callback == MainCallback).Select(pair => (KeyValuePair<uint, NativeBattleModulePoolTask>?)pair).FirstOrDefault();
        NativeBattleModulePoolTask? main = mainPair?.Value;
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        uint arena = state.ReadDword(ArenaGlobal);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            if (pool.DescriptorAddress == RootDescriptor)
            {
                Assert.Equal(RootBank, pool.BankAddress);
                Assert.Null(state.FixedBanks[RootBank]);
                continue;
            }
            NativeBattleCureFamilyPool shape = CommonPools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(arena + shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal(shape.Capacity, pool.Slots.Length);
            Assert.Null(state.CommonQueueStorage[shape.Descriptor]);
        }
        Assert.False(effect.Pools.Any(pool => pool.DescriptorAddress == EarlyDescriptor) && effect.Pools.Any(pool => pool.DescriptorAddress == ActDescriptor));
        NativeBattleEffectAnimationRegisters? animation = current.NativeEffectMemory.Textures?.Animation;
        if (animation?.Source.Identity == resources.Camera.Identity && current.NativeCamera?.ControlResource?.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, animation.StreamOffset);
            Assert.Equal(24, animation.ModelTableOffset);
            Assert.Equal(animation.Source.Data.ToArray(), current.NativeCamera.ControlResource!.Source.Data.ToArray());
            registered = true;
            bool runningCamera = current.NativeCamera.Variables[0] == 1 && current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            cameraControlAdvanced |= runningCamera;
            if (runningCamera)
            {
                cameraViewChanged |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            }
            Mark("effect210-registered-camera-and-model-control", current, cold);
        }
        if (age is 0 or 33 or 35 or 54 or 55 or 64 or 80 or 85 or 115 or 195 or 235 or 243 or 307)
        {
            Mark("effect210-before-main-age-" + age, current, cold);
        }
        NativeBattleEffectTaskPool? oldEarly = prior.NativeGraphics?.OriginalEffect?.Pools.FirstOrDefault(pool => pool.DescriptorAddress == EarlyDescriptor);
        if (oldEarly is not null && !effect.Pools.Any(pool => pool.DescriptorAddress == EarlyDescriptor))
        {
            NativeBattleEffectArena priorBank = NativeBattleEffectTaskMemory.CaptureBank(oldEarly);
            int offset = checked((int)(oldEarly.BankAddress - 0x20DFAB8));
            int length = oldEarly.Stride * oldEarly.Slots.Length;
            Assert.Equal(priorBank.ReadSnapshot(0, length).Data, current.NativeEffectMemory.Arena!.ReadSnapshot(offset, length).Data);
            Assert.Equal(priorBank.ReadSnapshot(0, length).Known, current.NativeEffectMemory.Arena.ReadSnapshot(offset, length).Known);
            Assert.NotNull(state.CommonQueueStorage[EarlyDescriptor]);
            earlyDetached = true;
            Mark("effect210-detached-early-bank-retains-orphan-bytes", current, cold);
        }
        if (effect.Pools.Any(pool => pool.DescriptorAddress == ActDescriptor))
        {
            Assert.True(earlyDetached);
            overlayInitialized = true;
            Mark("effect210-ACT-overlay-current-bank-owner", current, cold);
        }
        if (arena != 0 && current.NativeEffectMemory.EffectIo is { } io && io.ControllerOffset == checked((int)(arena - 0x20DFAB8 + 0xA708)) && io.BaseOffset is not null)
        {
            ioInitialized = true;
        }
        uint cursor = state.ReadDword(CursorGlobal);
        uint end = state.ReadDword(SecondCursorGlobal);
        if (arena != 0 && end >= arena + 0x1B1B8)
        {
            uint start = end - 0x10000;
            Assert.True(start == arena + 0xB1B8 || start == arena + 0x1B1B8);
            Assert.True(cursor >= start);
            sides.Add(start == arena + 0xB1B8 ? 0 : 1);
            CurrentCommands = current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        }
        Dictionary<(ushort Clut, ushort Page), HashSet<uint>> pairs = TexturePairs(resources);
        CurrentMeshCommands = CurrentCommands.Where(command => MatchesSource(command, pairs)).ToArray();
        foreach (NativeBattleLogicalCommand command in CurrentMeshCommands)
        {
            int pageIndex = (command.Opcode & 0x10) != 0 ? 5 : 4;
            sources.UnionWith(pairs[((ushort)(command.Words[2] >> 16), (ushort)(command.Words[pageIndex] >> 16))]);
        }
        if (CurrentMeshCommands.Length != 0)
        {
            Mark("effect210-actual-private-source-geometry", current, cold);
        }
        NativeBattleLogicalCommand[] strips = CurrentCommands.Where(command => command.Opcode == 0x3A && command.Words.Length == 8 && command.Words[0] == 0x3A185820 && command.VertexPayload4.Length == 4).ToArray();
        if (strips.Length != 0 && (previous.Values.Any(task => task.Callback == 0x6F82D0) || tasks.Values.Any(task => task.Callback == 0x6F82D0)))
        {
            ribbon = true;
            Mark("effect210-current-companion-ribbon", current, cold);
        }
        if (mainPair is { } currentMain)
        {
            uint workspaceSource = currentMain.Value.ReadDword(0x168);
            if ((oldAge is >= 64 and < 79 or >= 115 and < 235) && resources.Programmes.ContainsKey(workspaceSource) && CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F))
            {
                act = true;
                Mark("effect210-current-ACT-workspace-and-packets", current, cold);
            }
            if ((oldAge is >= 55 and < 62 or >= 72 and < 100) && currentMain.Value.ReadByte(0x61) > 0 && CurrentCommands.Any(command => command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E))
            {
                echo = true;
                Assert.NotNull(current.NativeGraphics.Models[4].CurrentBodyBoneMatrices);
                Mark("effect210-current-original-companion-echo", current, cold);
            }
        }
        if (oldAge == 85)
        {
            ObserveSoftwareCapture(prior, current);
            capture = true;
            Mark("effect210-private-current-stage-capture", current, cold);
        }
        originalTargetY ??= prior.NativeGraphics!.Models[0].Transform!.Position28.Y;
        if (current.NativeGraphics.Models[0].Transform!.Position28.Y != originalTargetY)
        {
            motion = true;
            Mark("effect210-native-current-target-motion", current, cold);
        }
        if (oldAge == 243 && age != 243)
        {
            Contacts++;
            Mark("effect210-all-current-contacts", current, cold);
        }
        for (int index = 0; index < current.NativeGraphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = current.NativeGraphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect210-current-contact-glyph", current, cold);
            }
        }
        if (before is not null && main is null)
        {
            retired = true;
            Mark("effect210-main-retired", current, cold);
        }
        if (oldAge is >= 227 and < 243)
        {
            CurrentFadeCommands = CurrentCommands;
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, Age = age, PreviousAge = oldAge, Arena = arena, Cursor = cursor, End = end, PrivateCommands = CurrentCommands.Length, SourceCommands = CurrentMeshCommands.Length, Contacts, ContactGlyphs, CameraCursor = current.NativeCamera?.ControlCursor, Pools = effect.Pools.Select(pool => new { pool.DescriptorAddress, pool.BankAddress, pool.Stride, Slots = pool.Slots.Length, pool.Head, pool.Tail }) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        File.WriteAllText(Path.Combine(output, "effect210-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Registered = registered, IoInitialized = ioInitialized, CameraControlAdvanced = cameraControlAdvanced, CameraViewChanged = cameraViewChanged, EarlyDetached = earlyDetached, OverlayInitialized = overlayInitialized, Capture = capture, Echo = echo, Ribbon = ribbon, Act = act, Motion = motion, Retired = retired, CursorSides = sides.Order(), SourceMeshes = sources.Order(), Contacts, ContactGlyphs, Observations = observations }));
        Assert.True(registered);
        Assert.True(ioInitialized);
        Assert.True(cameraControlAdvanced);
        Assert.True(cameraViewChanged);
        Assert.True(earlyDetached);
        Assert.True(overlayInitialized);
        Assert.True(capture);
        Assert.True(echo);
        Assert.True(ribbon);
        Assert.True(act);
        Assert.True(motion);
        Assert.True(retired);
        Assert.Equal(2, sides.Count);
        Assert.NotEmpty(sources);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect210!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(completed.NativeEffectMemory.Effect210.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
    }

    private void ObserveSoftwareCapture(BattleSnapshot prior, BattleSnapshot current)
    {
        NativeBattleLogicalCommand area = Assert.Single(CurrentCommands, command => command.Opcode == 0xE3 && command.Words[0] == 0xE30402C0);
        NativeBattleLogicalCommand areaEnd = Assert.Single(CurrentCommands, command => command.Packet == area.Packet && command.Opcode == 0xE4);
        Assert.Equal(0xE407FF7Fu, areaEnd.Words[0]);
        NativeBattleLogicalCommand offset = Assert.Single(CurrentCommands, command => command.Packet.Bank == area.Packet.Bank && command.Packet.Buffer == area.Packet.Buffer && command.Packet.ByteOffset == area.Packet.ByteOffset + 12 && command.Opcode == 0xE5);
        Assert.Equal(0xE50802C0u, offset.Words[0]);
        int blackOffset = checked(area.Packet.ByteOffset - 24);
        NativeBattleEffectArena arena = current.NativeEffectMemory!.Arena!;
        byte[] black = arena.Read(blackOffset, 24);
        Assert.Equal(0x05000000u, BinaryPrimitives.ReadUInt32LittleEndian(black) & 0xFF000000u);
        uint[] payload = Enumerable.Range(1, 5).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(black.AsSpan(index * 4))).ToArray();
        Assert.Equal(new uint[] { 0x28000000, 0, 192, 256u << 16, 192u | (256u << 16) }, payload);
        NativeBattlePrimitiveFrame primitiveFrame = current.NativeGraphics!.PrimitiveFrame!;
        NativeBattleLogicalOrdering ordering = primitiveFrame.Pending;
        int blackProxy = Assert.Single(Enumerable.Range(0, ordering.Proxies.Length), index => ordering.Proxies[index].Packet == new NativeBattlePrimitiveReference(area.Packet.Bank, area.Packet.Buffer, blackOffset));
        Assert.Equal((ushort)1, ordering.Proxies[blackProxy].Flags20);
        int areaProxy = Assert.Single(Enumerable.Range(0, ordering.Proxies.Length), index => ordering.Proxies[index].Packet == area.Packet);
        uint areaLink = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(area.Packet.ByteOffset, 4)) & 0xFFFFFF;
        uint offsetLink = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset.Packet.ByteOffset, 4)) & 0xFFFFFF;
        Assert.Equal((0x1C48828u + checked((uint)blackProxy * 24)) & 0xFFFFFF, areaLink);
        Assert.Equal((0x1C48828u + checked((uint)areaProxy * 24)) & 0xFFFFFF, offsetLink);
        NativeBattleLogicalCommand[] restore = CurrentCommands.Where(command => command.Packet.Bank == area.Packet.Bank && command.Packet.Buffer == area.Packet.Buffer && command.Packet.ByteOffset == blackOffset - 64).ToArray();
        Assert.Contains(restore, command => command.Opcode == 0xE3);
        Assert.Contains(restore, command => command.Opcode == 0xE4);
        Assert.Contains(restore, command => command.Opcode == 0xE5);
        NativeBattleGraphicsDefinition definition = current.Definition.NativeGraphics!;
        Assert.Equal(0x4668F0u, definition.SoftwarePrimitiveTable!.Handler(0x28));
        Assert.NotEqual((byte)0, definition.PrimitiveTables!.FlaggedCommands[0x28]);
        NativeSoftwareDrawingEnvironmentState environment = prior.NativeEffectMemory!.PrimitiveProcess!.Software!.Environment;
        environment = NativeSoftwareDrawingEnvironment.WriteE545BAD0(environment, offset.Words[0]);
        environment = NativeSoftwareDrawingEnvironment.WriteE345BA30(environment, area.Words[0]);
        environment = NativeSoftwareDrawingEnvironment.WriteE445BA60(environment, areaEnd.Words[0]).State;
        Assert.Equal(2u, environment.Derived!.Mode);
        Assert.Equal(new NativeSoftwareDrawingRectangle(704, 256, 192, 256), environment.Derived.Rectangle);
        Assert.Equal(704, environment.Derived.DrawPositionX);
        Assert.Equal(256, environment.Derived.DrawPositionY);
        byte[] before = prior.NativeEffectMemory!.Textures!.ReadVram();
        byte[] after = current.NativeEffectMemory!.Textures!.ReadVram();
        int changed = 0;
        for (int y = 256; y < 512; y++)
        {
            for (int x = 704; x < 896; x++)
            {
                int pixel = checked(2 * (y * 1024 + x));
                Assert.Equal((byte)0, after[pixel]);
                Assert.Equal((byte)0, after[pixel + 1]);
                if (before[pixel] != after[pixel] || before[pixel + 1] != after[pixel + 1])
                {
                    changed++;
                }
            }
        }
        Assert.True(changed > 0, "The linked software capture must clear its actual current VRAM rectangle.");
        observations.Add(new { CaptureSoftware = true, BlackPacketOffset = blackOffset, BlackProxy = blackProxy, AreaPacket = area.Packet, OffsetPacket = offset.Packet, Mode = environment.Derived.Mode, Rectangle = environment.Derived.Rectangle, ChangedVramWords = changed });
    }

    private static bool MatchesSource(NativeBattleLogicalCommand command, Dictionary<(ushort Clut, ushort Page), HashSet<uint>> pairs)
    {
        if (command.WordOffset != 1 || command.Opcode is not (0x24 or 0x26 or 0x2C or 0x2E or 0x34 or 0x36 or 0x3C or 0x3E))
        {
            return false;
        }
        int pageIndex = (command.Opcode & 0x10) != 0 ? 5 : 4;
        return command.Words.Length > pageIndex && pairs.ContainsKey(((ushort)(command.Words[2] >> 16), (ushort)(command.Words[pageIndex] >> 16)));
    }

    private static Dictionary<(ushort Clut, ushort Page), HashSet<uint>> TexturePairs(NativeBattleEffect210Resources resources)
    {
        Dictionary<(ushort Clut, ushort Page), HashSet<uint>> pairs = [];
        foreach (NativeBattleStaticMeshResource mesh in resources.Meshes.Values.Concat(resources.Geometry.Values))
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
                cursor += 4;
                for (int face = 0; face < count && (group is 2 or 3 or 6 or 7); face++)
                {
                    ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(cursor + face * strides[group], strides[group]);
                    var key = (BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(14)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18)));
                    if (!pairs.TryGetValue(key, out HashSet<uint>? sources))
                    {
                        sources = [];
                        pairs.Add(key, sources);
                    }
                    sources.Add(mesh.Address);
                }
                cursor += count * strides[group];
            }
        }
        return pairs;
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
