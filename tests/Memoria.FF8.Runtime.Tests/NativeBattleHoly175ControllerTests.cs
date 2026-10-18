using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175ControllerTests
{
    [Fact]
    public void WholePhysicalLifecycleKeepsLateFlareContactAndActualCompletionIndependent()
    {
        Harness harness = new();
        NativeBattleHoly175Frame frame = harness.Initialize();
        Assert.Equal(new[] { 2, 100 }, frame.Effect.Pools.Select(pool => pool.Slots.Length).ToArray());
        Assert.Throws<NotSupportedException>(() => frame.Memory.Holy175!.Sprite31Rows.Read(14, 2));
        uint completion = 0;
        int loadCallback = 0;
        for (int callback = 1; callback <= 140; callback++)
        {
            harness.Callback = callback;
            frame = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(), harness.Resources);
            if (callback <= 115)
            {
                Assert.Equal((callback & 1) == 0 ? 0xC000 : 0, frame.Effect.DrawArenaOffset);
            }
            if (callback == 112)
            {
                Assert.DoesNotContain(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == 0x5D1BD0);
                Assert.Contains(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == 0x5D43B0);
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
            if (callback == 115)
            {
                Assert.Null(frame.Effect.Pools[1].Head);
                NativeBattleModulePoolTask final = Assert.Single(frame.Effect.Pools[0].Slots.Where(task => (task.Flags & 1) != 0));
                Assert.Equal(0x5D4540u, final.Callback);
                Assert.Equal((ushort)1, final.ReadWord(0xC));
            }
            if (harness.Operations.LastOrDefault().Operation is NativeBattleHoly175LoadCommon load && completion == 0)
            {
                loadCallback = callback;
                completion = load.CompletionWordAddress;
            }
            if (loadCallback != 0 && callback == loadCallback + 2)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
                frame = frame with { Effect = NativeBattleHoly175Controller.WriteCompletionWord(frame.Effect, completion, 1) };
            }
            if (frame.Effect.ReturnedDescriptor == 0)
            {
                Assert.Equal(loadCallback + 3, callback);
                break;
            }
        }
        Assert.Equal(116, loadCallback);
        Assert.Equal(10, harness.Operations.Count(item => item.Operation is NativeBattleHoly175Capture));
        Assert.Equal(1, harness.Operations.Count(item => item.Operation is NativeBattleHoly175Sound));
        Assert.Equal(1, harness.Operations.Count(item => item.Operation is NativeBattleHoly175UploadTim));
        Assert.Equal(107, Assert.Single(harness.Operations, item => item.Operation is NativeBattleHoly175Contact).Callback);
        Assert.Equal(25, harness.Operations.Count(item => item.Operation is NativeBattleHoly175DrawTask { Task.Callback: 0x5D43B0 }));
        Assert.Equal(ExpectedRandom(7131), frame.Random);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Holy175!.Sprite31Rows.Read(14, 2));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Holy175!.Sprite4Rows.Read(16, 2));
    }

    [Fact]
    public void PausedPrimaryStopsBeforeTimelineButRootAndLiveWarpStillDrawWithoutAging()
    {
        Harness harness = new(flags: 2);
        NativeBattleHoly175Frame frame = harness.Initialize();
        NativeCrtRandomState initial = frame.Random;
        frame = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(0x201), harness.Resources);
        Assert.Equal(initial, frame.Random);
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(0xC));
        for (int index = 0; index < 7; index++)
        {
            frame = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(), harness.Resources);
        }
        NativeBattleModulePoolTask warp = frame.Effect.Pools[1].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == 0x5D2910);
        ushort age = warp.ReadWord(0xC);
        int captures = harness.Operations.Count(item => item.Operation is NativeBattleHoly175Capture);
        string rows = JsonSerializer.Serialize(frame.Memory.Holy175!.Sprite31Rows);
        NativeCrtRandomState random = frame.Random;
        frame = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(0x200), harness.Resources);
        Assert.Equal(random, frame.Random);
        Assert.Equal(rows, JsonSerializer.Serialize(frame.Memory.Holy175!.Sprite31Rows));
        Assert.Equal(age, frame.Effect.Pools[1].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == 0x5D2910).ReadWord(0xC));
        Assert.Equal(captures + 1, harness.Operations.Count(item => item.Operation is NativeBattleHoly175Capture));
        Assert.NotEmpty(harness.Sprite31Frames);
    }

    [Fact]
    public void ColdFrameAndExternalModelOwnersContinueExactlyAndHeaderChainRunsSamePass()
    {
        Harness harness = new(headers: 2, flags: 2);
        NativeBattleHoly175Frame frame = harness.Initialize();
        for (int callback = 1; callback <= 240 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            harness.Callback = callback;
            if (callback is 7 or 48 or 105 or 113)
            {
                Saved saved = new(frame, harness.Models);
                Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
                Harness independent = new(headers: 2, flags: 2) { Models = restored.Models };
                NativeBattleHoly175Frame first = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(), harness.Resources);
                NativeBattleHoly175Frame second = NativeBattleHoly175Controller.Advance5D44C0(restored.Frame, independent.Inputs(), independent.Resources);
                Assert.Equal(JsonSerializer.Serialize(new Saved(first, harness.Models)), JsonSerializer.Serialize(new Saved(second, independent.Models)));
                frame = first;
            }
            else
            {
                frame = NativeBattleHoly175Controller.Advance5D44C0(frame, harness.Inputs(), harness.Resources);
            }
            if (callback == 112)
            {
                Assert.Contains(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == 0x5D1BD0 && task.ReadWord(0xE) == 1 && task.ReadWord(0xC) == 1);
            }
        }
        Assert.Equal(2, harness.Operations.Count(item => item.Operation is NativeBattleHoly175Contact));
        Assert.Equal(ExpectedRandom(14262), frame.Random);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.DoesNotContain(harness.Operations, item => item.Operation is NativeBattleHoly175LoadCommon);
    }

    [Fact]
    public void UnknownImportedOwnersAndIncorrectCompletionTargetsFailWithoutEntryHydration()
    {
        Harness harness = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175Controller.Initialize5D1AD0(harness.Frame with { Memory = harness.Frame.Memory with { Holy175 = null } }, harness.Inputs(), harness.Resources));
        NativeBattleHoly175Frame frame = harness.Initialize();
        Assert.Throws<InvalidDataException>(() => NativeBattleHoly175Controller.WriteCompletionWord(frame.Effect, NativeBattleHoly175Controller.ChildBank + 0xE, 1));
        NativeBattleHoly175State warm = frame.Memory.Holy175!;
        NativeBattleHoly175State changed = warm with { BlackPackets = warm.BlackPackets.Write(4, new byte[] { 1, 2, 3, 4 }) };
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, changed.Materialize(harness.Resources).BlackPackets.Read(4, 4));
    }

    private sealed record Saved(NativeBattleHoly175Frame Frame, ImmutableArray<NativeBattleGraphicsModel> Models);
    private static NativeCrtRandomState ExpectedRandom(int draws)
    {
        NativeCrtRandomState random = new(1);
        for (int draw = 0; draw < draws; draw++)
        {
            random = random.Draw(out _);
        }
        return random;
    }

    private sealed class Harness
    {
        public NativeBattleHoly175Frame Frame { get; }
        public NativeBattleHoly175Resources Resources { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public ImmutableArray<NativeBattleGraphicsModel> Models { get; set; } = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2) { Color40 = 0x32808080, Color44 = 0x00808080, Alpha7 = 0 }).ToImmutableArray();
        public List<(int Callback, NativeBattleHoly175Operation Operation)> Operations { get; } = [];
        public List<ushort> Sprite31Frames { get; } = [];
        public int Callback { get; set; }

        public Harness(int headers = 1, byte flags = 0)
        {
            NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
            for (int index = 0; index < headers; index++)
            {
                (packets, _) = packets.Prepare(new(0, 2, 4, 0, (ushort)(1 << (3 + index)), 175, null));
                (packets, _) = packets.AppendContact(new(new((byte)(3 + index), 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
                (packets, _) = packets.Publish(1);
            }
            Packets = packets.WritePlayerRepeatMode(0, (byte)headers);
            byte[] delays = new byte[48];
            for (int index = 0; index < 12; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(delays.AsSpan(index * 4), 0xABCD0000u | (uint)index);
            }
            Resources = new(new("declared-animation", []), new("declared-tim", []), [], [], new("declared-sound", new byte[4].ToImmutableArray())) { CaptureBlackPackets = new("declared-black", new byte[48].ToImmutableArray()), DelayTable = new("declared-delays", delays.ToImmutableArray()) };
            NativeBattleOriginalEffectState effect = new(175, 174, new(175, 0x5D1AD0, 0x5D1AB0), new(0, flags, 8, 0, 0, 0), null);
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Holy175 = NativeBattleHoly175State.FromColdProcess() }, effect, new(1));
        }

        public NativeBattleHoly175Frame Initialize() => NativeBattleHoly175Controller.Initialize5D1AD0(Frame, Inputs(), Resources);

        public NativeBattleHoly175Inputs Inputs(uint flags = 0) => new(flags, Packets, _ => new byte[8].ToImmutableArray(), _ => 4096, actor => Models[actor].Flags0, (frame, task) =>
        {
            NativeBattleHoly175ModelColorStep step = NativeBattleHoly175ModelColors.Advance(task, Models, flags, 0x00808080, 0x32808080, NativeBattleGteColorInterpolation.CodeConstants45F270);
            Models = step.Models;
            return new(frame, step.Task, step.Result);
        }, (frame, _, _) => frame, (frame, _, row) =>
        {
            Assert.InRange(row.Frame, (ushort)0, (ushort)30);
            Sprite31Frames.Add(row.Frame);
            return new(frame, row.Frame == 30 ? (short)-1 : (short)1);
        }, (frame, operation) =>
        {
            Operations.Add((Callback, operation));
            return frame;
        });
    }
}
