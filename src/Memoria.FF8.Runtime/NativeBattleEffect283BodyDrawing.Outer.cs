using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect283BodyFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary124, uint CommonCursor);
public sealed record NativeBattleEffect283BodyServices(Func<NativeBattleEffect283BodyFrame, NativeBattleEffect283BodyFrame> Shadow5088A0, Func<NativeBattleEffect283BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect283BodyFrame> Compose5095B0, Func<NativeBattleEffect283BodyFrame, NativeBattleEffect283BodyFrame> DrawAndDeliver, Func<NativeBattleEffect283BodyFrame, NativeBattleEffect283BodyFrame> Restore508C90)
{
    public Func<NativeBattleEffect283BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect283BodyFrame>? DrawLinkedAndDeliver { get; init; }
}

public static partial class NativeBattleEffect283BodyDrawing
{
    public static NativeBattleEffect283BodyFrame DrawOuter(NativeBattleEffect283BodyFrame frame, NativeBattlePackedMatrix32 camera, short centerX, short centerY, short threshold, uint bankAddress, int fade, uint scratchAddress, NativeBattleSummonColors colors, NativeBattleEffect283BodyServices services)
    {
        NativeBattleEffectArena temporary = Dword(frame.Temporary124, 0x6C, bankAddress);
        temporary = Word(Word(Word(Word(temporary, 0x70, unchecked((ushort)threshold)), 0x72, unchecked((ushort)centerX)), 0x74, unchecked((ushort)centerY)), 0x76, 0);
        temporary = Dword(temporary, 0x78, unchecked((uint)fade));
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect283 body requires its current model world matrix.");
        NativeBattlePackedMatrix32 basis = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        temporary = NativeBattleEffectNodeContextMemory.Copy(Packed(basis), 0, temporary, 0, 18);
        temporary = NativeBattleEffectNodeContextMemory.Copy(Packed(basis), 20, temporary, 20, 12);
        (byte[] basisBytes, bool[] basisKnown) = temporary.ReadSnapshot(0, 32);
        basis = new(System.Collections.Immutable.ImmutableArray.CreateRange(basisBytes), System.Collections.Immutable.ImmutableArray.CreateRange(basisKnown));
        frame = services.Shadow5088A0(frame with { Temporary124 = temporary });
        frame = services.Compose5095B0(frame, basis);
        temporary = Dword(frame.Temporary124, 0x24, scratchAddress).Write(0x2C, new byte[4]);
        temporary = Word(Word(temporary, 0x30, 320), 0x32, 216);
        uint rgb = frame.Model.Color40 ?? throw new NotSupportedException("Effect283 body requires its current model color.");
        int factor = unchecked(4096 - fade);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect283 body requires current GTE geometry."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect283 body requires current GTE arithmetic."));
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, (byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
        uint color = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            color |= (uint)unchecked((byte)(registers.Colors.Ir[lane] ?? throw new NotSupportedException("Effect283 body requires its GPF color result."))) << (lane * 8);
        }
        temporary = Dword(temporary, 0x34, color).Write(0x37, [factor == 4096 ? (byte)0 : (byte)2]);
        temporary = Dword(temporary, 0x38, (frame.Model.SequenceState ?? throw new NotSupportedException("Effect283 body requires its current object mask.")).MeshMask124);
        frame = services.DrawAndDeliver(frame with { Temporary124 = temporary, Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } });
        frame = services.Restore508C90(frame);
        if (services.DrawLinkedAndDeliver is { } linked)
        {
            frame = linked(frame with { Temporary124 = Dword(frame.Temporary124, 0x38, uint.MaxValue) }, basis);
        }
        return frame;
    }

    private static NativeBattleEffectArena Packed(NativeBattlePackedMatrix32 packed)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        for (int index = 0; index < 32; index++)
        {
            if (packed.Known[index])
            {
                result = result.Write(index, [packed.Bytes[index]]);
            }
        }
        return result;
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena value, int offset, uint data) => value.Write(offset, BitConverter.GetBytes(data));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena value, int offset, ushort data) => value.Write(offset, BitConverter.GetBytes(data));
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short S16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
}
