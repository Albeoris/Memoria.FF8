using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129Body
{
    public static NativeBattleBreath129Frame Capture6B73F0(NativeBattleBreath129Frame frame, uint actorAddress, uint output, int pairs, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        if (pairs < 0)
        {
            throw new InvalidDataException("Native Breath capture requires its actual nonnegative pair count.");
        }
        (frame, uint temporary) = Allocate(frame, 52);
        uint cursor = frame.State.ReadDword(0x252117C);
        uint row = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, actorAddress));
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(0, BitConverter.GetBytes(cursor)).Write(4, BitConverter.GetBytes(row)), 52);
        NativeBattleBreath129BodyStep source = inputs.PublishBody508C90(frame, row);
        if (source.Source.BodyRowAddress != row)
        {
            throw new InvalidDataException("Native Breath capture changed its selected physical body row.");
        }
        frame = Capture6B74F0(source.Frame, temporary, source.Source);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(48, BitConverter.GetBytes(output)), 52);
        for (int pair = 0; pair < pairs; pair++)
        {
            short first;
            short second;
            do
            {
                NativeBattleEffectArena context = Context(frame, temporary, 52);
                int count = Signed(context, 44);
                if (count <= 0)
                {
                    throw new NotSupportedException("Native Breath sampling requires its actual positive signed face count.");
                }
                NativeCrtRandomState random = frame.Random.Draw(out ushort firstDraw).Draw(out ushort distanceDraw);
                frame = frame with { Random = random };
                first = unchecked((short)(unchecked(firstDraw * count) >> 15));
                short extent = Signed(context, 42);
                short desired = unchecked((short)((unchecked(distanceDraw * extent) >> 16) + (extent >> 2) + (extent >> 1)));
                (frame, second) = Select6B7A60(frame, temporary, first, desired, resources);
            }
            while (first == second);
            frame = Copy6B7BA0(frame, temporary, first);
            frame = Copy6B7BA0(frame, temporary, second);
        }
        return Release(frame, 52);
    }

    public static NativeBattleBreath129Frame Capture6B74F0(NativeBattleBreath129Frame frame, uint temporary, NativeBattleBreath129BodySource source)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        uint scratch = Dword(context, 0);
        uint output = scratch;
        context = context.Write(24, new byte[2]).Write(44, new byte[2]);
        for (int lane = 0; lane < 3; lane++)
        {
            context = context.Write(30 + lane * 2, BitConverter.GetBytes(short.MaxValue)).Write(36 + lane * 2, BitConverter.GetBytes(short.MinValue));
        }
        int objects = unchecked((int)Read32(source, source.GeometryAddress));
        for (int item = 0; item < objects; item++)
        {
            uint at = unchecked(source.GeometryAddress + Read32(source, checked(source.GeometryAddress + 4u + (uint)item * 4)));
            int groups = Read16(source, at);
            at = checked(at + 2);
            for (int group = 0; group < groups; group++)
            {
                short bone = Read16(source, at);
                int count = Read16(source, checked(at + 2));
                at = checked(at + 4);
                if ((uint)bone >= source.BoneMatrices.Length || count < 0)
                {
                    throw new InvalidDataException("Native Breath capture leaves its actual bone or packed vertex group.");
                }
                frame = LoadMatrix(frame, source.BoneMatrices[bone]);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    RequireScratch(frame, scratch, output, 16);
                    if (output - scratch >= 0x8000)
                    {
                        throw new NotSupportedException("Native Breath body vertices overlap their selected centroid-index bank.");
                    }
                    NativeBattleEffectArena captured = NativeBattleEffectNodeContextMemory.Copy(source.ReadGeometry(at, 6), 0, ReadBytes(frame, output, 16), 0, 6).Write(6, BitConverter.GetBytes(bone));
                    NativeBattleVector local = new(Signed(captured, 0), Signed(captured, 2), Signed(captured, 4));
                    NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Breath body capture requires its current data registers.")), 0, local, bone);
                    step = NativeBattleGeometry.Transform(step);
                    frame = Publish(frame, step);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        short value = unchecked((short)step.Colors.Ir[lane]!.Value);
                        captured = captured.Write(8 + lane * 2, BitConverter.GetBytes(value));
                        if (Signed(context, 30 + lane * 2) > value)
                        {
                            context = context.Write(30 + lane * 2, BitConverter.GetBytes(value));
                        }
                        else if (Signed(context, 36 + lane * 2) < value)
                        {
                            context = context.Write(36 + lane * 2, BitConverter.GetBytes(value));
                        }
                    }
                    frame = WriteBytes(frame, output, captured, 16);
                    output = checked(output + 16);
                    at = checked(at + 6);
                }
            }
            at = unchecked((at + 3) & ~3u);
            ushort polygons = unchecked((ushort)(Word(context, 44) + unchecked((ushort)Read16(source, at)) + unchecked((ushort)Read16(source, at + 2))));
            context = context.Write(44, BitConverter.GetBytes(polygons)).Write(24, BitConverter.GetBytes(unchecked((ushort)(item + 1))));
        }
        short maximum = short.MinValue;
        for (int lane = 0; lane < 3; lane++)
        {
            short extent = unchecked((short)(Signed(context, 36 + lane * 2) - Signed(context, 30 + lane * 2)));
            context = context.Write(36 + lane * 2, BitConverter.GetBytes(extent));
            maximum = lane == 0 ? extent : Math.Max(maximum, extent);
        }
        context = context.Write(42, BitConverter.GetBytes(maximum));
        frame = PublishContext(frame, temporary, context, 52);
        return CaptureFaces6B76E0(frame, temporary, source);
    }

    public static NativeBattleBreath129Frame CaptureFaces6B76E0(NativeBattleBreath129Frame frame, uint temporary, NativeBattleBreath129BodySource source)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        uint scratch = Dword(context, 0);
        int faces = Signed(context, 44);
        uint centroid = unchecked(scratch + 0x8002u + (uint)(faces * 2));
        context = context.Write(20, BitConverter.GetBytes(centroid)).Write(24, new byte[2]).Write(28, new byte[2]);
        frame = PublishContext(frame, temporary, context, 52);
        int objects = unchecked((int)Read32(source, source.GeometryAddress));
        int captured = 0;
        ushort totalVertices = 0;
        for (int item = 0; item < objects; item++)
        {
            ushort start = totalVertices;
            uint at = unchecked(source.GeometryAddress + Read32(source, checked(source.GeometryAddress + 4u + (uint)item * 4)));
            int groups = Read16(source, at);
            at = checked(at + 2);
            for (int group = 0; group < groups; group++)
            {
                int count = Read16(source, checked(at + 2));
                if (count < 0)
                {
                    throw new InvalidDataException("Native Breath face capture requires its actual nonnegative packed vertex count.");
                }
                totalVertices = unchecked((ushort)(totalVertices + count));
                at = checked(at + 4u + (uint)count * 6);
            }
            at = unchecked((at + 3) & ~3u);
            ushort triangles = unchecked((ushort)Read16(source, at));
            ushort quads = unchecked((ushort)Read16(source, at + 2));
            at = checked(at + 12);
            context = Context(frame, temporary, 52).Write(26, BitConverter.GetBytes(start)).Write(28, BitConverter.GetBytes(totalVertices)).Write(16, BitConverter.GetBytes(triangles)).Write(18, BitConverter.GetBytes(quads));
            frame = PublishContext(frame, temporary, context, 52);
            for (int kind = 0; kind < 2; kind++)
            {
                int count = kind == 0 ? triangles : quads;
                for (int polygon = 0; polygon < count; polygon++)
                {
                    int[] indices = new int[kind == 0 ? 3 : 4];
                    for (int corner = 0; corner < indices.Length; corner++)
                    {
                        int local = unchecked((ushort)Read16(source, at + (uint)corner * 2)) & 0xFFF;
                        if (local >= unchecked((ushort)(totalVertices - start)))
                        {
                            throw new InvalidDataException("Native Breath centroid index leaves its actual geometry object.");
                        }
                        indices[corner] = unchecked((ushort)(local + start));
                    }
                    context = Context(frame, temporary, 52);
                    uint pointer = Dword(context, 20);
                    RequireScratch(frame, scratch, checked(scratch + 0x8000u + (uint)captured * 2), 2);
                    frame = PutWord(frame, checked(scratch + 0x8000u + (uint)captured * 2), unchecked((ushort)((pointer - scratch - 0x8000) >> 1)));
                    frame = Centroid6B7910(frame, temporary, indices[0], indices[1], indices[2], kind == 0 ? -1 : indices[3]);
                    captured++;
                    at = checked(at + (kind == 0 ? 16u : 20u));
                    int remainingOffset = kind == 0 ? 16 : 18;
                    frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(remainingOffset, BitConverter.GetBytes(unchecked((ushort)(count - polygon - 2)))), 52);
                }
                if (count == 0)
                {
                    frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(kind == 0 ? 16 : 18, BitConverter.GetBytes(ushort.MaxValue)), 52);
                }
            }
            frame = PublishContext(frame, temporary, Context(frame, temporary, 52).Write(24, BitConverter.GetBytes(unchecked((ushort)(item + 1)))), 52);
        }
        uint last = Dword(Context(frame, temporary, 52), 20);
        RequireScratch(frame, scratch, checked(scratch + 0x8000u + (uint)captured * 2), 2);
        return PutWord(frame, checked(scratch + 0x8000u + (uint)captured * 2), unchecked((ushort)((last - scratch - 0x8000) >> 1)));
    }

    public static NativeBattleBreath129Frame Centroid6B7910(NativeBattleBreath129Frame frame, uint temporary, int first, int second, int third, int fourth)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        uint scratch = Dword(context, 0);
        uint target = Dword(context, 20);
        RequireScratch(frame, scratch, target, 12);
        int[] indices = fourth < 0 ? [first, second, third] : [first, second, third, fourth];
        NativeBattleEffectArena output = ReadBytes(frame, target, 12);
        for (int lane = 0; lane < 3; lane++)
        {
            short sum = 0;
            foreach (int index in indices)
            {
                sum = unchecked((short)(sum + Signed(ReadBytes(frame, checked(scratch + (uint)index * 16 + 8u + (uint)lane * 2), 2), 0)));
            }
            output = output.Write(lane * 2, BitConverter.GetBytes(unchecked((short)(sum / indices.Length))));
        }
        output = output.Write(6, BitConverter.GetBytes(unchecked((ushort)first))).Write(8, BitConverter.GetBytes(unchecked((ushort)second))).Write(10, BitConverter.GetBytes(unchecked((ushort)third)));
        frame = WriteBytes(frame, target, output, 12);
        return PublishContext(frame, temporary, context.Write(20, BitConverter.GetBytes(checked(target + 12))), 52);
    }

    public static (NativeBattleBreath129Frame Frame, short Selector) Select6B7A60(NativeBattleBreath129Frame frame, uint temporary, short first, short desired, NativeBattleBreath129Resources resources)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        uint scratch = Dword(context, 0);
        uint selected = Selected(frame, scratch, first);
        short best = first;
        int error = 32767;
        for (int index = 0; index < Signed(context, 44); index++)
        {
            uint candidate = Selected(frame, scratch, checked((short)index));
            short[] delta = new short[3];
            for (int lane = 0; lane < 3; lane++)
            {
                delta[lane] = unchecked((short)(Signed(ReadBytes(frame, selected + (uint)lane * 2, 2), 0) - Signed(ReadBytes(frame, candidate + (uint)lane * 2, 2), 0)));
                context = context.Write(8 + lane * 2, BitConverter.GetBytes(delta[lane]));
            }
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadSignedIr45DC40(new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Breath distance requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Breath distance requires its current data registers.")), new(delta[0], delta[1], delta[2]));
            registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
            int squared = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
            registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)squared)) };
            int distance = NativeBattleQuezacotlRibbonMath.SqrtQ12(squared, resources.SqrtWords256) >> 12;
            int difference = unchecked(distance - desired);
            if (difference < 0)
            {
                difference = unchecked(-difference);
            }
            if (unchecked((short)error) > difference)
            {
                error = difference;
                best = checked((short)index);
            }
            frame = Publish(frame, registers);
        }
        return (PublishContext(frame, temporary, context, 52), best);
    }

    public static NativeBattleBreath129Frame Copy6B7BA0(NativeBattleBreath129Frame frame, uint temporary, short selector)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 52);
        uint scratch = Dword(context, 0);
        uint selected = Selected(frame, scratch, selector);
        uint output = Dword(context, 48);
        for (int corner = 0; corner < 3; corner++)
        {
            short index = Signed(ReadBytes(frame, selected + 6u + (uint)corner * 2, 2), 0);
            uint row = unchecked(scratch + (uint)(index * 16));
            frame = WriteBytes(frame, checked(output + (uint)corner * 8), ReadBytes(frame, row, 8), 8);
        }
        return PublishContext(frame, temporary, context.Write(48, BitConverter.GetBytes(checked(output + 24))), 52);
    }

    private static uint Selected(NativeBattleBreath129Frame frame, uint scratch, short selector)
    {
        short offset = Signed(ReadBytes(frame, unchecked(scratch + 0x8000u + (uint)(selector * 2)), 2), 0);
        return unchecked(scratch + 0x8000u + (uint)(offset * 2));
    }
    private static void RequireScratch(NativeBattleBreath129Frame frame, uint start, uint address, int length)
    {
        uint working = frame.State.ReadDword(0x252115C);
        uint first = checked(working + 0x3B70);
        uint second = checked(working + 0x1BB70);
        uint end = start >= first && start < second ? second : start >= second && start < second + 0x18000 ? checked(second + 0x18000) : throw new InvalidDataException("Native Breath scratch cursor leaves its actual private packet banks.");
        if (address < start || (ulong)address + (uint)length > end)
        {
            throw new InvalidDataException("Native Breath capture exceeds its current remaining private scratch owner.");
        }
    }
    private static NativeBattleBreath129Frame PutWord(NativeBattleBreath129Frame frame, uint address, ushort value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    private static NativeBattleBreath129Frame Publish(NativeBattleBreath129Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static uint Read32(NativeBattleBreath129BodySource source, uint address) => Dword(source.ReadGeometry(address, 4), 0);
    private static short Read16(NativeBattleBreath129BodySource source, uint address) => Signed(source.ReadGeometry(address, 2), 0);
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena arena, int offset) => unchecked((short)Word(arena, offset));
}
