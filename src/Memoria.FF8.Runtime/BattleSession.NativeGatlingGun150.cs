using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeGatlingGun150(NativeBattleGraphicsState graphics) => RunNativeGatlingGun150(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeGatlingGun150(NativeBattleGraphicsState graphics) => RunNativeGatlingGun150(graphics, false);

    private NativeBattleGraphicsState RunNativeGatlingGun150(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Gatling Gun requires its selected original effect.");
        NativeBattleGatlingGun150Resources resources = effect.Binding.GatlingGun150Resources ?? throw new NotSupportedException("Gatling Gun requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Gatling Gun requires its actual process memory.");
        NativeBattleGatlingGun150Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Gatling Gun requires its CRT stream."), memory.GatlingGun150 ?? throw new NotSupportedException("Gatling Gun requires its retained native globals.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Gatling Gun requires its addressed file and temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Gatling Gun requires its original interpolation constants.");
        NativeBattleGatlingGun150Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), (_, actor) => graphics.Models[actor], Bone, Anchor, Execute);
        frame = initialize ? NativeBattleGatlingGun150Controller.Initialize(frame, resources, inputs) : NativeBattleGatlingGun150Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Gatling Gun requires its current camera.");

        void Publish(NativeBattleGatlingGun150Frame current)
        {
            NativeEffectMemory = current.Memory with { GatlingGun150 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gatling Gun lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleGatlingGun150Frame Import(NativeBattleGatlingGun150Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Gatling Gun external service lost its process memory.");
            return current with { Memory = latest, State = latest.GatlingGun150 ?? throw new InvalidDataException("Gatling Gun external service lost its native globals."), Staging = NativeFileStaging ?? throw new InvalidDataException("Gatling Gun external service lost its addressed memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Gatling Gun external service lost its CRT stream.") };
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleGatlingGun150Frame Deliver(NativeBattleGatlingGun150Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Gatling Gun requires its pending ordering table.");
            NativeBattleGatlingGun150PacketWrite written = NativeBattleGatlingGun150PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleGatlingGun150PointStep Anchor(NativeBattleGatlingGun150Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gatling Gun requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, Point(new(anchor.X, anchor.Y, anchor.Z)).Write(6, BitConverter.GetBytes((model.Transform ?? throw new NotSupportedException("Gatling Gun requires its actual actor heading.")).Angle36)));
        }

        NativeBattleGatlingGun150PointStep Bone(NativeBattleGatlingGun150Frame current, int actor, int selector, int distance)
        {
            Publish(current);
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Gatling Gun requires its selected current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Gatling Gun bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleGatlingGun150Frame Execute(NativeBattleGatlingGun150Frame current, NativeBattleGatlingGun150Operation operation)
        {
            switch (operation)
            {
                case NativeBattleGatlingGun150Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleGatlingGun150ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleGatlingGun150DrawStatic88 draw:
                    return DrawMesh(current, draw.TemporaryAddress, draw.SourceAddress, draw.TableBaseBucket, unchecked((byte)(draw.DepthShift & 31)), draw.PilotIdentity);
                case NativeBattleGatlingGun150DrawSprite180 draw:
                    return DrawSprite(current, draw.TemporaryAddress, draw.SourceAddress, draw.TableBaseBucket, unchecked((byte)(draw.DepthShift & 31)), draw.PilotIdentity);
                default:
                    throw new NotSupportedException("Gatling Gun operation requires its original native owner.");
            }
        }

        NativeBattleGatlingGun150Frame DrawMesh(NativeBattleGatlingGun150Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            const int length = 88;
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gatling Gun requires its physical drawing context.");
            NativeBattleEffectArena temporary = NativeGatlingGun150View(staging, address, length);
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleCameraProjection projection = Camera().Projection ?? throw new NotSupportedException("Gatling Gun requires its published projection.");
            NativeBattleGeometryRegisters registers = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Gatling Gun requires its current matrix owner.");
            NativeBattleActionMatrix matrix = NativeBattleGeometry.CaptureMatrix(registers);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(current.Memory, mesh, matrix, projection, temporary, colors, matrixPadding18: registers.Rotation20[18] is byte low && registers.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null, tableBaseBucket: bucket, depthShift: shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, length) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleGatlingGun150Frame DrawSprite(NativeBattleGatlingGun150Frame current, uint address, uint sourceAddress, int bucket, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Gatling Gun sprite requires its physical temporary memory.");
            NativeBattleEffectArena temporary = NativeGatlingGun150View(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != resource.Address)
            {
                throw new InvalidDataException("Gatling Gun sprite differs from its current physical frame.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => resources.ReadSprite(selected), Camera().Projection ?? throw new NotSupportedException("Gatling Gun sprite requires its current projection."), Camera().Roll, bucket, depthShift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }
    }

    private static NativeBattleEffectArena NativeGatlingGun150View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
