using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeCarbuncle278(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleCarbuncle278Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Carbuncle requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Carbuncle lost its selected effect."), nativeCrtRandom ?? throw new NotSupportedException("Native Carbuncle requires its actual CRT random owner."));
        if (initialize)
        {
            int encounter = definition.EncounterId ?? throw new NotSupportedException("Native Carbuncle requires its installed encounter WORD1CFF6E0.");
            frame = NativeBattleCarbuncle278Entry.Initialize680C50(frame, nativeActionPackets, graphics.Models, checked((ushort)encounter));
        }
        else
        {
            NativeBattleCarbuncle278Inputs inputs = new(nativeActionPackets, DrawMirror, StartVibration) { DrawCreature = DrawCreature, PresentCreature = PresentCreature, ModelCreature = ModelCreature, DrawSparkle = DrawSparkle, PresentSparkleWindow = PresentSparkleWindow };
            frame = NativeBattleCarbuncle278Controller.Advance680DF0(frame, (depth, flags) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained four stage groups.");
                stage.Validate();
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)depth), ColorFlags = unchecked((uint)flags) }).ToImmutableArray() };
            }, inputs);

            NativeBattleCarbuncle278Frame DrawMirror(NativeBattleCarbuncle278Frame current, NativeBattleVector plane, NativeBattleEffectLocalBounds bounds)
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Carbuncle mirror requires its current camera owner.");
                _ = NativeBattleCarbuncle278Mirror.Transform6812E0(plane, bounds, camera.FocalLength ?? throw new NotSupportedException("Native Carbuncle mirror requires its signed focal scratch WORD."), camera.Projection ?? throw new NotSupportedException("Native Carbuncle mirror requires its current screen offsets."));
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle mirror requires its current ordering table.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                int cursor = checked((int)(state.PacketCursor81BC - state.ModelBuffer81F8));
                NativeBattleCarbuncle278MirrorWrite written = NativeBattleCarbuncle278Mirror.Append6812E0(current.Memory, primitive.Pending, cursor, graphics.Flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory with { Carbuncle278 = state with { PacketCursor81BC = checked(state.ModelBuffer81F8 + (uint)written.Cursor) } } };
            }

            NativeBattleCarbuncle278Vibration StartVibration(NativeBattleCarbuncle278Frame current)
            {
                NativeVibrationBank bank = current.Effect.Binding.Carbuncle278Resources?.Vibration ?? throw new NotSupportedException("Native Carbuncle requires its original vibration bank.");
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Carbuncle requires its retained vibration process.")).Enqueue(bank, 1, 128);
                NativeVibrationProcess = queued.State;
                return new(current, queued.Handle);
            }

            NativeBattleCarbuncle278Frame DrawCreature(NativeBattleCarbuncle278Frame current, ushort counter)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                byte stage = graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native Carbuncle mirror requires its current stage resource index.");
                if (stage is 0x2C or 0x2D or 0x2E)
                {
                    current = DrawMirror(current, state.MirrorPlane81F0 ?? throw new NotSupportedException("Native Carbuncle mirror requires its retained plane WORDs."), state.Bounds81E0 ?? throw new NotSupportedException("Native Carbuncle mirror requires its current bounds WORDs."));
                    state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                }
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current projection.");
                NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current view matrix.");
                NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, unchecked(state.Base81CC - 200), 4096);
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its current ordering table.");
                NativeBattleStaticMeshResource mesh = current.Effect.Binding.Carbuncle278Resources?.MirrorBoundsMesh ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its original geometry.");
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle mirror mesh requires its actual COMMON packet owner.");
                byte intensity = unchecked((byte)((unchecked((int)(4096 - state.Flash81D4)) << 7) >> 12));
                Func<int, NativeBattleVector>? readVertices = null;
                int ratio = 4096;
                if (counter is >= 16 and < 36)
                {
                    NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources!;
                    if (resources.MirrorAlternateVertices.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Carbuncle mirror morph requires its original alternate vertex source.");
                    }
                    ratio = (counter - 16) * 4096 / 20;
                    current = current with { Memory = NativeBattleCarbuncle278Morph.Morph683CA0(current.Memory, mesh, resources.MirrorAlternateVertices, ratio) };
                    readVertices = offset =>
                    {
                        byte[] vertex = current.Memory.Arena!.Read(checked(0x2E84 + offset), 6);
                        return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.AsSpan(4)));
                    };
                }
                if (counter >= 36)
                {
                    System.Collections.Immutable.ImmutableArray<byte> vertices = current.Effect.Binding.Carbuncle278Resources!.MirrorAlternateVertices;
                    if (vertices.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Carbuncle mirror requires its original second vertex shape.");
                    }
                    readVertices = offset =>
                    {
                        ReadOnlySpan<byte> vertex = vertices.AsSpan().Slice(offset, 6);
                        return new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                    };
                }
                NativeBattleCommonPacketWrite written = NativeBattleCarbuncle278MirrorMesh.Append6839B0(process, primitive.Pending, mesh, matrix, projection, intensity, readVertices);
                if (counter >= 16)
                {
                    NativeBattleStaticMeshResource secondary = current.Effect.Binding.Carbuncle278Resources?.SecondaryMesh ?? throw new NotSupportedException("Native Carbuncle mirror requires its original secondary mesh.");
                    short scale = unchecked((short)ratio);
                    NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Compose56C2F0(view, local with { Rotation = [scale, 0, 0, 0, scale, 0, 0, 0, scale] });
                    NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(secondary.Bytes, 0, secondary.Bytes.Slice(8, secondary.Bytes.Length - 8), scaled, projection, new(0x2000, null, null, null, null, null, 17, 2));
                    written = NativeBattleCommonPacketArena.Append(written.Process, written.Ordering, batch);
                }
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            NativeBattleCarbuncle278ModelStep ModelCreature(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleEffectOwnedModelResource resource = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle requires its original creature model.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                if (state.Pause8114 == 0)
                {
                    NativeBattleCarbuncle278CreaturePose advanced = NativeBattleCarbuncle278Model.Advance6FBDB0(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle requires its current executable skeleton."), resource);
                    task = advanced.Task;
                    current = current with { Memory = current.Memory with { CarbuncleExecutableMemory = advanced.Executable } };
                }
                bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current hardware capability.");
                NativeBattleCarbuncle278ModelDraw draw = NativeBattleCarbuncle278Model.Draw6A79B0(current.Memory, task, resource, NativeCamera?.Projection ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current projection."), hardware);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle model drawing requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(draw.Memory.PrimitiveProcess!, primitive.Pending, draw.Packets);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(current with { Memory = draw.Memory with { PrimitiveProcess = written.Process } }, task);
            }

            NativeBattleCarbuncle278Frame DrawSparkle(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                NativeBattleModuleResource resource = current.Effect.Binding.Carbuncle278Resources?.SparkleSprite ?? throw new NotSupportedException("Native Carbuncle requires its original sparkle sprite resource.");
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Carbuncle sparkles require their current camera.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Carbuncle sparkles require their current projection.");
                NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(NativeBattleCarbuncle278Sparkles.Draw683E80(task, resource), projection, camera.Roll, 17, 2);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Carbuncle sparkles require their current ordering table.");
                int cursor = checked((int)(state.PacketCursor81BC - state.ModelBuffer81F8));
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory with { Carbuncle278 = state with { PacketCursor81BC = checked(state.ModelBuffer81F8 + (uint)written.Cursor) } } };
            }

            NativeBattleCarbuncle278Frame PresentSparkleWindow(NativeBattleCarbuncle278Frame current, NativeBattleModulePoolTask task)
            {
                ushort counter = task.ReadWord(0xC);
                if (counter == 86)
                {
                    System.Collections.Immutable.ImmutableArray<uint> sounds = current.Effect.Binding.Carbuncle278Resources?.Sounds ?? [];
                    if (sounds.Length < 2)
                    {
                        throw new NotSupportedException("Native Carbuncle requires its second original sound descriptor.");
                    }
                    PublishNativeModuleSound(new(sounds[1], 0, null) { Pan = 128 });
                }
                current = NativeBattleCarbuncle278Sparkles.Spawn681B3F(current, task);
                NativeBattleVector? target = null;
                if (counter < 92)
                {
                    NativeBattleEffectOwnedModelResource model = current.Effect.Binding.Carbuncle278Resources?.CreatureModel ?? throw new NotSupportedException("Native Carbuncle camera requires its original creature model.");
                    target = NativeBattleCarbuncle278Anchors.ReadVertex682270(task, current.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle camera requires its current creature skeleton."), model, 0, 176);
                }
                NativeCamera = NativeBattleCarbuncle278Camera.AdvanceSparkleWindow(NativeCamera ?? throw new NotSupportedException("Native Carbuncle camera requires its current pose."), counter, target);
                return current;
            }

            NativeBattleCarbuncle278Frame PresentCreature(NativeBattleCarbuncle278Frame current, ushort counter)
            {
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(current.Memory);
                if (counter == 1)
                {
                    NativeBattleCarbuncle278Resources resources = current.Effect.Binding.Carbuncle278Resources ?? throw new NotSupportedException("Native Carbuncle requires its original sound resources.");
                    if (resources.Sounds.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Carbuncle requires its first original sound descriptor.");
                    }
                    PublishNativeModuleSound(new(resources.Sounds[0], 0, null) { Pan = 128 });
                }
                if (counter == 2)
                {
                    graphics = graphics with { Models = graphics.Models.Select(model => UpdateModel(model)).ToImmutableArray() };
                }
                if (counter <= 36)
                {
                    NativeCamera = NativeBattleCarbuncle278Camera.AdvanceFirstWindow(NativeCamera ?? throw new NotSupportedException("Native Carbuncle requires its current camera globals."), state.Base81CC, counter);
                }
                return current;

                NativeBattleGraphicsModel UpdateModel(NativeBattleGraphicsModel model)
                {
                    if (model.Index < 3)
                    {
                        return model with { Flags0 = (ushort)(model.Flags0 | 4) };
                    }
                    if ((model.Flags0 & 2) == 0)
                    {
                        return model;
                    }
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Carbuncle lift requires its selected actor's position WORD20.");
                    return model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + state.Lift81D0)) } } };
                }
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
