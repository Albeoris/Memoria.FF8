using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect303BodyFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary188, uint CommonCursor, uint FxCursor);
public sealed record NativeBattleEffect303BodyServices(Func<NativeBattleEffect303BodyFrame, NativeBattleEffect303BodyFrame> Shadow5088A0, Func<NativeBattleEffect303BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect303BodyFrame> Compose5095B0, Func<NativeBattleEffect303BodyFrame, NativeBattleEffect303BodyFrame> DrawAndDeliver, Func<NativeBattleEffect303BodyFrame, NativeBattleEffect303BodyFrame> Restore508C90);

public static partial class NativeBattleEffect303BodyDrawing
{
    public static NativeBattleEffect303BodyFrame DrawOuter(NativeBattleEffect303BodyFrame frame, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena textureRectangle8, ushort clut, int vOffset, int uOffset, NativeBattleEffectArena color4, uint scratchAddress, NativeBattleEffect303BodyServices services)
    {
        frame = services.Shadow5088A0(frame);
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect303 body requires the current recipient world matrix.");
        NativeBattlePackedMatrix32 basis = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        frame = frame with { Temporary188 = NativeBattleEffectNodeContextMemory.Copy(Packed(basis), 0, frame.Temporary188, 0, 32) };
        frame = services.Compose5095B0(frame, basis);
        NativeBattleEffectArena temporary = frame.Temporary188;
        int x = S16(textureRectangle8, 0);
        int y = S16(textureRectangle8, 2);
        ushort page = unchecked((ushort)((((x & 0x3C0) | 0x2800) >> 6) | ((y >> 4) & 0x10) | ((y & 0x200) << 2)));
        temporary = Dword(temporary, 0x24, scratchAddress).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((short)320)).Write(0x32, BitConverter.GetBytes((short)216));
        temporary = Dword(temporary, 0x34, frame.Model.Color40 ?? throw new NotSupportedException("Effect303 body requires the actual recipient color DWORD."));
        temporary = Dword(temporary, 0x38, (frame.Model.SequenceState ?? throw new NotSupportedException("Effect303 body requires the current recipient object mask.")).MeshMask124);
        temporary = Dword(temporary, 0x94, 0x02000000);
        temporary = Dword(temporary, 0x98, 0xE2000000);
        temporary = Dword(temporary, 0x9C, 0);
        temporary = Dword(temporary, 0xA0, 0x02000000);
        temporary = Dword(temporary, 0xA4, 0xE2000210);
        temporary = Dword(temporary, 0xA8, 0);
        temporary = Dword(temporary, 0x84, unchecked((uint)(uOffset & 127)));
        temporary = Dword(temporary, 0x88, unchecked((uint)(vOffset & 127)));
        temporary = Dword(temporary, 0x8C, 128);
        temporary = Dword(temporary, 0x90, 128);
        temporary = temporary.Write(0x80, BitConverter.GetBytes(clut)).Write(0x82, BitConverter.GetBytes(page));
        temporary = NativeBattleEffectNodeContextMemory.Copy(color4, 0, temporary, 0x7C, 4);
        temporary = Dword(temporary, 0xB4, frame.CommonCursor);
        temporary = Dword(temporary, 0xB8, frame.FxCursor);
        frame = services.DrawAndDeliver(frame with { Temporary188 = temporary });
        frame = frame with { CommonCursor = U32(frame.Temporary188, 0xB4), FxCursor = U32(frame.Temporary188, 0xB8) };
        return services.Restore508C90(frame);
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
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short S16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
}
