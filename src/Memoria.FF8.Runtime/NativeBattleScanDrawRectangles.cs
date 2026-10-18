using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanIntersection(NativeBattleScanRect Rectangle, bool Visible);
public sealed record NativeBattleScanBackendClip(NativeBattleScanByteOwner Backend, NativeBattleScanByteOwner ContextScratch, uint ContextPointer, bool Visible);
public enum NativeBattleScanRectangleSubmission { Reset49D680, Sprite49C610, WindowSprite49C660 }
public sealed record NativeBattleScanRectangleFrame(NativeBattleScanByteOwner Scratch, uint ScratchAddress, uint Tail);
public sealed record NativeBattleScanRectangleServices(Action Begin49B0B0, Action End49B0D0, Func<NativeBattleScanRectangleSubmission, uint, uint, NativeBattleScanByteOwner, uint> Submit);

public static class NativeBattleScanDrawRectangles
{
    public static NativeBattleScanIntersection Intersect4B6350(NativeBattleScanRect first, NativeBattleScanRect second)
    {
        int left = Math.Max(first.X, second.X);
        int top = Math.Max(first.Y, second.Y);
        int right = Math.Min(first.X + first.Width, second.X + second.Width);
        int bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        bool visible = true;
        if (left >= right)
        {
            right = left;
            visible = false;
        }
        if (top >= bottom)
        {
            bottom = top;
            visible = false;
        }
        return new(new((short)left, (short)top, unchecked((short)(right - left)), unchecked((short)(bottom - top))), visible);
    }

    public static NativeBattleScanBackendClip ClipBackend4B6400(NativeBattleScanByteOwner backend, NativeBattleScanByteOwner contextScratch, uint contextPointer, Func<NativeBattleScanRect> readDrawArea49A870, Action<uint> publishContext)
    {
        backend.RequireLength(64);
        contextScratch.RequireLength(24);
        ArgumentNullException.ThrowIfNull(readDrawArea49A870);
        ArgumentNullException.ThrowIfNull(publishContext);
        publishContext(unchecked(contextPointer + 24));
        bool visible = Clip(8, 0x18);
        Clip(0x10, 0x24);
        publishContext(contextPointer);
        return new(backend, contextScratch, contextPointer, visible);

        bool Clip(int source, int destination)
        {
            NativeBattleScanRect area = readDrawArea49A870();
            contextScratch = contextScratch.Write(12, area.ToBytes()).WriteWord(20, unchecked((ushort)area.X)).WriteWord(22, unchecked((ushort)area.Y));
            NativeBattleScanRect rectangle = ReadRect(backend, source);
            rectangle = rectangle with { X = unchecked((short)(rectangle.X + area.X)), Y = unchecked((short)(rectangle.Y + area.Y)) };
            short originalX = rectangle.X;
            short originalY = rectangle.Y;
            bool intersects = false;
            if (rectangle.Width > 0 && rectangle.Height > 0)
            {
                NativeBattleScanIntersection clipped = Intersect4B6350(area, rectangle);
                rectangle = clipped.Rectangle;
                intersects = clipped.Visible;
            }
            if (rectangle.Width <= 1)
            {
                rectangle = rectangle with { Width = 2 };
            }
            if (rectangle.Height <= 1)
            {
                rectangle = rectangle with { Height = 1 };
            }
            contextScratch = contextScratch.Write(0, rectangle.ToBytes()).WriteWord(8, unchecked((ushort)originalX)).WriteWord(10, unchecked((ushort)originalY));
            backend = backend.Write(destination, contextScratch.Bytes.AsSpan(0, 12));
            return intersects;
        }
    }

    public static NativeBattleScanRectangleFrame EmitFill4B6A80(NativeBattleScanRectangleFrame frame, NativeBattleScanRect rectangle, uint color, uint mode, NativeBattleScanRectangleServices services, Func<int, uint> readStaticWindowCommand)
    {
        if (mode != 0)
        {
            throw new NotSupportedException("Native Scan fill requires its reached mode zero profile.");
        }
        Validate(frame, services);
        ArgumentNullException.ThrowIfNull(readStaticWindowCommand);
        services.Begin49B0B0();
        frame = Reset(frame, services);
        int width = unchecked((ushort)rectangle.Width) & 0xFFFE;
        int x = unchecked((ushort)rectangle.X);
        int command = 0;
        while (width > 0)
        {
            int amount = Math.Min(width, 128);
            NativeBattleScanByteOwner scratch = frame.Scratch.WriteWord(0x14, (ushort)x).WriteDword(0x18, 0x38500000).WriteDword(0x10, (color & 0x02FFFFFF) | 0x64000000).WriteDword(0, 0x07000000).WriteDword(8, readStaticWindowCommand(command++)).WriteDword(12, 0).WriteDword(4, 0xE100041E).WriteWord(0x16, unchecked((ushort)rectangle.Y)).WriteWord(0x1E, unchecked((ushort)rectangle.Height)).WriteByte(0x19, 0).WriteWord(0x1C, (ushort)amount);
            frame = Submit(frame with { Scratch = scratch }, services, NativeBattleScanRectangleSubmission.WindowSprite49C660);
            width -= amount;
            x += amount;
        }
        services.End49B0D0();
        return frame;
    }

    public static NativeBattleScanRectangleFrame EmitBorder4B6530(NativeBattleScanRectangleFrame frame, NativeBattleScanRect rectangle, uint color, uint mode, NativeBattleScanRectangleServices services, Func<byte> readDepthFlag1D2B0D9, Action<float> setOffsetY1D2B0E0)
    {
        if (mode != 3)
        {
            throw new NotSupportedException("Native Scan border requires its reached mode three profile.");
        }
        Validate(frame, services);
        ArgumentNullException.ThrowIfNull(readDepthFlag1D2B0D9);
        ArgumentNullException.ThrowIfNull(setOffsetY1D2B0E0);
        services.Begin49B0B0();
        int x = rectangle.X;
        int y = rectangle.Y;
        int width = rectangle.Width;
        int height = rectangle.Height;
        int cornerWidth = width < 16 ? width / 2 : 8;
        int cornerHeight = height < 16 ? height / 2 : 8;
        int crop = (width < 16 ? 8 - cornerWidth : 0) | (height < 16 ? 0x800 - (cornerHeight << 8) : 0);
        int innerX = x + cornerWidth;
        int innerY = y + cornerHeight;
        int innerHeight = height - 2 * cornerHeight;
        int rightX = x + width - cornerWidth;
        int bottomY = y + height - cornerHeight;
        int innerWidth = (width - 2 * cornerWidth + 1) / 2 * 2;
        uint packedColor = (color & 0x02FFFFFF) | 0x64000000;
        frame = Reset(frame, services);
        Corner(x, y, 0x10);
        WindowSprite(x, bottomY, cornerWidth, cornerHeight, (crop & ~0xFF) + 0x1010, 0xE2000000, true);
        frame = Reset(frame, services);
        if (innerHeight > 0)
        {
            WindowSprite(x, innerY, 8, innerHeight, 0, 0xE2008BFF, false);
        }
        Corner(rightX, y, (crop & 0xFF) + 0x20);
        WindowSprite(rightX, bottomY, cornerWidth, cornerHeight, crop + 0x1020, 0xE2000000, true);
        frame = Reset(frame, services);
        if (innerHeight > 0)
        {
            WindowSprite(rightX, innerY, 8, innerHeight, crop & 0xFF, 0xE20093FF, false);
        }
        if (innerWidth > 0)
        {
            if (innerWidth > 240)
            {
                WindowSprite(innerX, y, 240, 8, 0, 0xE2000FFF, false);
                WindowSprite(innerX, bottomY, 240, 8, crop & 0xFF00, 0xE2010FFF, false);
                innerWidth -= 240;
                innerX += 240;
            }
            WindowSprite(innerX, y, innerWidth, 8, 0, 0xE2000FFF, false);
            frame = frame with { Scratch = PrepareWindow(frame.Scratch, innerX, bottomY, innerWidth, 8, crop & 0xFF00, 0xE2010FFF, false) };
            if (readDepthFlag1D2B0D9() != 0)
            {
                setOffsetY1D2B0E0(-1);
            }
            frame = Submit(frame, services, NativeBattleScanRectangleSubmission.WindowSprite49C660);
            setOffsetY1D2B0E0(0);
        }
        services.End49B0D0();
        return frame;

        void Corner(int left, int top, int uv)
        {
            NativeBattleScanByteOwner scratch = frame.Scratch.WriteWord(12, unchecked((ushort)left)).WriteWord(14, unchecked((ushort)top)).WriteDword(8, packedColor).WriteDword(0, 0x05000000).WriteDword(4, 0xE100041E).WriteWord(20, unchecked((ushort)cornerWidth)).WriteWord(22, unchecked((ushort)cornerHeight)).WriteWord(18, 0x3810).WriteWord(16, unchecked((ushort)uv));
            frame = Submit(frame with { Scratch = scratch }, services, NativeBattleScanRectangleSubmission.Sprite49C610);
        }

        void WindowSprite(int left, int top, int spriteWidth, int spriteHeight, int uv, uint window, bool uvWordOnly)
        {
            frame = Submit(frame with { Scratch = PrepareWindow(frame.Scratch, left, top, spriteWidth, spriteHeight, uv, window, uvWordOnly) }, services, NativeBattleScanRectangleSubmission.WindowSprite49C660);
        }

        NativeBattleScanByteOwner PrepareWindow(NativeBattleScanByteOwner scratch, int left, int top, int spriteWidth, int spriteHeight, int uv, uint window, bool uvWordOnly)
        {
            scratch = scratch.WriteDword(16, packedColor).WriteDword(0, 0x07000000).WriteDword(4, 0xE100041E).WriteDword(8, window).WriteDword(12, 0).WriteWord(20, unchecked((ushort)left)).WriteWord(22, unchecked((ushort)top)).WriteWord(28, unchecked((ushort)spriteWidth)).WriteWord(30, unchecked((ushort)spriteHeight));
            return uvWordOnly ? scratch.WriteWord(26, 0x3810).WriteWord(24, unchecked((ushort)uv)) : scratch.WriteDword(24, unchecked((uint)(0x38100000 + uv)));
        }
    }

    private static NativeBattleScanRect ReadRect(NativeBattleScanByteOwner owner, int offset) => new(unchecked((short)owner.ReadWord(offset)), unchecked((short)owner.ReadWord(offset + 2)), unchecked((short)owner.ReadWord(offset + 4)), unchecked((short)owner.ReadWord(offset + 6)));

    private static NativeBattleScanRectangleFrame Reset(NativeBattleScanRectangleFrame frame, NativeBattleScanRectangleServices services)
    {
        return Submit(frame with { Scratch = frame.Scratch.WriteDword(0, 0x02000000).WriteDword(4, 0xE2000000).WriteDword(8, 0) }, services, NativeBattleScanRectangleSubmission.Reset49D680);
    }

    private static NativeBattleScanRectangleFrame Submit(NativeBattleScanRectangleFrame frame, NativeBattleScanRectangleServices services, NativeBattleScanRectangleSubmission kind) => frame with { Tail = services.Submit(kind, frame.Tail, frame.ScratchAddress, frame.Scratch) };

    private static void Validate(NativeBattleScanRectangleFrame frame, NativeBattleScanRectangleServices services)
    {
        frame.Scratch.RequireLength(32);
        ArgumentNullException.ThrowIfNull(services.Begin49B0B0);
        ArgumentNullException.ThrowIfNull(services.End49B0D0);
        ArgumentNullException.ThrowIfNull(services.Submit);
    }
}
