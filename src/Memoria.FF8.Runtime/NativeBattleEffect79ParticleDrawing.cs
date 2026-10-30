using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect79ParticleDrawing
{
    public static NativeBattleEffect79Frame Draw7BBEB0(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs) => DrawTask(frame, taskAddress, resources, inputs);
    public static NativeBattleEffect79Frame Draw7BC950(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs, short uniformScale) => DrawTask(frame, taskAddress, resources, inputs, uniformScale);

    public static NativeBattleEffect79Frame DrawTask(NativeBattleEffect79Frame frame, uint taskAddress, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs, short? scale = null)
    {
        NativeBattleModulePoolTask task = Read(frame, taskAddress);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        int length = scale.HasValue ? 0xE8 : 0xD8;
        (frame, uint address) = Allocate(frame, length);
        NativeBattleEffectArena temporary = Context(frame, address, length);
        NativeBattleProjectedSpriteHead prepared = scale is short scaled ? PrepareHead(frame.Memory, task, inputs.ReadView(frame), temporary, scaled) : NativeBattleProjectedSpriteD8.PrepareHead(frame.Memory, task, inputs.ReadView(frame), temporary);
        frame = PublishContext(frame with { Memory = prepared.Memory }, address, prepared.TemporaryD8, length);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(prepared, source =>
        {
            if (!resources.Sprites.TryGetValue(source, out ImmutableArray<byte> bytes))
            {
                throw new NotSupportedException("Effect79 particle requires its selected original projected sprite source.");
            }
            return new(0, source, 0, 0, bytes);
        }, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(79, task) : null);
        frame = PublishContext(frame with { Memory = drawn.Memory }, address, drawn.TemporaryD8, length);
        frame = inputs.Execute(frame, new NativeBattleEffect79DeliverBatch(drawn.Batch, 0x25DF33C));
        return Release(frame, length);
    }

    public static NativeBattleProjectedSpriteHead PrepareHead(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattlePackedMatrix32 packedCamera, NativeBattleEffectArena temporaryD8, short scale)
    {
        packedCamera.Validate();
        NativeBattleActionMatrix camera = packedCamera.ReadMatrix();
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Projected sprite requires its actual geometry register owner."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Projected sprite requires its actual data register owner."));
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                temporaryD8 = Word(temporaryD8, 0xB8 + row * 6 + column * 2, unchecked((ushort)camera.Rotation[column * 3 + row]));
            }
        }
        if (scale != 4096)
        {
            temporaryD8 = Dword(Dword(Dword(temporaryD8, 0xD8, unchecked((uint)(int)scale)), 0xDC, unchecked((uint)(int)scale)), 0xE0, unchecked((uint)(int)scale));
            for (int coefficient = 0; coefficient < 9; coefficient++)
            {
                short value = unchecked((short)ReadWord(temporaryD8, 0xB8 + coefficient * 2));
                temporaryD8 = Word(temporaryD8, 0xB8 + coefficient * 2, unchecked((ushort)(unchecked(value * scale) >> 12)));
            }
        }
        NativeBattleVector position = new(Signed(task, 0x1C), Signed(task, 0x1E), Signed(task, 0x20));
        temporaryD8 = Dword(Dword(Dword(temporaryD8, 0xCC, unchecked((uint)(int)position.X)), 0xD0, unchecked((uint)(int)position.Y)), 0xD4, unchecked((uint)(int)position.Z));
        short? padding = packedCamera.Known[18] && packedCamera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packedCamera.Bytes.AsSpan().Slice(18, 2)) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, true, false, padding) };
        for (int column = 0; column < 3; column++)
        {
            step = NativeBattleGeometry.LoadIrColumn(step, ReadWord(temporaryD8, 0xB8 + column * 2), ReadWord(temporaryD8, 0xBE + column * 2), ReadWord(temporaryD8, 0xC4 + column * 2));
            step = NativeBattleGeometry.Transform(step, false, true);
            for (int row = 0; row < 3; row++)
            {
                temporaryD8 = Word(temporaryD8, 0xB8 + row * 6 + column * 2, unchecked((ushort)(step.Colors.Ir[row] ?? throw new InvalidDataException("Projected sprite matrix product did not publish its IR output."))));
            }
        }
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, false, true, null) };
        step = NativeBattleGeometry.LoadVector(step, 0, position, position.Z < 0 ? (short)-1 : (short)0);
        step = NativeBattleGeometry.Transform(step);
        for (int axis = 0; axis < 3; axis++)
        {
            temporaryD8 = Dword(temporaryD8, 0xCC + axis * 4, unchecked((uint)(step.Colors.Mac[axis] ?? throw new InvalidDataException("Projected sprite translation did not publish its MAC output."))));
        }
        NativeBattleActionMatrix matrix = ReadMatrix(temporaryD8, 0xB8);
        (byte[] paddingBytes, bool[] paddingKnown) = temporaryD8.ReadSnapshot(0xCA, 2);
        padding = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(paddingBytes) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, padding) };
        temporaryD8 = Dword(temporaryD8, 0, task.ReadDword(0x4C));
        temporaryD8 = Word(temporaryD8, 4, task.ReadWord(0x50));
        temporaryD8 = Word(temporaryD8, 0x24, 0);
        temporaryD8 = Word(temporaryD8, 0xB4, task.ReadWord(0x56));
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, temporaryD8);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort ReadWord(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(offset, 2));
    private static uint ReadDword(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena memory, int offset, ushort value) => memory.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena memory, int offset)
    {
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)ReadWord(memory, offset + index * 2))).ToArray();
        return new(rotation.ToImmutableArray(), unchecked((int)ReadDword(memory, offset + 20)), unchecked((int)ReadDword(memory, offset + 24)), unchecked((int)ReadDword(memory, offset + 28)), 4096);
    }
}
