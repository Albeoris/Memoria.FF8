using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleGilgameshCaptureAssertions
{
    private readonly ushort effect;
    private readonly BattleSnapshot source;
    private readonly NativeBattleContactPacket contact;
    private int movingFrames;
    private int textPublications;
    private bool flashSeen;
    private bool flashReleased;
    private bool vibrationSeen;

    internal NativeBattleGilgameshCaptureAssertions(BattleSnapshot source, ushort effect, NativeBattleActionHeaderSlot header, NativeBattleContactPacket contact)
    {
        this.source = source;
        this.effect = effect;
        this.contact = contact;
        Assert.InRange(effect, (ushort)327, (ushort)330);
        Assert.Equal((byte)0, header.ReadByte(0));
        Assert.Equal((byte)245, header.ReadByte(1));
        Assert.Equal((byte)9, header.ReadByte(2));
        Assert.Equal(effect switch { 327 => (ushort)10, 328 => (ushort)9, 329 => (ushort)7, _ => (ushort)8 }, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        Assert.Equal((byte)0, contact.Primary.Flags2);
        Assert.Equal((byte)0, contact.Primary.HitFlags3);
        Assert.IsType<NativeBattleGilgameshProcess>(source.NativeEffectMemory!.Gilgamesh).Validate();
        Assert.IsType<NativeBattlePcRenderRequest>(source.NativeEffectMemory!.PcRenderRequest);
        Assert.NotNull(source.NativeStage);
        Assert.True(source.NativeStage!.Groups.Length >= 4);
    }

    internal static NativeBattleLogicalCommand[] Commands(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics is not { OriginalEffect: { } selected, PrimitiveFrame: { } frame } || selected.Binding.Effect != effect)
        {
            return [];
        }
        HashSet<NativeBattlePrimitiveReference> owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        return frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet))).ToArray();
    }

    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        snapshot.NativeGraphics!.ModuleEffects.Validate();
        NativeBattleLogicalCommand[] commands = Commands(snapshot, effect).Where(command => command.Opcode is >= 0x20 and < 0x80).ToArray();
        foreach (NativeBattleLogicalCommand command in commands)
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.True(visual.Identity.Effect == effect, $"Effect{effect} callback{snapshot.NativeGraphics.Callback}, packet{command.Packet}, opcode{command.Opcode:X2}, identity{visual.Identity}.");
            Assert.True(visual.Identity.Generation > 0);
            Assert.NotEqual(0L, visual.Identity.ResourceGeneration);
            Assert.InRange(visual.CornerCount, 3, 4);
            Assert.True(visual.UseSourceDepths);
            Assert.NotNull(visual.SourceDepths);
            Assert.Equal(4, command.VertexPayload4.Length);
            Assert.Equal(new System.Numerics.Vector4(command.VertexPayload4[0], command.VertexPayload4[1], command.VertexPayload4[2], command.VertexPayload4[3]), visual.SourceDepths);
            for (int corner = 0; corner < visual.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = visual.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
            }
            else
            {
                Assert.True(visual.Continuous);
            }
        }
        return commands.Length;
    }

    internal static bool HasFlash(BattleSnapshot snapshot, ushort effect)
    {
        return snapshot.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.GilgameshFlashPresentation?.Effect == effect);
    }

    internal void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        _ = AssertFrame(current, effect);
        if (Commands(current, effect).Any(command => command.Opcode is >= 0x20 and < 0x80 && command.PilotGeometry is { ScreenSpace: false, Continuous: true }))
        {
            movingFrames++;
        }
        bool flash = HasFlash(current, effect);
        if (flash)
        {
            flashSeen = true;
            cold("gilgamesh-flash-live", current);
        }
        if (HasFlash(prior, effect) && !flash)
        {
            flashReleased = true;
            cold("gilgamesh-flash-released", current);
        }
        if (current.NativeEffectMemory!.Gilgamesh!.VibrationHandle is int handle && handle > 0 && current.NativeVibrationProcess!.IsPlaying(handle))
        {
            vibrationSeen = true;
        }
        for (int slot = 0; slot < current.NativeGraphics!.Effects.Slots.Length; slot++)
        {
            NativeBattleEffectTask now = current.NativeGraphics.Effects.Slots[slot];
            NativeBattleEffectTask before = prior.NativeGraphics!.Effects.Slots[slot];
            if ((now.Flags0 & 1) != 0 && now.Kind == NativeBattleEffectKind.DamageText5069B0 && now.Frame == 1 && !((before.Flags0 & 1) != 0 && before.Kind == now.Kind && before.Frame == 1))
            {
                Assert.Equal(contact.Primary.ActorIndex, now.ModelIndex);
                Assert.Equal(contact.Primary.Amount6.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(character => (byte)(character - '0' + 56)).Append((byte)0), now.Glyphs);
                Assert.Equal(0x808080u, now.Color);
                textPublications++;
                cold("gilgamesh-contact", current);
            }
        }
    }

    internal void Complete(GameAssets game, BattleSnapshot completed)
    {
        Assert.True(movingFrames > 1);
        Assert.True(flashSeen);
        Assert.True(flashReleased);
        Assert.False(HasFlash(completed, effect));
        Assert.Equal(1, textPublications);
        Assert.True(vibrationSeen);
        Assert.False(completed.NativeVibrationProcess!.IsPlaying(completed.NativeEffectMemory!.Gilgamesh!.VibrationHandle!.Value));
        NativeBattleGilgameshEffectResources resources = game.ReadNativeGilgameshEffectResources(effect);
        uint Stream(ushort id) => unchecked((uint)(690000 + BinaryPrimitives.ReadInt32LittleEndian(resources.Files.Files.Single(file => file.FileId == id).Bytes.AsSpan())));
        uint[] expected = [resources.Director.SoundInitial, unchecked((uint)(resources.InitialStreamNumber + 690000)), resources.Director.SoundMiddle, resources.Director.SoundLate, Stream(750), Stream(752), resources.Director.SoundClosing, Stream(753)];
        NativeBattleModuleSound[] audio = completed.Events.Skip(source.Events.Length).Select(item => item.NativeModuleSound).OfType<NativeBattleModuleSound>().Where(sound => expected.Contains(sound.Sound)).ToArray();
        Assert.Equal(expected, audio.Select(sound => sound.Sound));
        Assert.Equal(new uint[] { 127, 96, 127, 127, 96, 96, 127, 96 }, audio.Select(sound => sound.Volume));
        for (int index = 0; index < audio.Length; index++)
        {
            Assert.Equal(0u, audio[index].Flags);
            Assert.Equal(128u, audio[index].RawPan);
            Assert.NotNull(audio[index].Allocation);
            _ = audio[index].ReadCommittedVoice();
            if (index > 0)
            {
                Assert.Equal(audio[index - 1].Allocation!.AfterRotation, audio[index].Allocation!.BeforeRotation);
            }
        }
        for (int group = 0; group < 4; group++)
        {
            Assert.Equal((byte)(source.NativeStage!.Groups[group].Flags | 2), completed.NativeStage!.Groups[group].Flags);
            Assert.Equal((short)0, completed.NativeStage.Groups[group].ColorDepth);
            Assert.Equal(0u, completed.NativeStage.Groups[group].ColorFlags);
        }
    }
}
