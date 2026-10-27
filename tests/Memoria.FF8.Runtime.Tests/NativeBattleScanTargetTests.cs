using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTargetTests
{
    [Theory]
    [InlineData(0, 0x84D4B0u)]
    [InlineData(2, 0x850B40u)]
    [InlineData(4, 0x84D4B0u)]
    public void First_callback_uses_contact_group_stride_after_sound_and_actual_model_updates(byte branchFlags, uint expectedChild)
    {
        var fixture = Fixture(branchFlags);
        List<string> calls = [];
        NativeBattleScanTargetInputs inputs = fixture.Inputs with
        {
            Sound501330 = (current, descriptor, flags, pan) =>
            {
                calls.Add("sound");
                Assert.Equal(0x15D0B3Cu, descriptor);
                Assert.Equal(0u, flags);
                Assert.Equal((byte)128, pan);
                Assert.Equal((ushort)0x1234, Task(current, fixture.Address).ReadWord(0x48));
                return current with { Scan = current.Scan with { Memory = current.Scan.Memory with { SoundPendingFlags = 0x20 } } };
            },
            ReadPoseSource = _ =>
            {
                calls.Add("bounds");
                return fixture.Inputs.ReadPoseSource(6);
            }
        };
        NativeBattleScanTargetStep result = NativeBattleScanTarget.Advance84D230(fixture.Frame, fixture.Address, inputs);
        Assert.Equal(new[] { "sound", "bounds" }, calls);
        Assert.Equal(0, result.Result);
        Assert.Equal(0x20u, result.Frame.Scan.Memory.SoundPendingFlags);
        Assert.Equal((ushort)0, Task(result.Frame, fixture.Address).ReadWord(0x48));
        Assert.Equal((ushort)1, Task(result.Frame, fixture.Address).ReadWord(0x24));
        Assert.Equal((byte)1, Task(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)1, Task(result.Frame, fixture.Address).ReadByte(0x28));
        NativeBattleModulePoolTask child = result.Frame.Scan.Effect.Pools[2].Slots[0];
        Assert.Equal(expectedChild, child.Callback);
        Assert.Equal(fixture.Address, child.ReadDword(0x18));
        Assert.Equal((byte)6, child.ReadByte(0x2D));
        Assert.Equal((byte)1, child.ReadByte(0x2A));
        NativeBattleScanPresentationMemory presentation = result.Frame.Scan.Memory.Scan!.Presentation;
        Assert.Equal((ushort)0, presentation.Result269D938);
        Assert.Equal((ushort)0, presentation.Exit269CE2C);
        Assert.Equal(NativeBattleScanModelSelection.ModelBase + 6u * 156, presentation.PrimaryModel269CE30);
        Assert.Equal(1u, presentation.Selection269A1BC.ReadDword(24));
        Assert.Equal(0u, presentation.Selection269A1BC.ReadDword(0));
    }

    [Fact]
    public void Child_wait_preserves_root_gate_and_completion_uses_header_group_pointer_exactly_once()
    {
        var fixture = Fixture(2);
        int sounds = 0;
        List<int> contacts = [];
        NativeBattleScanTargetInputs inputs = fixture.Inputs with
        {
            Sound501330 = (current, _, _, _) =>
            {
                sounds++;
                return current;
            },
            ApplyContact506690 = (current, contact) =>
            {
                Assert.Equal((byte)0, new NativeBattleEffectHierarchy(current.Scan.Effect.Pools).Read(current.Scan.Effect.Pools[0].BankAddress).ReadByte(0x63));
                contacts.Add(contact);
                return current with { Scan = current.Scan with { Memory = current.Scan.Memory with { SoundPendingFlags = 0x40 } } };
            }
        };
        NativeBattleScanTargetFrame frame = NativeBattleScanTarget.Advance84D230(fixture.Frame, fixture.Address, inputs).Frame;
        NativeBattleScanTargetStep waited = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, inputs);
        Assert.Equal(0, waited.Result);
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(waited.Frame.Scan.Effect.Pools).Read(waited.Frame.Scan.Effect.Pools[0].BankAddress).ReadByte(0x63));
        Assert.Empty(contacts);
        Assert.Equal(1, sounds);
        NativeBattleEffectHierarchy hierarchy = new(waited.Frame.Scan.Effect.Pools);
        NativeBattleEffectHierarchyStep retiredChild = hierarchy.Advance(2, (current, address) => new(current.Complete8DC530(address), 2));
        frame = waited.Frame with { Scan = waited.Frame.Scan with { Effect = waited.Frame.Scan.Effect with { Pools = retiredChild.Hierarchy.Pools } } };
        NativeBattleScanTargetStep completed = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, inputs);
        Assert.Equal(2, completed.Result);
        Assert.Equal(new[] { 2 }, contacts);
        Assert.Equal(1, sounds);
        Assert.Equal(0x40u, completed.Frame.Scan.Memory.SoundPendingFlags);
        Assert.Equal((byte)2, Task(completed.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)1, Task(completed.Frame, fixture.Address).ReadByte(0x26));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(completed.Frame.Scan.Effect.Pools).Read(completed.Frame.Scan.Effect.Pools[0].BankAddress).ReadByte(0x28));
    }

    [Fact]
    public void Negative_signed_group_uses_prior_contact_and_header_without_unsigned_conversion()
    {
        var fixture = Fixture(2);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteByte(0x2A, 0xFF));
        NativeBattleScanTargetFrame frame = fixture.Frame with { Scan = fixture.Frame.Scan with { Effect = fixture.Frame.Scan.Effect with { Parameters = fixture.Frame.Scan.Effect.Parameters! with { HeaderIndex7C = 1 }, Pools = hierarchy.Pools } } };
        NativeBattleScanTargetStep first = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, fixture.Inputs);
        Assert.Equal(0x850B40u, first.Frame.Scan.Effect.Pools[2].Slots[0].Callback);
        Assert.Equal((byte)4, first.Frame.Scan.Effect.Pools[2].Slots[0].ReadByte(0x2D));
        hierarchy = new(first.Frame.Scan.Effect.Pools);
        NativeBattleEffectHierarchyStep retired = hierarchy.Advance(2, (current, address) => new(current.Complete8DC530(address), 2));
        frame = first.Frame with { Scan = first.Frame.Scan with { Effect = first.Frame.Scan.Effect with { Pools = retired.Hierarchy.Pools } } };
        int? contact = null;
        NativeBattleScanTargetStep last = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, fixture.Inputs with
        {
            ApplyContact506690 = (current, index) =>
            {
                contact = index;
                return current;
            }
        });
        Assert.Equal(0, contact);
        Assert.Equal(2, last.Result);
    }

    [Fact]
    public void Independent_target_snapshot_continuation_preserves_real_hierarchy_and_selection()
    {
        var fixture = Fixture(0);
        NativeBattleScanTargetFrame frame = NativeBattleScanTarget.Advance84D230(fixture.Frame, fixture.Address, fixture.Inputs).Frame;
        NativeBattleScanTargetFrame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleScanTargetFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleScanTargetStep left = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, fixture.Inputs);
        NativeBattleScanTargetStep right = NativeBattleScanTarget.Advance84D230(clone, fixture.Address, fixture.Inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(left), NativeBattleTestSnapshots.Json(right));
        Assert.Equal((ushort)2, Task(left.Frame, fixture.Address).ReadWord(0x24));
    }

    [Fact]
    public void Phase_two_is_noop_but_counter_wrap_is_not_blocked_by_a_pause_adapter()
    {
        var fixture = Fixture(0);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, ushort.MaxValue).WriteByte(0x29, 2));
        NativeBattleScanTargetFrame frame = fixture.Frame with { Scan = fixture.Frame.Scan with { Effect = fixture.Frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        NativeBattleScanTargetStep result = NativeBattleScanTarget.Advance84D230(frame, fixture.Address, fixture.Inputs with { Sound501330 = (_, _, _, _) => throw new InvalidDataException() });
        Assert.Equal((ushort)0, Task(result.Frame, fixture.Address).ReadWord(0x24));
        Assert.Equal((byte)2, Task(result.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0, result.Result);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanTargetFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);

    private static (NativeBattleScanTargetFrame Frame, uint Address, NativeBattleScanTargetInputs Inputs) Fixture(byte branchFlags)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(Contact(4, 0));
        (packets, _) = packets.AppendContact(Contact(5, branchFlags));
        packets = packets.WritePlayerRepeatMode(0, 2);
        (packets, _) = packets.Publish(2);
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(Contact(6, 0));
        packets = packets.WritePlayerRepeatMode(1, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Scan = NativeBattleScanProcess.FromColdProcess() };
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(memory, effect, packets);
        NativeBattleEffectHierarchy hierarchy = new(scan.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x2A, 1).WriteByte(0x63, 1));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x84D230, 0x58, root, (_, group, actor) => new(packets.Headers[group].ReadByte(0), packets.ReadContact(packets.Headers[group].FirstContactIndex!.Value + actor).Primary.ActorIndex));
        hierarchy = target.Hierarchy.Write(target.Address, target.Hierarchy.Read(target.Address).WriteWord(0x48, 0x1234));
        scan = scan with { Effect = scan.Effect with { Pools = hierarchy.Pools }, Memory = scan.Memory with { Scan = scan.Memory.Scan! with { Presentation = scan.Memory.Scan!.Presentation with { Result269D938 = 9, Exit269CE2C = 10 } } } };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }).ToImmutableArray();
        ImmutableArray<NativeBattleScanByteOwner> physical = ScanPhysicalModelFixture.Models();
        physical = physical.SetItem(6, physical[6].WriteByte(0, 2));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleScanTargetInputs inputs = new(packets, _ => source, _ => throw new InvalidDataException(), (current, _, _, _) => current, (_, _) => throw new InvalidDataException("Contact is not reached in this fixture callback."));
        return (new(scan, models, physical), target.Address, inputs);
    }

    private static NativeBattleContactPacket Contact(byte actor, byte flags) => new(new(actor, 1, flags, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
}
