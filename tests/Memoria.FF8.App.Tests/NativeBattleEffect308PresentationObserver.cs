using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect308PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> scripts = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> models = [];
    private readonly List<object> observations = [];
    private readonly Dictionary<int, uint> savedColors = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool? cameraDecision;
    private bool mainRetired;
    private bool queuesCleared;
    private bool queued;
    private bool uploaded;
    private bool drained;
    private int? vibration;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect308State state = current.NativeEffectMemory!.Effect308!;
        NativeBattleEffect308Resources resources = effect.Binding.Effect308Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? root = tasks.Values.FirstOrDefault(task => task.Callback == RootCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        callbacks.UnionWith(tasks.Values.Select(task => task.Callback));
        if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
        {
            if (cameraDecision is bool previousDecision)
            {
                Assert.Equal(previousDecision, decision);
            }
            cameraDecision = decision;
        }
        Assert.True(state.ScriptSourcesBound);
        Assert.Equal(ScriptRanges.Select(range => range.Address).Order(), state.ScriptStorage.Keys.Order());
        foreach ((uint address, int length) in ScriptRanges)
        {
            Assert.Equal(length, state.ScriptStorage[address].Read(0, length).Length);
        }
        uint arena = state.ReadDword(ArenaGlobal);
        int caster = NativeBattleEffect308Controller.Actor(state.ReadDword(CasterGlobal));
        Assert.Equal(3, caster);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = pool.DescriptorAddress == RootDescriptor ? Assert.Single(Pools) : Assert.Single(CommonPools, shape => shape.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal(shape.Capacity, pool.Slots.Length);
            Assert.Equal(shape.Descriptor == RootDescriptor ? shape.Bank : arena + shape.Bank, pool.BankAddress);
            if (shape.Descriptor == RootDescriptor)
            {
                Assert.Null(state.FixedBanks[shape.Bank]);
            }
            else
            {
                Assert.Null(state.CommonQueueStorage[shape.Descriptor]);
            }
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(CameraModelOffset, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[CameraModelOffset..]));
            track |= playing;
            view |= playing && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
        }
        if (main is not null)
        {
            vibration ??= unchecked((int)main.ReadDword(16));
            for (int index = 0; index < 3; index++)
            {
                NativeBattleGraphicsModel model = graphics.Models[caster + index];
                Assert.Equal((byte)(121 + index), model.ModelId4);
                Assert.Equal(index == 0 ? 44 : 6, model.CurrentBodyBoneMatrices!.Matrices.Length);
                Assert.NotNull(model.Color44);
                savedColors.TryAdd(caster + index, main.ReadDword(20 + index * 4));
                Assert.Equal(savedColors[caster + index], main.ReadDword(20 + index * 4));
                models.Add(model.Index);
            }
        }
        foreach (NativeBattleModulePoolTask script in tasks.Values.Where(task => task.Callback == ScriptCallback))
        {
            int actor = NativeBattleEffect308Controller.Actor(script.ReadDword(12));
            Assert.InRange(actor, caster, caster + 2);
            uint pc = script.ReadDword(24);
            (uint source, int length) = ScriptRanges[actor - caster];
            Assert.InRange(pc, source, source + (uint)length);
            scripts.Add(source);
        }
        if (oldAge == 85)
        {
            Assert.DoesNotContain(tasks.Values, task => task.Callback is ScriptCallback or 0x6685E0 or 0x668A50);
            queuesCleared = true;
        }
        if (before is not null && main is null)
        {
            Assert.Equal(110, oldAge);
            foreach ((int actor, uint color) in savedColors)
            {
                Assert.Equal(color, graphics.Models[actor].Color44);
                Assert.Equal(0, graphics.Models[actor].Flags0 & 8);
            }
            mainRetired = true;
            Mark("effect308-main110-restores-triple-colors", current, cold);
        }
        if (current.NativeEffectMemory.EffectIo?.BaseOffset == IoOffset)
        {
            byte[] io = current.NativeEffectMemory.Arena!.Read(IoOffset, 24);
            if (io[3] == 2)
            {
                Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(8)));
                Assert.Equal((ushort)360, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(10)));
                Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(16)));
                Assert.Equal((ushort)361, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(18)));
                Assert.Equal(arena + FirstPacketOffset, BinaryPrimitives.ReadUInt32LittleEndian(io.AsSpan(12)));
                Assert.Equal(arena + FirstPacketOffset, BinaryPrimitives.ReadUInt32LittleEndian(io.AsSpan(20)));
                queued = true;
                Mark("effect308-age103-files360-361-shared-destination", current, cold);
                if (io[4] == 2 && io[0] == 0 && io[1] == 0)
                {
                    byte[] file = resources.Files[361].Data.ToArray();
                    Assert.Equal(file, current.NativeEffectMemory.Arena.Read(FirstPacketOffset, file.Length));
                    VerifyTim(file, current.NativeEffectMemory.Textures!.ReadVram());
                    uploaded = true;
                    Mark("effect308-file361-completion-VRAM", current, cold);
                }
            }
        }
        NativeBattleLogicalCommand[] commands = CurrentCommands(current);
        if (commands.Length > 0)
        {
            uint cursor = state.ReadDword(CursorGlobal);
            banks.Add(cursor >= arena + SecondPacketOffset ? 1 : 0);
        }
        if (root is not null && root.ReadWord(12) == 1 && main is null)
        {
            Mark("effect308-root-before-triple-initialization", current, cold);
        }
        if (age is 1 or 20 or 70 or 85 or 86 or 102 or 103 or 104 or 106 or 107 or 110)
        {
            Mark("effect308-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
            Mark("effect308-all-pools-returned", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, MainAge = age, PreviousMainAge = oldAge, RootAge = root?.ReadWord(12), Commands = commands.Length, Cursor = state.ReadDword(CursorGlobal), Pools = effect.Pools.Select(pool => new { pool.DescriptorAddress, pool.BankAddress, Live = pool.Slots.Count(task => (task.Flags & 1) != 0) }), Colors = graphics.Models.Skip(caster).Take(3).Select(model => model.Color44) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track);
            Assert.True(view);
        }
        Assert.True(mainRetired);
        Assert.True(queuesCleared);
        Assert.True(queued);
        Assert.True(uploaded);
        Assert.True(drained);
        Assert.Equal(new uint[] { RootCallback, ScriptCallback, 0x6685E0, 0x668A50, MainCallback }.Order(), callbacks.Order());
        Assert.Equal(ScriptRanges.Select(range => range.Address).Order(), scripts.Order());
        Assert.Equal(new[] { 0, 1 }, banks.Order());
        Assert.Equal(new[] { 3, 4, 5 }, models.Order());
        Assert.True(vibration is > 0);
        Assert.False(completed.NativeVibrationProcess!.IsPlaying(vibration.Value));
        Assert.All(completed.NativeEffectMemory!.Effect308!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(completed.NativeEffectMemory.Effect308.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        Assert.InRange(markers.Count, 1, 20);
        File.WriteAllText(Path.Combine(output, "effect308-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Scripts = scripts.Order(), PacketBanks = banks.Order(), Models = models.Order(), Camera = camera, CameraDecision = cameraDecision, CameraTrack = track, CameraViewChanged = view, MainRetired = mainRetired, QueuesCleared = queuesCleared, Queued = queued, Uploaded = uploaded, Drained = drained, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot current)
    {
        NativeBattleEffect308State? state = current.NativeEffectMemory?.Effect308;
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } || state is null)
        {
            return [];
        }
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (cursor < arena + FirstPacketOffset || cursor > arena + MorphOffset)
        {
            return [];
        }
        uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - arena && command.Packet.ByteOffset < cursor - arena).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
    }

    public static NativeBattleLogicalCommand[] CurrentFadeCommands(BattleSnapshot current)
    {
        return CurrentCommands(current).Where(command => command.Opcode is 0x28 or 0x2A).ToArray();
    }

    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
    {
        Dictionary<uint, NativeBattleModulePoolTask> tasks = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return tasks;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            int? index = pool.Head;
            HashSet<int> visited = [];
            while (index is int slot)
            {
                Assert.True(visited.Add(slot));
                tasks.Add(pool.BankAddress + (uint)(slot * pool.Stride), pool.Slots[slot]);
                index = pool.Slots[slot].Next;
            }
        }
        return tasks;
    }

    private static void VerifyTim(byte[] data, byte[] vram)
    {
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 4));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 6));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10));
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.AsSpan(offset + 12 + row * width * 2, width * 2).ToArray(), vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2).ToArray());
            }
            offset += size;
        }
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
