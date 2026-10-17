using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceAudioTests
{
    [Theory]
    [InlineData(true, 0, 77)]
    [InlineData(true, 4, 31)]
    [InlineData(false, 0, 6)]
    [InlineData(false, 4, 31)]
    public void Conditional_97_98_sound_reads_current_contact_bit4_after_pan_and_preserves_mask(bool actorSound, byte contactFlags, uint expected)
    {
        NativeBattleSequenceSound source = new(6, actorSound, 6, 0xFF00) { OverrideOnContact4 = true };
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(source, 0, null, _ => 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Enumerable.Repeat(77u, 1120).ToImmutableArray(), contactFlags)!;
        Assert.Equal(expected, sound.Sound);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(0xFF00u, sound.Flags);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceAudio.Resolve505AB0(source, 0, null, _ => 0, _ => default, _ => 128, null));
    }

    [Fact]
    public void Source_metadata_keeps_the_legacy_JSON_field_names_without_treating_attributes_as_volume()
    {
        const string original = "{\"SoundId\":6,\"ActorSound\":true,\"Attribute\":6,\"Volume\":65280}";
        NativeBattleSequenceSound source = JsonSerializer.Deserialize<NativeBattleSequenceSound>(original)!;
        using JsonDocument prior = JsonDocument.Parse(original);
        Assert.Equal((byte)6, source.Flags);
        Assert.Equal((ushort)0xFF00, source.Attributes);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source), prior.RootElement));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    public void Projected_pan_reads_bone241_from_the_original_current_or_first_target_model(byte flags, int source)
    {
        List<int> reads = [];
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(new(255, false, flags, 0), 3, 5, _ => throw new InvalidOperationException(), index =>
        {
            reads.Add(index);
            return new(10, 20, 30);
        }, position =>
        {
            Assert.Equal(new NativeBattleVector(10, 20, 30), position);
            return 175;
        }, null)!;
        Assert.Equal(new[] { source }, reads);
        Assert.Equal(255u, sound.Sound);
        Assert.Equal((byte)175, sound.Pan);
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 0), sound.Anchor);
        Assert.Equal(127u, sound.Volume);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(0xFD)]
    public void Fixed_pan_does_not_require_a_target_bone_camera_or_actor_table(byte flags)
    {
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(new(0, false, flags, 0), 3, null, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), null)!;
        Assert.Equal((byte)128, sound.Pan);
        Assert.Null(sound.Anchor);
        Assert.Equal(0u, sound.Sound);
        Assert.Equal((byte)26, sound.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess()).Sound.ReadCommittedVoice());
    }

    [Fact]
    public void Actor_identity_is_loaded_BYTE4_without_a_second_enemy_offset_and_mask_is_the_optional_BYTE_shifted8()
    {
        ImmutableArray<uint> table = Enumerable.Range(0, 1120).Select(index => (uint)(90000 + index)).ToImmutableArray();
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(new(6, true, 6, 0xFF00), 3, null, index =>
        {
            Assert.Equal(3, index);
            return 77;
        }, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), table)!;
        Assert.Equal(table[77 * 7 + 6], sound.Sound);
        Assert.Equal(0xFF00u, sound.Flags);
        Assert.Equal((byte)9, sound.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess()).Sound.ReadCommittedVoice());
        Assert.Equal(127u, sound.Volume);
    }

    [Theory]
    [InlineData(160, 0)]
    [InlineData(159, 7)]
    public void Native5015A0_rejects_table_operands_without_allocating_a_voice(byte identity, byte sound)
    {
        Assert.Null(NativeBattleSequenceAudio.Resolve505AB0(new(sound, true, 4, 0), 3, null, _ => identity, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), null));
    }

    [Fact]
    public void Required_unknown_sources_fail_lazily_at_the_native_read()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceAudio.Resolve505AB0(new(0, true, 4, 0), 3, null, _ => 0, _ => default, _ => 128, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceAudio.Resolve505AB0(new(0, false, 1, 0), 3, null, _ => 0, _ => default, _ => 128, null));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceAudio.Resolve505AB0(new(0, false, 4, 0x100), 3, null, _ => 0, _ => default, _ => 128, null));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceAudio.Resolve505AB0(new(0, false, 6, 1), 3, null, _ => 0, _ => default, _ => 128, null));
    }

    [Theory]
    [InlineData(0x97, true)]
    [InlineData(0x98, false)]
    [InlineData(0xB5, true)]
    [InlineData(0xB6, true)]
    [InlineData(0xB8, false)]
    public void Original_opcodes_consume_the_optional_unsigned_attribute_BYTE_and_publish_inline_before_contact(byte opcode, bool actorSound)
    {
        byte[] bytes = [1, 0, 4, 0, opcode, 6, 6, 255, 0xAA, 0xB8, 0, 4, 0xA1];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 1);
        NativeBattleGraphicsResource resource = new(bytes.ToImmutableArray()) { AnimationFrames = [1] };
        NativeBattleGraphicsModel model = new(3, 3) { ModelId4 = 77, Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = 1, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
        List<string> calls = [];
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null)
        {
            PlaySound = (current, cue) =>
            {
                Assert.Equal(3, current.Index);
                Assert.Equal(cue.First == 6 && opcode is 0x97 or 0x98, cue.OverrideSoundOnContact4);
                calls.Add($"sound:{cue.First}");
                NativeBattleSequenceSound source = new((byte)cue.First, cue.Kind == NativeBattleSequenceCueKind.ActorSound, (byte)cue.Second, (ushort)cue.Third);
                NativeBattleModuleSound resolved = NativeBattleSequenceAudio.Resolve505AB0(source, 3, null, _ => 77, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Enumerable.Repeat(123u, 1120).ToImmutableArray())!;
                (process, _) = resolved.Commit46B2A0(process);
            },
            ApplyActionContacts = current =>
            {
                calls.Add("contact");
                Assert.Equal((byte)1, process.Rotation);
                return current;
            }
        });
        Assert.Equal(new[] { "sound:6", "contact", "sound:0" }, calls);
        Assert.Equal(actorSound ? NativeBattleSequenceCueKind.ActorSound : NativeBattleSequenceCueKind.SystemSound, step.Cues[0].Kind);
        Assert.Equal(0xFF00, step.Cues[0].Third);
        Assert.Equal((byte)2, process.Rotation);
    }
}
