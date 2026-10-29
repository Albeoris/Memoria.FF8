using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect183(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect183 requires its selected original effect.");
        NativeBattleEffect183Resources resources = effect.Binding.Effect183Resources ?? throw new NotSupportedException("Effect183 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect183 requires its current process memory.");
        NativeBattleEffect183Frame frame = new(memory, effect, memory.Effect183 ?? throw new NotSupportedException("Effect183 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect183 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect183 requires its color arithmetic.");
        NativeBattleEffect183Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect183 requires its projection."), Bone, actor => (graphics.Models[actor].Transform ?? throw new NotSupportedException("Effect183 requires its actor heading.")).Angle36, Anchor, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect183Controller.Initialize(frame, resources, inputs) : NativeBattleEffect183Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect183 requires its current camera.");

        void Publish(NativeBattleEffect183Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect183 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect183 lost its temporary memory.");
        }

        NativeBattleEffect183Frame Import(NativeBattleEffect183Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect183 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect183 ?? throw new InvalidDataException("Effect183 external service lost its retained storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect183 external service lost its temporary memory.") };
        }

        NativeBattleEffect183PointStep Bone(NativeBattleEffect183Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect183 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect183 bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleEffect183PointStep Anchor(NativeBattleEffect183Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect183 requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            short heading = (model.Transform ?? throw new NotSupportedException("Effect183 requires its actual actor heading.")).Angle36;
            return new(current, Point(new(anchor.X, anchor.Y, anchor.Z)).Write(6, BitConverter.GetBytes(heading)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleEffect183Frame Execute(NativeBattleEffect183Frame current, NativeBattleEffect183Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect183Sound sound:
                    Publish(current);
                    byte[] point = sound.Anchor8.Read(0, 8);
                    PublishNativeModuleSound(sound.Packed, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6))));
                    return Import(current);
                case NativeBattleEffect183ContactFirst contact:
                    Publish(current);
                    int first = nativeActionPackets.Headers[contact.HeaderIndex].FirstContactIndex ?? throw new NotSupportedException("Effect183 requires its first captured contact.");
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(first)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect183DrawWindowed144 draw:
                    return Draw(current, draw.TemporaryAddress, draw.SourceAddress, draw.PilotIdentity);
                default:
                    throw new NotSupportedException("Effect183 emitted an unsupported native operation.");
            }
        }

        NativeBattleEffect183Frame Draw(NativeBattleEffect183Frame current, uint address, uint sourceAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleEffectArena temporary = NativeBattleEffect183Controller.Context(current, address, 144);
            if (Dword(0) != sourceAddress)
            {
                throw new InvalidDataException("Effect183 mesh arguments differ from its physical temporary.");
            }
            uint flags = Dword(32);
            NativeBattleHoly175WindowedMeshContext context = new(flags, Dword(8), (flags & 0xC0) != 0 ? unchecked((int)Dword(12)) : null, Dword(36), unchecked((int)Dword(16)), unchecked((int)Dword(20)), new(Word(24), Word(26), Word(28), Word(30)), 17, 2);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect183 requires its current geometry registers.");
            NativeBattleGfGeometryBatch batch = NativeBattleHoly175WindowedMeshPackets.EmitBatch650720(resources.Mesh(sourceAddress).Bytes, NativeBattleGeometry.CaptureMatrix(geometry), Camera().Projection ?? throw new NotSupportedException("Effect183 requires its current projection."), context, colors, pilotIdentity: pilotIdentity);
            batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } capturedGeometry } captured ? attempt with { Primitive = captured with { PilotGeometry = capturedGeometry with { UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero } } } : attempt).ToImmutableArray() };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect183 requires its pending ordering table.");
            uint physicalCursor = current.State.ReadDword(0x230421C);
            int cursor = checked((int)(physicalCursor - NativeBattleEffect183Profile.PacketBase));
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(current.State.Packets, "22F421C", cursor, NativeBattleEffect183Profile.PacketBytes, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = (current.State with { Packets = written.Arena }).WriteDword(0x230421C, checked(NativeBattleEffect183Profile.PacketBase + (uint)written.Cursor)) };

            uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(offset, 4));
            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(temporary.Read(offset, 2));
        }
    }
}
