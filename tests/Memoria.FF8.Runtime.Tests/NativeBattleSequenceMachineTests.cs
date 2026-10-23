using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceMachineTests
{
    [Theory]
    [InlineData("930001")]
    [InlineData("AF01")]
    [InlineData("BF01")]
    [InlineData("B2")]
    [InlineData("B7")]
    [InlineData("AA")]
    [InlineData("B4FF00")]
    public void Self_replacement_changes_header_but_retains_physical_PC_across_callbacks(string select)
    {
        NativeBattleGraphicsResource resource = Resource((1, "A1"), (2, select + "C02A00E500A1C02B00E501A1"));
        NativeBattleGraphicsModel Replace(NativeBattleGraphicsModel current) => NativeBattleSequenceSelections.SelectResource505BC0(current, resource, 1);
        NativeBattleSequenceInputs inputs = new(null, null)
        {
            SelectModelSequence = (current, _, _, _) => Replace(current),
            ApplyNextContact = Replace,
            ApplyCurrentContact = Replace,
            ApplyActionContacts = Replace,
            ScheduleContactEffect = (current, program) => (Replace(current), program.Offset + 2)
        };
        NativeBattleSequenceStep first = NativeBattleSequenceMachine.Advance(Model(2), resource, inputs);
        Assert.Equal((byte)1, first.Model.Sequence);
        Assert.Equal((short)42, first.Model.SequenceState!.Locals[0]);
        Assert.Equal((ushort)(1 + select.Length / 2 + 6), first.Model.SequenceState.Offset46);
        NativeBattleSequenceStep next = NativeBattleSequenceMachine.Advance(first.Model, resource, inputs);
        Assert.Equal((short)43, next.Model.SequenceState!.Locals[1]);
        Assert.Equal((byte)1, next.Model.Sequence);
    }

    [Fact]
    public void Replaced_header_offset_stores_low_WORD_even_when_old_PC_precedes_new_program()
    {
        NativeBattleGraphicsResource resource = Resource((1, "AF02A1A1"), (2, "A1"));
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)
        {
            SelectModelSequence = (current, _, _, selected) => NativeBattleSequenceSelections.SelectResource505BC0(current, resource, selected)
        });
        Assert.Equal((byte)2, step.Model.Sequence);
        Assert.Equal(ushort.MaxValue, step.Model.SequenceState!.Offset46);
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(step.Model, resource, new(null, null)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alternate_pass_restarts_uses_header48_scratch_and_obeys_model_flag8(bool suppressed)
    {
        NativeBattleGraphicsResource resource = Resource((1, "C37FE501A1"), (2, "C00100E500C00B00E57FA1"));
        NativeBattleGraphicsModel model = Model(1);
        model = model with { Flags2 = suppressed ? (ushort)12 : (ushort)4, SequenceState = model.SequenceState! with { Alternate43 = 2, Scratch = model.SequenceState.Scratch.SetItem(0, 27) } };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Equal(suppressed ? null : (short?)1, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((short)27, step.Model.SequenceState.Locals[1]);
        Assert.Equal(suppressed ? null : (short?)11, step.Model.SequenceState.AlternateScratch48[0]);
        Assert.Equal((short)27, step.Model.SequenceState.Scratch[0]);
        Assert.Equal((ushort)5, step.Model.SequenceState.Offset46);
        Assert.Equal((byte)1, step.Model.SequenceState.Callback42);
        NativeBattleSequenceStep selected = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "9A02A1"), (2, "A1")), new(null, null));
        Assert.Equal((byte)2, selected.Model.SequenceState!.Alternate43);
        Assert.All(selected.Model.SequenceState.AlternateScratch48, Assert.Null);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    public void Linked_position_selectors_use_signed_words_and_native_null_reads_zero_and_writes_nothing(byte selector)
    {
        NativeBattleGraphicsResource resource = Resource((1, $"C0FEFFE5{selector:X2}C3{selector:X2}E500A1"));
        NativeBattleGraphicsModel model = Model(1) with { LinkedTransform = new(new(4, 5, 6), new(11, 22, 33)) };
        NativeBattleSequenceStep linked = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Equal((short)-2, linked.Model.SequenceState!.Locals[0]);
        NativeBattleVector expected = selector switch { 20 => new(-2, 22, 33), 21 => new(11, -2, 33), _ => new(11, 22, -2) };
        Assert.Equal(expected, linked.Model.LinkedTransform!.Position);
        Assert.Equal(model.LinkedTransform.Rotation, linked.Model.LinkedTransform.Rotation);
        NativeBattleSequenceStep absent = NativeBattleSequenceMachine.Advance(model with { LinkedTransform = null }, resource, new(null, null));
        Assert.Null(absent.Model.LinkedTransform);
        Assert.Equal((short)0, absent.Model.SequenceState!.Locals[0]);
    }

    [Fact]
    public void Opcode92_publishes_current_model_before_rebinding_and_reads_new_external_geometry_inline()
    {
        int calls = 0;
        int distance = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1) with { Rotation14 = 0 }, Resource((1, "C03412E51792FFC310E500A1")), new(null, null)
        {
            RebindActionGeometry = (current, selector) =>
            {
                calls++;
                Assert.Equal((byte)255, selector);
                Assert.Equal((short)0x1234, current.Rotation14);
                Assert.Equal((byte)1, current.Sequence);
                distance = 12345;
                return current with { Rotation14 = 2048 };
            },
            ReadExternal = variable => variable == 16 ? distance : throw new InvalidDataException()
        });
        Assert.Equal(1, calls);
        Assert.Equal((short)12345, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((short)2048, step.Model.Rotation14);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "9200A1")), new(null, null)));
    }

    [Fact]
    public void Native_default_command_arms_are_operandless_no_ops()
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "8788898A8B8C8D8E8FB3C207E500A1")), new(null, null));
        Assert.Equal((short)7, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((ushort)3, step.Model.Flags0);
        Assert.Empty(step.Cues);
    }

    [Fact]
    public void Sequence_write43_stores_only_the_retained_model_byte145()
    {
        NativeBattleGraphicsModel model = Model(1) with { BlinkCountdown144 = 21, MinimumY146 = 1234 };
        Assert.Null(model.SequenceByte145);
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, Resource((1, "C0FEFFE52BA1")), new(null, null));
        Assert.Equal((byte)254, step.Model.SequenceByte145);
        Assert.Equal((byte)21, step.Model.BlinkCountdown144);
        Assert.Equal((short)1234, step.Model.MinimumY146);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(step.Model))!;
        Assert.Equal((byte)254, restored.SequenceByte145);
    }

    [Theory]
    [InlineData("93FEFFA1", 0x93, 254, 255)]
    [InlineData("AFFFA1", 0xAF, 0, 255)]
    [InlineData("BFFFA1", 0xBF, 0, 255)]
    public void Shared_model_sequence_commands_decode_unsigned_operands_and_continue_inline(string program, byte expectedOpcode, byte expectedSelector, byte expectedSequence)
    {
        int calls = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, program)), new(null, null)
        {
            SelectModelSequence = (current, opcode, selector, sequence) =>
            {
                calls++;
                Assert.Equal(expectedOpcode, opcode);
                Assert.Equal(expectedSelector, selector);
                Assert.Equal(expectedSequence, sequence);
                return current with { Flags8 = 0x1234 };
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal(0x1234u, step.Model.Flags8);
    }

    [Fact]
    public void B7_consumes_current_contact_inline_without_selecting_the_advance_callback()
    {
        int calls = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B7C31FE500A1")), new(null, null)
        {
            ApplyCurrentContact = current =>
            {
                calls++;
                return current with { Flags8 = 0x4321 };
            },
            ApplyNextContact = _ => throw new InvalidDataException("B7 does not advance the physical contact pointer.")
        });
        Assert.Equal(1, calls);
        Assert.Equal((short)0x4321, step.Model.SequenceState!.Locals[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B7A1")), new(null, null)));
    }

    [Fact]
    public void B2_applies_and_advances_the_current_contact_inline_before_following_expression()
    {
        int calls = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B2C31FE500A1")), new(null, null)
        {
            ApplyNextContact = current =>
            {
                calls++;
                return current with { Flags8 = 0x4321 };
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal((short)0x4321, step.Model.SequenceState!.Locals[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B2A1")), new(null, null)));
    }

    [Fact]
    public void B0_uses_the_current_model_particle_producer_and_consumes_its_returned_operands_inline()
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B0FF00C31FE500A1")), new(null, null)
        {
            ScheduleModelParticle = (current, program) =>
            {
                Assert.Equal(3, current.Index);
                Assert.Equal((byte)255, program.Source[program.Offset]);
                return (current with { Flags8 = 0x1234 }, program.Offset + 2);
            },
            ScheduleContactEffect = (_, _) => throw new InvalidDataException("B0 must not publish contact receipts.")
        });
        Assert.Equal((short)0x1234, step.Model.SequenceState!.Locals[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B0FF00A1")), new(null, null)));
    }

    [Fact]
    public void B4_publishes_current_model_to_its_inline_contact_producer_before_following_expression()
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "86B4FF00C31FE500A1")), new(null, null)
        {
            ScheduleContactEffect = (current, program) =>
            {
                Assert.Equal(0x13, current.Flags0);
                Assert.Equal((byte)255, program.Source[program.Offset]);
                return (current with { Flags8 = 0x1234 }, program.Offset + 2);
            }
        });
        Assert.Equal((short)0x1234, step.Model.SequenceState!.Locals[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "B4FF00A1")), new(null, null)));
    }

    [Theory]
    [InlineData("C308D00200E500A1", 0, 0x8001, 0, 16384)]
    [InlineData("C323D00200E500A1", 0, 1, 0x8000, 16384)]
    [InlineData("C31FD00001E500A1", 0x12340000, 1, 0, 0x3400)]
    public void Expression_reads_preserve_each_native_selector_return_width(string program, uint status, ushort resourceFlags, ushort cameraTasks, short expected)
    {
        NativeBattleGraphicsModel model = Model(1) with { Flags8 = status, ResourceFlags44 = resourceFlags };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, Resource((1, program)), new(null, cameraTasks));
        Assert.Equal(expected, step.Model.SequenceState!.Locals[0]);
    }

    [Fact]
    public void External_expression_and_variable_texture_preserve_complete_dword_values()
    {
        List<int> variants = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "9B0010C310D00400E500A1")), new(null, null)
        {
            ReadExternal = variable => variable == 16 ? 70000 : throw new InvalidDataException(),
            ApplyTexture = (_, variant) => variants.Add(variant)
        });
        Assert.Equal(new[] { 70000 }, variants);
        Assert.Equal((short)17500, step.Model.SequenceState!.Locals[0]);
    }

    [Fact]
    public void Target_write_callback_returns_same_model_updates_before_following_expressions()
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "C0FEFFE531C317E500A1")), new(null, null)
        {
            WriteModelExternal = NativeBattleActionGraphics.WriteSequenceTarget5048E0
        });
        Assert.Equal((short)-2, step.Model.Rotation14);
        Assert.Equal((short)4094, step.Model.SequenceState!.Locals[0]);
    }

    [Theory]
    [InlineData("82A1", 0xC4)]
    [InlineData("9CA1", 0x85)]
    public void Common_model_flag_commands_write_distinct_native_bits(string program, int expected)
    {
        NativeBattleGraphicsModel model = Model(1) with { Flags2 = 0x84 };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, Resource((1, program)), new(null, null));
        Assert.Equal((ushort)expected, step.Model.Flags2);
    }

    [Fact]
    public void Position_reset_preserves_current_height_and_all_other_transform_fields()
    {
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(400, 500, 600), 700, 800, 900);
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1) with { Transform = transform }, Resource((1, "95A1")), new(null, null));
        Assert.Equal(transform with { Position28 = new(100, 500, 300) }, step.Model.Transform);
    }

    [Fact]
    public void Variable_texture_command_passes_signed_word_and_continues_after_both_operands()
    {
        List<(byte Selector, int Variant)> copies = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "C0FEFFE5009BFF00C207E501A1")), new(null, null)
        {
            ApplyTexture = (selector, variant) => copies.Add((selector, variant))
        });
        Assert.Equal(new[] { ((byte)255, -2) }, copies);
        Assert.Equal((short)7, step.Model.SequenceState!.Locals[1]);
    }

    [Fact]
    public void BC_consumes_one_unsigned_ignored_operand_before_the_next_opcode()
    {
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "BCFFC207E500A1")), new(null, null));
        Assert.Equal((short)7, step.Model.SequenceState!.Locals[0]);
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "BC")), new(null, null)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public void Sequence_variable_19_reads_the_current_contact_flag_independently_of_header_camera_choice(int cameraChoice, int expected)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 1, 13, (sbyte)(cameraChoice ^ 2), 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, (byte)cameraChoice, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.ActivateHeader(0);
        NativeBattleSequenceInputs inputs = new(null, null)
        {
            ReadExternal = variable =>
            {
                Assert.Equal((byte)25, variable);
                return NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets);
            }
        };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), Resource((1, "C319E500A1")), inputs);
        Assert.Equal((short)expected, step.Model.SequenceState!.Locals[0]);
    }

    [Fact]
    public void A5_reads_one_unsigned_selector_and_dispatches_571840_before_the_next_instruction()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A5FEAAA1"));
        List<string> order = [];
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)
        {
            ScheduleModelEffect = selector => order.Add($"A5:{selector}"),
            ApplyActionContacts = model =>
            {
                order.Add("AA");
                return model;
            }
        });
        Assert.Equal(new[] { "A5:254", "AA" }, order);
        Assert.True(step.Model.SequenceState!.Offset46 >= 3);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)));
    }

    [Fact]
    public void Action_contact_is_published_inline_before_following_status_expression()
    {
        NativeBattleGraphicsResource resource = Resource((1, "AAC31FE500A1"));
        int contacts = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)
        {
            ApplyActionContacts = model =>
            {
                contacts++;
                return model with { Flags8 = 0x1000, SequenceState = model.SequenceState! with { Pending3 = 2 } };
            }
        });
        Assert.Equal(1, contacts);
        Assert.Equal((short)0x1000, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((byte)2, step.Model.SequenceState.Pending3);
        Assert.DoesNotContain(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
    }

    [Fact]
    public void Linked_weapon_decodes_its_own_frame_limit_without_completing_the_body_pose_early()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (2, "00AAA2")) with { AnimationFrames = [3], LinkedAnimationFrames = [1] };
        NativeBattleGraphicsModel model = NativeBattleSequenceMachine.Advance(Model(2), resource, new(null, null)).Model;
        Assert.Equal((byte)1, model.SequenceState!.LinkedPose!.Frame);
        Assert.Equal((byte)1, model.SequenceState.LinkedPose.Limit);
        NativeBattleSequenceStep next = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Equal((byte)2, next.Model.SequenceState!.PoseFrame114);
        Assert.Equal((byte)1, next.Model.SequenceState.LinkedPose!.Frame);
        Assert.DoesNotContain(next.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(next.Model))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(next.Model, resource, new(null, null))), JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(restored, resource, new(null, null))));
    }

    [Fact]
    public void IfritLocalCounterPublishesTextOnceAndResumesFromTheOwnedCursor()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (14, "C100E500BBA000C300E57FC100CB7FEA0791031EE610C300E57FC11ECB7FEA06E60DE602A1C300C501E500E6DAAAA2"));
        NativeBattleGraphicsModel model = Model(14);
        NativeBattleSequenceStep first = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Single(first.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.Text && cue.First == 3 && cue.Second == 30);
        Assert.DoesNotContain(first.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
        model = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(first.Model))!;
        for (int callback = 1; callback <= 30; callback++)
        {
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
            Assert.DoesNotContain(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.Text);
            Assert.Equal(callback == 30, step.Cues.Any(cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback));
            model = step.Model;
        }
        Assert.Equal((byte)1, model.Sequence);
        Assert.Equal((short)30, model.SequenceState!.Locals[0]);
        Assert.NotEqual(0, model.ResourceFlags44!.Value & 0x40);
    }

    [Fact]
    public void EntrySequenceStartsAppearanceAndCompletesOnlyAfterItsSelectedPose()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (8, "A801A2"));
        NativeBattleGraphicsModel model = Model(8);
        NativeBattleSequenceStep entry = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0));
        Assert.Equal((byte)1, entry.Model.Kind5);
        Assert.Equal((byte)0, entry.Model.Phase6);
        Assert.Equal((byte)1, entry.Model.Sequence);
        Assert.Equal((byte)1, entry.Model.SequenceState!.PoseFrame114);
        Assert.NotEqual(0, entry.Model.ResourceFlags44!.Value & 0x40);
        byte completion = 0;
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, Enumerable.Range(0, 7).Select(index => index == 3 ? entry.Model.CompletionInputs() : new NativeBattleGraphicsModelCompletion(0, 0, 0, null)).ToArray(), 0, ref completion));
    }

    [Fact]
    public void PoseWaitUsesOriginalFrameCountAndDoesNotTreatAtbPauseAsACompletedPose()
    {
        NativeBattleGraphicsResource resource = Resource((1, "A300E6FF"), (2, "00AAA2")) with { AnimationFrames = ImmutableArray.Create<byte>(3) };
        NativeBattleGraphicsModel model = Model(2);
        for (int callback = 0; callback < 3; callback++)
        {
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
            Assert.DoesNotContain(step.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
            model = step.Model;
        }
        NativeBattleSequenceStep ended = NativeBattleSequenceMachine.Advance(model, resource, new(null, null));
        Assert.Single(ended.Cues, cue => cue.Kind == NativeBattleSequenceCueKind.ActionCallback);
    }

    [Fact]
    public void NativeExpressionWordStoresAndTemporaryDwordWidthsRemainDistinct()
    {
        NativeBattleGraphicsResource resource = Resource((1, "C0FF7FC501E500E580C380D102E501A1E6FF"));
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null));
        Assert.Equal(short.MinValue, step.Model.SequenceState!.Locals[0]);
        Assert.Equal((short)16384, step.Model.SequenceState.Locals[1]);
    }

    [Fact]
    public void ReachedEntryPredicatesRequireTheirActualOwners()
    {
        NativeBattleGraphicsResource resource = Resource((1, "C322E500A1E6FF"));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceMachine.Advance(Model(1), resource, new(null, null)));
        Assert.Equal((short)4, NativeBattleSequenceMachine.Advance(Model(1), resource, new(4, null)).Model.SequenceState!.Locals[0]);
    }

    private static NativeBattleGraphicsModel Model(byte sequence) => new(3, 3) { ModelId4 = 16, Flags2 = 4, Flags8 = 0, Kind5 = 0, Sequence = sequence, ResourceFlags44 = 1, SequenceState = new() { Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };

    private static NativeBattleGraphicsResource Resource(params (int Id, string Hex)[] programs)
    {
        int count = programs.Max(program => program.Id);
        byte[] bytes = new byte[2 + count * 2 + programs.Sum(program => program.Hex.Length / 2)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)count);
        int cursor = 2 + count * 2;
        foreach ((int id, string hex) in programs)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(id * 2), (ushort)cursor);
            byte[] program = Convert.FromHexString(hex);
            program.CopyTo(bytes, cursor);
            cursor += program.Length;
        }
        return new(bytes.ToImmutableArray()) { AnimationFrames = ImmutableArray.Create<byte>(1) };
    }
}
