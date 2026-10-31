using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect301PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool scriptBound;
    private bool scriptExhausted;
    private bool programme;
    private bool records;
    private bool bodyScratch;
    private bool mapSentinels;
    private bool hiddenParty;
    private bool restoredParty;
    private bool vibrationStarted;
    private bool vibrationStopped;
    private bool mainRetired;
    private bool orphans;
    private bool geometry;
    private bool drained;
    private bool color44Retained;
    private int meshBirths;
    private int spriteBirths;
    public int Contacts => 0;
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    private int capturedPrimitives;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        capturedPrimitives += NativeBattleRecovery32CaptureAssertions.AssertFrame(current, 301);
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect301State state = current.NativeEffectMemory!.Effect301!;
        NativeBattleEffect301Resources resources = effect.Binding.Effect301Resources!;
        int caster = effect.Parameters!.Actor78;
        Assert.InRange(caster, 3, 6);
        Assert.Equal((byte)110, graphics.Models[caster].ModelId4);
        Assert.Equal(40, graphics.Models[caster].CurrentBodyBoneMatrices!.Matrices.Length);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        meshBirths += tasks.Values.Count(task => task.Callback == 0x66EC50 && task.ReadWord(18) == 1);
        spriteBirths += tasks.Values.Count(task => task.Callback == 0x66EE60 && task.ReadWord(18) == 1);
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            track |= current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            view |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            Mark("effect301-current-camera8-24", current, cold);
        }
        if (main is not null)
        {
            Assert.Equal(0x103D46Cu, main.ReadDword(0x1C));
            Assert.Equal((uint)Math.Min(age, 121), main.ReadDword(0x20));
            programme = true;
            Assert.Equal(new byte[2400].Select(_ => (byte)255), main.Payload.AsSpan(0x4AC - 12, 2400).ToArray());
            mapSentinels = true;
            for (int index = 0; index < 128; index++)
            {
                int life = main.ReadWord(0xAC + index * 8 + 6);
                Assert.InRange(life, 0, 12);
                records |= life != 0;
            }
            if (age >= 1)
            {
                Assert.Equal(age <= 121 ? 4u : 0u, (uint)(graphics.Models[caster].Flags0 & 0xC));
            }
            for (int actor = 0; actor < 3; actor++)
            {
                Assert.Equal(4, graphics.Models[actor].Flags0 & 4);
            }
            hiddenParty = true;
            if (!vibrationStarted)
            {
                int handle = unchecked((int)main.ReadDword(16));
                NativeVibrationSlot slot = current.NativeVibrationProcess!.Slots[handle & 3];
                Assert.Equal(handle, slot.Generation * 16 | slot.Id);
                Assert.Equal((byte)128, slot.Priority);
                Assert.True(current.NativeVibrationProcess.IsPlaying(handle));
                vibrationStarted = true;
                Mark("effect301-current-vibration-returned-handle", current, cold);
            }
        }
        if (state.ScriptSourcePending == false)
        {
            scriptBound = true;
            uint expected = 0x103BD00;
            for (int processed = 1; processed <= Math.Min(Math.Max(age, 0), 101); processed++)
            {
                expected += checked((uint)(2 + resources.ReadScriptWord(expected) * 20));
                expected += checked((uint)(2 + resources.ReadScriptWord(expected) * 12));
            }
            uint actual = state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress);
            if (main is not null)
            {
                Assert.Equal(expected, actual);
            }
            scriptExhausted |= actual == 0x103CFB8;
        }
        if (oldAge == 137 && before is not null)
        {
            Assert.False(current.NativeVibrationProcess!.IsPlaying(unchecked((int)before.ReadDword(16))));
            vibrationStopped = true;
        }
        if (before is not null && main is null)
        {
            Assert.Equal(141, oldAge);
            mainRetired = true;
            orphans = tasks.Values.Any(task => task.Callback is 0x66EC50 or 0x66EE60);
            Mark("effect301-main142-return-and-current-child-owners", current, cold);
        }
        if (age is >= 122 and <= 141)
        {
            Assert.Equal(0x32u, graphics.Models[caster].Color44!.Value >> 24);
            color44Retained = true;
        }
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena != 0)
        {
            if (age is >= 1 and <= 121)
            {
                int offset = checked((int)(arena - 0x20DFAB8)) + MorphOffset;
                var snapshot = current.NativeEffectMemory.Arena!.ReadSnapshot(offset, MorphLength);
                bodyScratch |= snapshot.Known.Take(4).All(value => value) && snapshot.Known.Skip(MorphLength - 16).Take(4).All(value => value);
            }
            uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
            if (cursor >= start && cursor <= start + 0x10000)
            {
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                if (CurrentCommands.Length > 0)
                {
                    banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                    geometry |= CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length == 4);
                    Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
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
        if (age is 1 or 12 or 60 or 101 or 102 or 121 or 122 or 138 or 141)
        {
            Mark("effect301-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
            for (int actor = 0; actor < 3; actor++)
            {
                Assert.Equal(state.ReadDword(SavedPartyFlagsAddress + (uint)(actor * 4)) & 4, (uint)(graphics.Models[actor].Flags0 & 4));
            }
            Assert.Equal(0, graphics.Models[caster].Flags0 & 0xC);
            Assert.Equal(0x103CFB8u, state.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
            restoredParty = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, Caster = caster, Age = age, PreviousAge = oldAge, Cursor = cursor, ScriptPending = state.ScriptSourcePending, Commands = CurrentCommands.Length, MeshBirths = meshBirths, SpriteBirths = spriteBirths });
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect301-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Camera = camera, CameraTrack = track, CameraView = view, ScriptBound = scriptBound, ScriptExhausted = scriptExhausted, Programme = programme, BodyScratch = bodyScratch, MapSentinels = mapSentinels, Records = records, HiddenParty = hiddenParty, RestoredParty = restoredParty, VibrationStarted = vibrationStarted, VibrationStopped = vibrationStopped, MainRetired = mainRetired, ChildSurvivorsAfterMain = orphans, Color44Code32 = color44Retained, MeshBirths = meshBirths, SpriteBirths = spriteBirths, Geometry = geometry, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(capturedPrimitives > 0, "The complete standalone route did not observe owned moving geometry.");
        NativeBattleRecovery32CaptureAssertions.AssertFrame(completed, 301);
        Publish(output);
        Assert.True(camera && track && view);
        Assert.True(scriptBound && scriptExhausted);
        Assert.True(programme && bodyScratch && mapSentinels && records);
        Assert.True(hiddenParty && restoredParty);
        Assert.True(vibrationStarted && vibrationStopped);
        Assert.True(mainRetired && drained);
        Assert.True(color44Retained);
        Assert.True(geometry);
        Assert.Equal(115, meshBirths);
        Assert.Equal(174, spriteBirths);
        Assert.Equal(2, banks.Count);
        Assert.Equal(0, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect301!.FixedBanks.Values, bank => Assert.NotNull(bank));
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
            while (index is int slot)
            {
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
