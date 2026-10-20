namespace Memoria.FF8.Runtime;

/// <summary>The native cylindrical lattice, column heights and ordered packet writes of Meteor.</summary>
public sealed class NativeBattleMeteor223Grid
{
    private readonly NativeBattleMeteor223GridMemory memory;
    private int Workspace => memory.Workspace;

    private NativeBattleMeteor223Grid(NativeBattleWaterExecutionFrame frame, NativeBattleMeteor223GridServices services)
    {
        memory = new(frame, services);
    }

    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleMeteor223GridServices services)
    {
        NativeBattleMeteor223Grid grid = new(frame, services);
        return grid.Execute();
    }

    private NativeBattleWaterExecutionFrame Execute()
    {
        int current = memory.Current;
        if (memory.Dwords[current, 0xC4] == 0)
        {
            memory.Dwords[current, 0xC4] = -1;
            if (memory.Dwords[current, 0xBC] == 0)
            {
                memory.Dwords[current, 0xBC] = 0x27971E4;
            }
        }
        int parameters = memory.Dwords[current, 0xB8];
        int fades = memory.Dwords[current, 0xBC];
        short referenceSelector = memory.SignedWords[parameters, 6];
        memory.Frame = memory.Services.PrepareBasis(memory.Frame, referenceSelector);
        int reference = memory.ResolveNode(referenceSelector);
        Put(0xF0, memory.SignedWords[parameters, 8]);
        int wave = Get(0xF0) == 0 ? 0 : memory.ResolveNode(Get(0xF0));
        Put(0xE0, unchecked(memory.Dwords[memory.Manager, 0x74] + 0x400));
        memory.Frame = memory.Services.SelectTexture(memory.Frame, memory.SignedWords[parameters, 10]);
        Put(0xE4, Get(0xF8));
        int rectangle = Get(0xF0);
        int width = memory.SignedWords[rectangle, 4];
        Put(0x30, (Byte(0xF4) & 0x80) == 0 ? width : unchecked(2 * width));
        Put(0x34, memory.SignedWords[rectangle, 6]);
        Put(0x50, memory.SignedWords[current, 0x92]);
        Put(0x54, memory.SignedWords[current, 0x9A]);
        Put(0x58, memory.Dwords[current, 0xCC] | 0x3C000000);
        Put(0x5C, 0);
        Put(0x60, memory.Dwords[memory.Manager, 0x7C]);
        Put(0x98, 0);
        Put(0x9C, 0);
        int rows = memory.SignedWords[parameters, 0];
        int columns = memory.SignedWords[parameters, 4];
        memory.BeginPackets(Get(0x60), rows, columns);
        Put(0x64, rows);
        Put(0x68, unchecked(memory.SignedWords[reference, 0x90] << 16));
        Put(0x6C, 0x10000000 / rows);
        Put(0x70, columns);
        Put(0x74, memory.SignedWords[current, 0x8E]);
        Put(0x7C, unchecked(memory.SignedWords[current, 0x90] << 8));
        Put(0x78, unchecked((memory.SignedWords[current, 0x94] << 8) - Get(0x7C)) / columns);
        Put(0x20, unchecked(memory.SignedWords[current, 0x8C] << 16));
        Put(0x24, unchecked(Get(0x30) << 16) / columns);
        Put(0x28, 0);
        Put(0x2C, memory.SignedWords[parameters, 2]);
        Put(0x84, unchecked(memory.SignedWords[current, 0x96] << 8));
        Put(0x88, unchecked(memory.SignedWords[current, 0x98] << 8));
        PutWord(0xAC, memory.Words[parameters, 12]);
        for (int index = 0; index < 4; index++)
        {
            PutWord(0xB0 + 2 * index, memory.Words[fades, 2 * index]);
        }
        PutWord(0xB8, 0);
        PutWord(0xBA, 256);
        InitializeWave(wave);
        Put(0x90, unchecked(memory.SignedWords[reference, 0x8C] << 16));
        Put(0x94, unchecked(memory.SignedWords[reference, 0x8E] << 12));
        if ((Byte(0xAC) & 1) == 0)
        {
            Put(0x8C, unchecked(memory.SignedWords[reference, 0x98] << 16));
            throw new NotSupportedException("Native Meteor noncentered grid requires its unwritten stack depth-buffer selector.");
        }
        int accumulatedHeight = 0;
        int heightIncrement = Get(0x90);
        for (int column = 1; column < columns; column++)
        {
            heightIncrement = unchecked(heightIncrement + Get(0x94));
            accumulatedHeight = unchecked(accumulatedHeight + heightIncrement);
        }
        Put(0x8C, unchecked((memory.SignedWords[reference, 0x98] << 16) - accumulatedHeight));
        int packet = InitializeEdge(wave != 0);
        int lastLinkedAddress = Get(0x60);
        while (Get(0x64) > 0)
        {
            BuildBand(ref packet, ref lastLinkedAddress);
            Put(0x64, Get(0x64) - 1);
        }
        int finalCursor = unchecked(lastLinkedAddress + 0x44);
        memory.Dwords[memory.Manager, 0x7C] = finalCursor;
        return memory.Publish(finalCursor);
    }

    private void InitializeWave(int reference)
    {
        if (reference == 0)
        {
            Put(0xA0, 0);
            Put(0xA4, 0);
            Put(0xA8, 0);
            return;
        }
        PutWord(0xA0, unchecked((ushort)(memory.Bytes[reference, 0x94] << 8)));
        PutWord(0xA2, memory.Words[reference, 0x96]);
        PutWord(0xA4, memory.Words[reference, 0x98]);
        PutWord(0xA6, memory.Words[reference, 0x8C]);
        PutWord(0xA8, memory.Words[reference, 0x8E]);
        PutWord(0xAA, memory.Words[reference, 0x90]);
    }

    private int InitializeEdge(bool wave)
    {
        Put(0x40, Get(0xE0));
        ColumnCursor cursor = NewCursor(Get(0x8C), Get(0x90));
        int packet = Get(0x60);
        int depthAddress = unchecked(memory.Dwords[memory.Manager, 0x74] + 0x200);
        StoreVertex(cursor, true, wave);
        memory.Words[depthAddress, 0] = unchecked((ushort)memory.Project(Workspace + 0xC0, packet + 0x10));
        byte u = unchecked((byte)((Get(0x20) >> 16) % Get(0x30) + Get(0x98)));
        memory.Bytes[packet, 0x2C] = u;
        memory.Bytes[packet, 0x14] = u;
        byte v = unchecked((byte)(Byte(0x9C) + Byte(0x28)));
        memory.Bytes[packet, 0x21] = v;
        memory.Bytes[packet, 0x15] = v;
        ShadeVertex();
        memory.Dwords[packet, 0x24] = Get(0xBC);
        memory.Dwords[packet, 0x0C] = Get(0xBC);
        Put(0x5C, Get(0x5C) + 1);
        int textureU = Get(0x20);
        for (int column = 0; column < Get(0x70); column++)
        {
            Step(ref cursor, true);
            textureU = unchecked(textureU + Get(0x24));
            StoreVertex(cursor, true, wave);
            int nextScreen = unchecked(packet + 0x54);
            memory.Words[depthAddress, 0] = unchecked((ushort)memory.Project(Workspace + 0xC0, nextScreen));
            memory.Dwords[packet, 0x1C] = memory.Dwords[nextScreen, 0];
            ShadeVertex();
            memory.Dwords[packet, 0x30] = Get(0xBC);
            memory.Dwords[packet, 0x18] = Get(0xBC);
            memory.Dwords[nextScreen, 0x14] = Get(0xBC);
            memory.Dwords[nextScreen, -4] = Get(0xBC);
            int remainder = (textureU >> 16) % Get(0x30);
            int nextU = unchecked(remainder + Get(0x98));
            int rightU = remainder == 0 || memory.Bytes[packet, 0x14] >= nextU ? unchecked(nextU + Byte(0x30)) : nextU;
            memory.Bytes[packet, 0x38] = unchecked((byte)rightU);
            memory.Bytes[packet, 0x20] = unchecked((byte)rightU);
            memory.Bytes[nextScreen, 0x1C] = unchecked((byte)nextU);
            memory.Bytes[nextScreen, 4] = unchecked((byte)nextU);
            v = memory.Bytes[packet, 0x15];
            memory.Bytes[nextScreen, 0x11] = v;
            memory.Bytes[nextScreen, 5] = v;
            packet = unchecked(packet + 0x44);
            Put(0x5C, Get(0x5C) + 1);
        }
        return packet;
    }

    private void BuildBand(ref int output, ref int lastLinkedAddress)
    {
        Put(0x40, Get(0xE0));
        int previous = unchecked(output - Get(0x70) * 0x44);
        Put(0x28, unchecked(Get(0x28) + Get(0x2C)));
        Put(0x68, unchecked(Get(0x68) + Get(0x6C)));
        ColumnCursor cursor = NewCursor(0, 0);
        int depthBase = memory.Dwords[memory.Manager, 0x74];
        int currentDepth = (Byte(0x64) & 1) == 0 ? depthBase + 0x200 : depthBase;
        int priorDepth = (Byte(0x64) & 1) == 0 ? depthBase : depthBase + 0x200;
        StoreVertex(cursor, false, false);
        memory.Words[currentDepth, 0] = unchecked((ushort)memory.Project(Workspace + 0xC0, output + 0x10));
        memory.Dwords[previous, 0x28] = memory.Dwords[output, 0x10];
        CopyPair(previous, 0x0C, output, 0x24, 0x0C);
        CopyPair(previous, 0x18, output, 0x30, 0x18);
        CopyBytePair(previous, 0x14, output, 0x2C, 0x14);
        CopyBytePair(previous, 0x20, output, 0x38, 0x20);
        int remainder = Get(0x28) % Get(0x34);
        int bottomV = remainder == 0 ? Get(0x9C) : unchecked(remainder + Get(0x9C));
        int topV = remainder == 0 ? unchecked(Get(0x9C) + Get(0x34)) : bottomV;
        memory.Bytes[previous, 0x39] = unchecked((byte)topV);
        memory.Bytes[previous, 0x2D] = unchecked((byte)topV);
        memory.Bytes[output, 0x21] = unchecked((byte)bottomV);
        memory.Bytes[output, 0x15] = unchecked((byte)bottomV);
        Put(0x38, bottomV);
        Put(0x3C, topV);
        for (int column = 0; column < Get(0x70); column++)
        {
            memory.Bytes[previous, 3] = 16;
            memory.Dwords[previous, 8] = 0;
            memory.Dwords[previous, 0x3C] = unchecked((int)0xE2000000);
            memory.Dwords[previous, 0x40] = 0;
            memory.Words[previous, 0x22] = Word(0x50);
            memory.Words[previous, 0x16] = Word(0x54);
            memory.Dwords[previous, 4] = Get(0xE4);
            Step(ref cursor, false);
            StoreVertex(cursor, false, false);
            int screenAddress = unchecked(output + 0x54);
            int depth = memory.Project(Workspace + 0xC0, screenAddress);
            int sum = memory.SignedWords[priorDepth, 0] + memory.SignedWords[currentDepth, 0];
            currentDepth += 2;
            priorDepth += 2;
            memory.Words[currentDepth, 0] = unchecked((ushort)depth);
            sum = unchecked(sum + memory.SignedWords[priorDepth, 0] + depth);
            uint screen = unchecked((uint)memory.Dwords[screenAddress, 0]);
            memory.Dwords[output, 0x1C] = unchecked((int)screen);
            memory.Dwords[previous, 0x34] = unchecked((int)screen);
            CopyPair(previous, 0x0C, output, 0x24, 0x0C);
            CopyPair(previous, 0x18, output, 0x30, 0x18);
            memory.Bytes[screenAddress, 0x11] = Byte(0x38);
            memory.Bytes[screenAddress, 5] = Byte(0x38);
            memory.Bytes[previous, 0x39] = Byte(0x3C);
            memory.Bytes[previous, 0x2D] = Byte(0x3C);
            int following = unchecked(previous + 0x44);
            CopyBytePair(following, 0x14, screenAddress, 0x1C, 4);
            memory.Dwords[following, 0x28] = unchecked((int)screen);
            CopyBytePair(following, 0x20, screenAddress, 0x28, 0x10);
            int bucket = (sum / 4) >> 2;
            lastLinkedAddress = previous;
            if (bucket is > 0 and < 0xFFF)
            {
                memory.Link(unchecked(memory.Dwords[memory.Runtime, 0x4C] + 4 * bucket), previous);
            }
            previous = following;
            output = unchecked(output + 0x44);
        }
    }

    private ColumnCursor NewCursor(int height, int heightStep) => new(Get(0x7C), Get(0x78), Get(0x68), Get(0x84), height, heightStep);

    private void Step(ref ColumnCursor cursor, bool height)
    {
        int radialStep = unchecked(cursor.RadialStep + Get(0x74));
        int angularStep = unchecked(cursor.AngularStep + Get(0x88));
        int nextHeightStep = height ? unchecked(cursor.HeightStep + Get(0x94)) : cursor.HeightStep;
        cursor = new(unchecked(cursor.Radius + radialStep), radialStep, unchecked(cursor.Angle + angularStep), angularStep, height ? unchecked(cursor.Height + nextHeightStep) : cursor.Height, nextHeightStep);
    }

    private void StoreVertex(ColumnCursor cursor, bool firstEdge, bool wave)
    {
        int radius = cursor.Radius >> 4;
        int angle = cursor.Angle >> 16;
        PutWord(0xC0, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) * radius >> 16)));
        PutWord(0xC2, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) * radius >> 16)));
        int heights = Get(0x40);
        if (firstEdge)
        {
            int height = cursor.Height;
            if (wave)
            {
                int displacement = unchecked(NativeBattleCameraMath.Sine(unchecked(SignedWord(0xA6) << 4)) * SignedWord(0xA0)) >> 4;
                height = unchecked(height + displacement);
            }
            PutWord(0xC4, unchecked((ushort)(height >> 16)));
            memory.Dwords[heights, 0] = SignedWord(0xC4);
            if (wave)
            {
                PutWord(0xA2, unchecked((ushort)(Word(0xA2) + Word(0xA4))));
                PutWord(0xA0, unchecked((ushort)(Word(0xA0) + Word(0xA2))));
                PutWord(0xA8, unchecked((ushort)(Word(0xA8) + Word(0xAA))));
                PutWord(0xA6, unchecked((ushort)(Word(0xA6) + Word(0xA8))));
            }
        }
        else
        {
            PutWord(0xC4, memory.Words[heights, 0]);
        }
        if ((Byte(0xAC) & 16) != 0)
        {
            ushort second = Word(0xC2);
            PutWord(0xC2, Word(0xC4));
            PutWord(0xC4, second);
        }
        Put(0x40, unchecked(heights + 4));
    }

    private void ShadeVertex()
    {
        int color = Get(0x58);
        Put(0xBC, color);
        if (Word(0xB6) != 0 && SignedWord(0xB4) <= Get(0x5C))
        {
            int factor = SignedWord(0xBA);
            Put(0xBC, factor <= 0 ? color & unchecked((int)0xFF000000) : ScaleColor(color, factor));
            PutWord(0xBA, unchecked((ushort)(Word(0xBA) + Word(0xB6))));
        }
        else if (Word(0xB2) != 0)
        {
            if (SignedWord(0xB0) >= Get(0x5C))
            {
                Put(0xBC, color & unchecked((int)0xFF000000));
                return;
            }
            int factor = SignedWord(0xB8);
            Put(0xBC, factor >= 256 ? color : ScaleColor(color, factor));
            PutWord(0xB8, unchecked((ushort)(Word(0xB8) + Word(0xB2))));
        }
    }

    private static int ScaleColor(int color, int factor)
    {
        int Channel(int shift) => unchecked(((color >> shift) & 255) * factor) / 256;
        int upper = unchecked((Channel(16) << 8) | Channel(8));
        return unchecked((upper << 8) | Channel(0) | (color & (int)0xFF000000));
    }

    private void CopyPair(int source, int offset, int destination, int first, int second)
    {
        int value = memory.Dwords[source, offset];
        memory.Dwords[destination, first] = value;
        memory.Dwords[destination, second] = value;
    }

    private void CopyBytePair(int source, int offset, int destination, int first, int second)
    {
        byte value = memory.Bytes[source, offset];
        memory.Bytes[destination, first] = value;
        memory.Bytes[destination, second] = value;
    }

    private int Get(int offset) => memory.Dwords[Workspace, offset];
    private void Put(int offset, int value) => memory.Dwords[Workspace, offset] = value;
    private ushort Word(int offset) => memory.Words[Workspace, offset];
    private short SignedWord(int offset) => memory.SignedWords[Workspace, offset];
    private byte Byte(int offset) => memory.Bytes[Workspace, offset];
    private void PutWord(int offset, ushort value) => memory.Words[Workspace, offset] = value;
    private readonly record struct ColumnCursor(int Radius, int RadialStep, int Angle, int AngularStep, int Height, int HeightStep);
}
