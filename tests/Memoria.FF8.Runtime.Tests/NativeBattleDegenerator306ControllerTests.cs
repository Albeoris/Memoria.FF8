using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306ControllerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, PacketBankBytes)]
    public void Root_uses_old_age_for_bank_selection_while_paused_main_stays_unadvanced(int age, int bank)
    {
        Fixture fixture = new();
        NativeBattleDegenerator306Frame frame = NativeBattleDegenerator306Controller.Write(fixture.Frame, RootBank, NativeBattleDegenerator306Controller.Read(fixture.Frame, RootBank).WriteWord(12, (ushort)age));
        NativeBattleDegenerator306Frame result = fixture.Step(frame, 0x201);
        Assert.Equal(ArenaAddress + (uint)bank, result.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)(age + 1), NativeBattleDegenerator306Controller.Read(result, RootBank).ReadWord(12));
        Assert.Equal((ushort)0, NativeBattleDegenerator306Controller.Read(result, ChildBank).ReadWord(12));
        Assert.Empty(fixture.Operations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Required_service_cycle_preserves_MISS_policy_and_waits_for_real_completion_flag(bool miss)
    {
        Fixture fixture = new(miss);
        NativeBattleDegenerator306Frame frame = fixture.Frame;
        for (int callback = 0; callback < 150; callback++)
        {
            frame = fixture.Step(frame);
        }
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.NotNull(fixture.CompletionAddress);
        Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306CommonTexture>());
        Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306Contact>());
        Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306Sound>());
        Assert.Equal(15, fixture.Operations.OfType<NativeBattleDegenerator306DrawSprite180>().Count(draw => draw.SourceAddress == 0xCE60C0));
        Assert.All(fixture.Operations.OfType<NativeBattleDegenerator306DrawSprite180>().Where(draw => draw.SourceAddress == 0xCE60C0), draw => Assert.Equal((byte)14, draw.DepthShift));
        Assert.Equal(miss ? 0 : 30, fixture.Operations.OfType<NativeBattleDegenerator306DrawBody136>().Count());
        Assert.Contains(fixture.Operations.OfType<NativeBattleDegenerator306DrawStatic88>(), draw => draw.SourceAddress == 0xCE643C && draw.TableBaseBucket == 4122 && draw.DepthShift == 14);
        Assert.Contains(new NativeBattleDegenerator306StageFlags(2, 0), fixture.Operations);
        Assert.Contains(new NativeBattleDegenerator306StageFlags(0, 2), fixture.Operations);
        frame = NativeBattleDegenerator306Controller.WriteCompletionByte(frame, fixture.CompletionAddress!.Value, 1);
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleVector position = fixture.Models[3].Transform!.Position28;
        Assert.Equal(new short[] { 0, 0, 0 }, new[] { position.X, position.Y, position.Z });
    }

    [Fact]
    public void Matrix_wrapper_draws_before_pause_and_preserves_geometry_from_actual_camera_policy()
    {
        Fixture fixture = new();
        NativeBattleDegenerator306Frame frame = fixture.Frame;
        (frame, uint address) = NativeBattleDegenerator306Controller.Append(frame, 0x5A3840);
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 7).WriteWord(0x18, 512).WriteWord(0x1A, 16).WriteWord(0x1C, 1024);
        frame = NativeBattleDegenerator306Controller.Write(frame, address, task);
        NativeBattleDegenerator306Frame result = fixture.Step(frame, 0x201);
        NativeBattleDegenerator306DrawStatic88 draw = Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306DrawStatic88>());
        Assert.Equal(4122, draw.TableBaseBucket);
        Assert.Equal(14, draw.DepthShift);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.ToArray(), NativeBattleStorageSnapshots.Capture(NativeBattleDegenerator306Controller.Read(result, address)).Payload.ToArray());
        Assert.Equal(new int?[] { 0, 0, 777 }, result.Memory.GeometryRegisters!.Translation);
    }

    [Fact]
    public void Camera_sprite_overwrites_retained_nonzero_physical_phase_even_when_paused()
    {
        Fixture fixture = new();
        NativeBattleDegenerator306Frame frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 4, BitConverter.GetBytes((ushort)0xBEEF)) };
        (frame, uint address) = NativeBattleDegenerator306Controller.Append(frame, 0x5A3710);
        frame = NativeBattleDegenerator306Controller.Write(frame, address, NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 1));
        NativeBattleDegenerator306Frame result = fixture.Step(frame, 0x201);
        NativeBattleDegenerator306DrawSprite180 draw = Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306DrawSprite180>());
        Assert.Equal((ushort)0, draw.Phase);
        Assert.Equal((byte)14, draw.DepthShift);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging!.Read(draw.TemporaryAddress + 4, 2)));
        Assert.Equal((ushort)1, NativeBattleDegenerator306Controller.Read(result, address).ReadWord(12));
    }

    [Fact]
    public void Saved_active_controller_recreates_the_same_next_callbacks_and_partial_state()
    {
        Fixture first = new();
        NativeBattleDegenerator306Frame frame = first.Frame;
        for (int callback = 0; callback < 77; callback++)
        {
            frame = first.Step(frame);
        }
        NativeBattleDegenerator306Frame restored = NativeBattleTestSnapshots.Independent(frame);
        Fixture second = first.CopyModels();
        first.Operations.Clear();
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Step(frame)), NativeBattleTestSnapshots.Json(second.Step(restored)));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Operations.ToImmutableArray()), NativeBattleTestSnapshots.Json(second.Operations.ToImmutableArray()));
    }

    private sealed class Fixture
    {
        public NativeBattleDegenerator306Frame Frame { get; }
        public List<NativeBattleDegenerator306Operation> Operations { get; } = [];
        public NativeBattleGraphicsModel[] Models { get; }
        public uint? CompletionAddress { get; private set; }
        private NativeBattleActionPacketsState Packets { get; }
        private NativeBattleDegenerator306Resources Resources { get; }
        public Fixture(bool miss = false)
        {
            NativeBattleActionPacketsState packets = OriginalPackets();
            (packets, _) = packets.Prepare(new(OriginalOwner(0), 2, 4, 0, 0, 306, null));
            (packets, _) = packets.AppendContact(new(new(OriginalOwner(3), 1, 0, miss ? (byte)4 : (byte)0, 0, 0, 0), null));
            packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), 1);
            (packets, _) = packets.Publish(1);
            Packets = packets;
            Models = Enumerable.Range(0, 7).Select(actor => new NativeBattleGraphicsModel(actor is 0 or 3 ? (ushort)2 : (ushort)0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770(), Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, HasLinkedModelRing140 = false }).ToArray();
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
            (root, _) = root.Append(RootCallback);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, _) = children.Append(MainCallback);
            NativeBattleOriginalEffectState effect = new(306, 306, new(306, Entry, Initializer), new(OriginalOwner(0), 1, BattleModelTargets.From([OriginalOwner(3)]), new BattleActionHeaderReference(0), 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(306) with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            Frame = new(memory, effect, new(1), NativeBattleDegenerator306State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteModelOperand(TargetGlobal, OriginalOwner(3), OriginalAliases).WriteModelOperand(CasterGlobal, OriginalOwner(0), OriginalAliases)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Resources = new(new("fixture", []), new("fixture", []), Mesh(0xCEAB94), Mesh(0xCEAF2C), Mesh(0xCE643C), Mesh(0xCEA2E4), Mesh(0xCE7A2C), new(0, 0xCE60C0, 0, 0, []), new(1, 0xCE62A8, 0, 0, []), new uint[12].ToImmutableArray(), 0x55775);
        }
        public Fixture CopyModels()
        {
            Fixture fixture = new();
            Models.CopyTo(fixture.Models, 0);
            return fixture;
        }
        public NativeBattleDegenerator306Frame Step(NativeBattleDegenerator306Frame frame, uint flags = 0) => NativeBattleDegenerator306Controller.Advance(frame, Resources, new(flags, Packets, actor => Models[OriginalSlot(actor)], _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), () => 777, actor => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(Models[OriginalSlot(actor)].Rotation12!.Value)).Write(2, BitConverter.GetBytes(Models[OriginalSlot(actor)].Rotation14!.Value)).Write(4, BitConverter.GetBytes(Models[OriginalSlot(actor)].Rotation16!.Value)).MarkUnknown(6, 2), (current, _, selector, _) => new(current, new((short)selector, 0, 1000)), (current, _) => new(current, new(0, 0, 0, 0)), Execute));
        private NativeBattleDegenerator306Frame Execute(NativeBattleDegenerator306Frame frame, NativeBattleDegenerator306Operation operation)
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleDegenerator306CommonTexture load:
                    CompletionAddress = load.CompletionAddress;
                    return NativeBattleDegenerator306Controller.WriteCompletionByte(frame, load.CompletionAddress, 0);
                case NativeBattleDegenerator306DrawSprite180 draw:
                    Assert.Equal(draw.Phase, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(draw.TemporaryAddress + 4, 2)));
                    return frame with { Staging = frame.Staging!.Write(draw.TemporaryAddress + 0x28, BitConverter.GetBytes(draw.SourceAddress == 0xCE62A8 && draw.Phase >= 14 ? (short)-1 : (short)0)) };
                case NativeBattleDegenerator306ActorFlags update:
                    Models[OriginalSlot(update.Actor)] = Models[OriginalSlot(update.Actor)] with { Flags0 = (ushort)((Models[OriginalSlot(update.Actor)].Flags0 & ~update.ClearMask) | update.SetMask) };
                    break;
                case NativeBattleDegenerator306ActorPosition position:
                    Models[OriginalSlot(position.Actor)] = Models[OriginalSlot(position.Actor)] with { Transform = Models[OriginalSlot(position.Actor)].Transform! with { Position28 = position.Position } };
                    break;
                case NativeBattleDegenerator306ActorPackedPosition8 position:
                    byte[] bytes = position.Packed8.Read(0, 8);
                    Models[OriginalSlot(position.Actor)] = Models[OriginalSlot(position.Actor)] with { Transform = Models[OriginalSlot(position.Actor)].Transform! with { Position28 = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), Vertical34 = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) } };
                    break;
                case NativeBattleDegenerator306ActorRotation8 rotation:
                    Models[OriginalSlot(rotation.Actor)] = Models[OriginalSlot(rotation.Actor)] with { Rotation12 = BinaryPrimitives.ReadInt16LittleEndian(rotation.Packed8.Read(0, 2)), Rotation14 = BinaryPrimitives.ReadInt16LittleEndian(rotation.Packed8.Read(2, 2)), Rotation16 = BinaryPrimitives.ReadInt16LittleEndian(rotation.Packed8.Read(4, 2)) };
                    break;
            }
            return frame;
        }
        private static NativeBattleStaticMeshResource Mesh(uint address) => new(0, address, []);
    }
}
