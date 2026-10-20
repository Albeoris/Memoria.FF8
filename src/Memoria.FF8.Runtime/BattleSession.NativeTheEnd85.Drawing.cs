using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTheEnd85Runner
    {
        private int DrawTask(uint address, uint callback, short parameter)
        {
            if (callback == 0x788C20)
            {
                frame = NativeBattleTheEnd85GroundInitialization.Initialize788C20(frame, address, NodeServices());
                return 0;
            }
            if (callback == 0x789700)
            {
                NativeBattleTheEnd85NodeStep nodes = NativeBattleTheEnd85GroundInitialization.Advance789700(frame, address, NodeServices());
                frame = nodes.Frame;
                return nodes.Complete ? 1 : 0;
            }
            NativeBattleModulePoolTask task = Task(address);
            NativeBattleTheEnd85DrawServices services = new(Read, Write);
            NativeBattleTheEnd85DrawingStep drawn = callback switch
            {
                0x785660 => NativeBattleTheEnd85Drawing.Advance785660(task, Program(task.ReadDword(116)), Projection, Colors, Staging, State.Dword(0x25BC894), services),
                0x788700 => NativeBattleTheEnd85Drawing.Draw788700(task, Projection, Colors, Staging, services),
                0x78C530 => NativeBattleTheEnd85Drawing.Draw78C530(task, parameter, Projection, Colors, Staging, services),
                0x78D2E0 => NativeBattleTheEnd85Drawing.Draw78D2E0(task, Staging),
                0x78D4D0 => NativeBattleTheEnd85Drawing.Draw78D4D0(task, parameter, Staging),
                _ => throw new NotSupportedException("Native The End selected an unsupported drawing callback.")
            };
            Staging = drawn.Staging;
            Put(address, drawn.Task);
            AppendCommon(drawn.Packets, drawn.LinkFlags);
            FarColor(drawn.PublishedFarColor, drawn.FarRed, drawn.FarGreen, drawn.FarBlue);
            return drawn.Remaining;
        }

        private NativeBattleTheEnd85NodeServices NodeServices() => new(resources, owner.nativeActionPackets, ReadModel, ReadPose, Camera.CapturePackedView1D97778().ReadMatrix(), DrawNode);

        private NativeBattleTheEnd85Frame DrawNode(NativeBattleTheEnd85Frame current, uint node, uint model, uint callback)
        {
            frame = current;
            int size = callback == 0x7897F0 ? 104 : callback == 0x789A10 ? 180 : throw new NotSupportedException("Native The End node drawing callback is unknown.");
            uint cursor = State.Dword(0x25C21CC);
            if (cursor < NativeBattleEffectNodeContextMemory.Address + size || cursor > NativeBattleEffectNodeContextMemory.Address + NativeBattleEffectNodeContextMemory.Length)
            {
                throw new InvalidDataException("Native The End downward drawing context leaves its physical shared memory.");
            }
            uint temporary = cursor - (uint)size;
            State = State.WriteDword(0x25C21CC, temporary);
            Staging = NativeBattleTemporaryMemory.PublishView(Staging, NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Capture(frame.Memory), NativeBattleEffectNodeContextMemory.Length);
            NativeBattleTheEnd85DrawServices services = new(Read, Write);
            NativeBattleTheEnd85NodeDrawingStep drawn = callback == 0x7897F0
                ? NativeBattleTheEnd85NodeDrawing.Draw7897F0(Staging, temporary, node, model, Projection, Colors, services)
                : NativeBattleTheEnd85NodeDrawing.Draw789A10(Staging, temporary, node, Projection, Camera.Roll, services);
            Staging = drawn.Staging;
            NativeBattleEffectArena observed = NativeBattleTemporaryMemory.ReadView(Staging, NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Length);
            frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, observed) };
            Staging = Staging.MarkUnknown(NativeBattleEffectNodeContextMemory.Address, NativeBattleEffectNodeContextMemory.Length);
            State = State.WriteDword(0x25C21CC, checked(temporary + (uint)size));
            if (callback == 0x789A10)
            {
                AppendPrivate(drawn.Packets);
            }
            else
            {
                AppendCommon(drawn.Packets);
            }
            FarColor(drawn.PublishedFarColor, drawn.FarRed, drawn.FarGreen, drawn.FarBlue);
            return frame;
        }

        private void AppendCommon(NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native The End drawing requires its ordering table.");
            NativeBattleCommonPacketWrite result = NativeBattleCommonPacketArena.Append(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native The End requires its COMMON packet owner."), primitive.Pending, batch, flags20);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = result.Process } };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = result.Ordering } };
        }

        private void AppendPrivate(NativeBattleGfGeometryBatch batch)
        {
            uint cursor = State.Dword(0x25BC394);
            if (cursor < 0x20DFAB8 || cursor > 0x21DFAB8)
            {
                throw new InvalidDataException("Native The End private packet cursor leaves its actual arena.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native The End private drawing requires its ordering table.");
            NativeBattleEffectPacketWrite result = NativeBattleEffectPacketArena.Append(frame.Memory, checked((int)(cursor - 0x20DFAB8)), primitive.Pending, batch);
            frame = frame with { Memory = result.Memory, Effect = frame.Effect with { DrawArenaOffset = result.Cursor } };
            State = State.WriteDword(0x25BC394, checked(0x20DFAB8u + (uint)result.Cursor));
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = result.Ordering } };
        }

        private void FarColor(bool published, uint? red, uint? green, uint? blue)
        {
            if (!published)
            {
                return;
            }
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native The End color publication requires its retained renderer process.");
            NativeBattleModelRendererRegisters old = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = old with { FarRed = red, FarGreen = green, FarBlue = blue } } } };
        }

        private void AppendShadows()
        {
            NativeBattleGraphicsDefinition definition = owner.definition.NativeGraphics ?? throw new NotSupportedException("Native The End shadows require their current graphics definition.");
            bool gouraud = (definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native The End shadows require their current device capability profile.")).GouraudAlpha;
            NativeBattleShadowSource source = definition.Rules.ShadowSource ?? throw new NotSupportedException("Native The End shadows require their original matrix/vector seeds.");
            for (int actor = 3; actor < 7; actor++)
            {
                NativeBattleGraphicsModel model = graphics.Models[actor];
                if ((model.Flags0 & 2) == 0)
                {
                    continue;
                }
                NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, model.GeometryBounds52 ?? throw new NotSupportedException("Native The End shadow requires its retained model bounds."), Projection.View ?? throw new NotSupportedException("Native The End shadow requires its current view."), source, gouraud);
                NativeBattleModuleProjectedVertex[] vertices = shadow.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, vertex, Projection)).ToArray();
                AppendPrivate(NativeBattleShadowPackets.EmitBatch5088A0(vertices, model.Color44 ?? throw new NotSupportedException("Native The End shadow requires its current model color."), gouraud, 17, 4));
            }
        }

        private NativeBattleGfAnimationSource Program(uint address)
        {
            if (address >= NativeBattleTheEnd85GroundInitialization.ModelBankAddress && (ulong)address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress < NativeBattleTheEnd85GroundInitialization.ModelBankLength)
            {
                ImmutableArray<byte> bytes = State.ModelData ?? resources.Read(NativeBattleTheEnd85GroundInitialization.ModelBankAddress, NativeBattleTheEnd85GroundInitialization.ModelBankLength).ToImmutableArray();
                return new(address, bytes.AsSpan().Slice(checked((int)(address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress))).ToArray().ToImmutableArray());
            }
            return resources.Program(address);
        }

        private byte[] Read(uint address, int length)
        {
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                if (address >= pool.BankAddress && (ulong)address - pool.BankAddress + (uint)length <= (uint)(pool.Stride * pool.Slots.Length))
                {
                    uint relative = address - pool.BankAddress;
                    int slot = checked((int)(relative / pool.Stride));
                    int offset = checked((int)(relative % pool.Stride));
                    if (offset < 12 || length > pool.Stride - offset)
                    {
                        throw new NotSupportedException("Native The End raw task read crosses physical headers or slots.");
                    }
                    NativeBattleModulePoolTask task = pool.Slots[slot];
                    task.RequireKnownPayload(offset, length);
                    return task.Payload.AsSpan().Slice(offset - 12, length).ToArray();
                }
            }
            if (address >= 0x20DFAB8 && (ulong)address - 0x20DFAB8 + (uint)length <= NativeBattleEffectArena.Length)
            {
                return (frame.Memory.Arena ?? throw new NotSupportedException("Native The End requires its physical arena.")).Read(checked((int)(address - 0x20DFAB8)), length);
            }
            if (address >= NativeBattleTheEnd85State.GlobalAddress && (ulong)address - NativeBattleTheEnd85State.GlobalAddress + (uint)length <= NativeBattleTheEnd85State.GlobalLength)
            {
                return State.Read(address, length);
            }
            if (State.ModelData is { } data && address >= NativeBattleTheEnd85GroundInitialization.ModelBankAddress && (ulong)address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress + (uint)length <= (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress)), length).ToArray();
            }
            return resources.Read(address, length);
        }

        private void Write(uint address, ImmutableArray<byte> bytes)
        {
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                if (address >= pool.BankAddress && (ulong)address - pool.BankAddress + (uint)bytes.Length <= (uint)(pool.Stride * pool.Slots.Length))
                {
                    uint relative = address - pool.BankAddress;
                    int offset = checked((int)(relative % pool.Stride));
                    if (offset < 12 || bytes.Length > pool.Stride - offset)
                    {
                        throw new NotSupportedException("Native The End raw task write crosses physical headers or slots.");
                    }
                    uint taskAddress = address - (uint)offset;
                    NativeBattleModulePoolTask task = Task(taskAddress);
                    byte[] payload = task.Payload.ToArray();
                    bytes.AsSpan().CopyTo(payload.AsSpan(offset - 12));
                    Put(taskAddress, (task with { Payload = payload.ToImmutableArray() }).WithoutUnknownPayload(offset, bytes.Length).WithoutResourcePointers(offset, bytes.Length));
                    return;
                }
            }
            if (address >= 0x20DFAB8 && (ulong)address - 0x20DFAB8 + (uint)bytes.Length <= NativeBattleEffectArena.Length)
            {
                frame = frame with { Memory = frame.Memory with { Arena = (frame.Memory.Arena ?? throw new NotSupportedException("Native The End requires its physical arena.")).Write(checked((int)(address - 0x20DFAB8)), bytes.AsSpan()) } };
                return;
            }
            if (address >= NativeBattleTheEnd85State.GlobalAddress && (ulong)address - NativeBattleTheEnd85State.GlobalAddress + (uint)bytes.Length <= NativeBattleTheEnd85State.GlobalLength)
            {
                State = State.Write(address, bytes.AsSpan());
                return;
            }
            if (address >= NativeBattleTheEnd85GroundInitialization.ModelBankAddress && (ulong)address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress + (uint)bytes.Length <= NativeBattleTheEnd85GroundInitialization.ModelBankLength)
            {
                byte[] data = (State.ModelData ?? resources.Read(NativeBattleTheEnd85GroundInitialization.ModelBankAddress, NativeBattleTheEnd85GroundInitialization.ModelBankLength).ToImmutableArray()).ToArray();
                bytes.AsSpan().CopyTo(data.AsSpan(checked((int)(address - NativeBattleTheEnd85GroundInitialization.ModelBankAddress))));
                State = State with { ModelData = data.ToImmutableArray() };
                return;
            }
            throw new NotSupportedException("Native The End write has no current physical owner.");
        }
    }
}
