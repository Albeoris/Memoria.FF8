using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicMesh(NativeBattleWaterExecutionFrame frame, byte kind, int secondFileLength, NativeBattleCinematicResources resources, NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native cinematic mesh requires its selected original effect.");
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
        if (resources.TextureProfile.InitializationAddress != profile.InitializationAddress || resources.TextureProfile.OwnerDirectoryAddress != profile.OwnerDirectoryAddress)
        {
            throw new InvalidDataException("Native cinematic mesh resources disagree with their selected static directory owner.");
        }
        if (kind is not (1 or 2 or 7 or 9 or 0x1B or 0x39))
        {
            throw new NotSupportedException($"Native cinematic draw {kind:X2} is outside the ordinary mesh wrappers.");
        }
        uint handler = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.DrawTable + 4u * kind), 4));
        handler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "draw_table", kind, handler);
        if ((kind, handler) is not ((1, 0xB26FB0) or (2, 0xB27050) or (0x1B, 0xB272D0) or (9, 0xB270A0) or (0x39, 0xAEAAE0)) && !(profile.Effect == 220 && kind == 7 && handler == 0xB27220))
        {
            throw new NotSupportedException($"Native cinematic draw {kind:X2} selected unsupported mesh wrapper {handler:X8}.");
        }
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, secondFileLength);
        int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native cinematic mesh requires its canonical camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native cinematic mesh requires its current projection registers.");
        ushort focal = projection.FocalLength ?? throw new NotSupportedException("Native cinematic mesh requires its actual current GTE focal register.");
        NativeBattleEffectMemory currentMemory = RequireWaterMemory();
        NativeBattleGeometryRegisterStep currentStep = new(currentMemory.GeometryRegisters ?? throw new NotSupportedException("Native cinematic mesh requires its current geometry registers."), currentMemory.ScriptedColorRegisters ?? throw new NotSupportedException("Native cinematic mesh requires its current arithmetic registers."));
        NativeBattleWaterMeshPrepared prepared;
        if (kind == 2)
        {
            (prepared, currentStep) = NativeBattleWaterMeshPreparation.PrepareWrapper2(frame, bank, index, unchecked((short)focal), currentStep);
        }
        else
        {
            prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, bank, index, kind, unchecked((short)focal));
            currentStep = currentStep with { Geometry = NativeBattleGeometry.LoadMatrix(currentStep.Geometry, prepared.Matrix, true, true, null) };
        }
        NativeCamera = camera with { Projection = projection with { FocalLength = unchecked((ushort)prepared.Focal) } };
        frame = prepared.Frame;
        uint shape = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD8, 4));
        int extent = MeasureNativeCinematicMeshExtent(frame, shape, ReadStride);
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic mesh requires its actual depth and depth-cue registers.");
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Native cinematic mesh requires its retained shared lighting controls.");
        uint start = CoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C);
        NativeBattleScriptedMeshMorphServices morph = new((current, selector) =>
        {
            int referenced = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, referenced));
        }, (current, shapeIndex) => NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, shapeIndex).Selected);
        NativeBattleScriptedMeshProjected projected = NativeBattleScriptedMeshPreparation.Project(prepared, extent, projection, registers.Dqa, registers.Dqb, registers.Depths, lighting, currentStep, morph, registers.DepthScaleBits1877DA8, NativeBattleCinematicVisualIdentity.Identity(profile.Effect, frame, bank, index));
        NativeBattleScriptedMeshSubmission submitted = NativeBattleScriptedMeshPreparation.Submit(projected, extent, ReadHandler, ReadStride, OtBucket);
        frame = submitted.Frame;
        memory = RequireWaterMemory() with
        {
            Arena = frame.Arena,
            ScriptedEffects = frame.Core,
            ScriptedGraphics = registers with { Depths = projected.DepthOwner, DepthScaleBits1877DA8 = projected.DepthScaleBits1877DA8 },
            ScriptedLighting = submitted.Lighting,
            ScriptedColorRegisters = submitted.Registers,
            GeometryRegisters = submitted.Geometry ?? throw new NotSupportedException("Native cinematic mesh did not publish its geometry registers.")
        };
        if (!submitted.Packets.Attempts.IsEmpty)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic mesh requires its actual current ordering table.");
            if ((ReadSlot(frame, 0x4C, 1)[0] & 1) != 0)
            {
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, ArenaOffset(start, 0), primitive.Pending, submitted.Packets);
                memory = written.Memory;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                NativeBattlePrimitiveProcess common = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic mesh requires its actual COMMON packet owner.");
                if (start != common.ByteCursor)
                {
                    throw new InvalidDataException("Native cinematic mesh COMMON cursor disagrees with its current physical packet owner.");
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, submitted.Packets);
                memory = memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
        }
        NativeEffectMemory = memory;
        return (frame with { Arena = memory.Arena!, Core = memory.ScriptedEffects! }, graphics);

        byte ReadStride(int type)
        {
            if ((uint)type >= 20)
            {
                throw new NotSupportedException($"Native cinematic primitive size index {type:X} leaves its actual static directory.");
            }
            return resources.ReadStatic(checked(profile.PrimitiveSizes + (uint)type), 1)[0];
        }

        uint ReadHandler(int dispatch)
        {
            if ((uint)dispatch >= 60)
            {
                throw new NotSupportedException($"Native cinematic primitive dispatch index {dispatch:X} leaves its actual static table.");
            }
            uint handler = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.PrimitiveTable + 4u * (uint)dispatch), 4));
            return NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "prim_table", dispatch, handler);
        }
    }

    internal static int MeasureNativeCinematicMeshExtent(NativeBattleWaterExecutionFrame frame, uint shape, Func<int, byte> readStride)
    {
        _ = ArenaOffset(shape, 0x24);
        int extent = 0x24;
        int vertices = unchecked((int)ReadDword(shape + 0x18));
        int normals = unchecked((int)ReadDword(shape + 0x20));
        if (vertices <= 0 || normals < 0)
        {
            throw new NotSupportedException("Native cinematic shape requires its current positive vertex count and nonnegative normal count.");
        }
        Include(ReadDword(shape + 0x14), checked(vertices * 8));
        if (normals > 0)
        {
            Include(ReadDword(shape + 0x1C), checked(normals * 8));
        }
        uint cursor = ReadDword(shape + 8);
        while (true)
        {
            Include(cursor, 4);
            byte[] header = frame.Arena.Read(ArenaOffset(unchecked(shape + cursor), 4), 4);
            short type = BinaryPrimitives.ReadInt16LittleEndian(header);
            short count = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(2));
            if (count == -1)
            {
                return extent;
            }
            if (type < 0 || count <= 0)
            {
                throw new NotSupportedException("Native cinematic shape requires its actual selected primitive type and positive record count.");
            }
            byte stride = readStride(type);
            cursor = checked(cursor + 4);
            int length = checked(count * stride);
            Include(cursor, length);
            cursor = checked(cursor + (uint)length);
        }

        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));

        void Include(uint relative, int length)
        {
            int end = checked((int)relative + length);
            _ = ArenaOffset(checked(shape + relative), length);
            extent = Math.Max(extent, end);
        }
    }
}
