using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame DrawFragments664020(NativeBattleEffect314Frame frame, uint main, NativeBattleEffect314Inputs inputs)
    {
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = LoadMatrix(frame, view);
        NativeBattleEffect314AddressStep allocation = inputs.AllocateTemporary(frame, 8);
        frame = allocation.Frame;
        uint temporary = allocation.Address;
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect314 fragments require their actual projection controls.");
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        for (int index = 0; index < FragmentCapacity; index++)
        {
            uint address = checked(main + FragmentOffset + (uint)(index * FragmentStride));
            NativeBattleEffectArena row = ReadBytes(frame, address, FragmentStride);
            ushort kind = U16(row, 6);
            if (kind == 0)
            {
                continue;
            }
            bool quad = kind == 2;
            int count = quad ? 4 : 3;
            int length = quad ? 40 : 32;
            System.Numerics.Vector3[]? worlds = frame.PilotIdentity is null ? null : new System.Numerics.Vector3[count];
            byte[] data = new byte[length];
            var mask = new NativeBattleGfPacketByteState[length];
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleEffectArena scratch = inputs.ReadArena(frame, temporary, 8);
            for (int corner = 0; corner < 3; corner++)
            {
                scratch = PointScratch(scratch, row, corner);
                frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporary, scratch, 8));
                if (worlds is not null)
                {
                    worlds[corner] = NativeBattleEffect32Presentation.World(view.ReadMatrix(), new(I16(scratch, 0), I16(scratch, 2), I16(scratch, 4)), projection);
                }
                registers = LoadScratch(registers, corner, scratch);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            Store(12, row.Read(0x2C, 4));
            Store(20, row.Read(0x30, 4));
            Store(28, row.Read(0x34, 2));
            uint[] screen = new uint[count];
            for (int corner = 0; corner < 3; corner++)
            {
                screen[corner] = registers.Geometry.Sxy[corner] ?? throw new NotSupportedException("Effect314 fragment requires its projected SXY FIFO.");
                Store(8 + corner * 8, BitConverter.GetBytes(screen[corner]));
            }
            int clip = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                Clip(screen[corner], corner);
            }
            if (quad)
            {
                scratch = PointScratch(scratch, row, 3);
                frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporary, scratch, 8));
                if (worlds is not null)
                {
                    worlds[3] = NativeBattleEffect32Presentation.World(view.ReadMatrix(), new(I16(scratch, 0), I16(scratch, 2), I16(scratch, 4)), projection);
                }
                registers = NativeBattleGeometry.ProjectSingle(LoadScratch(registers, 0, scratch), projection, controls.Dqa, controls.Dqb);
                Store(36, row.Read(0x36, 2));
                screen[3] = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect314 quad requires its fourth projected SXY.");
                Store(32, BitConverter.GetBytes(screen[3]));
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect314 quad requires its actual ZSF4."));
                Clip(screen[3], 3);
            }
            else
            {
                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect314 triangle requires its actual ZSF3."));
            }
            int all = (1 << count) - 1;
            bool viewport = (clip & all) != all && ((clip >> 4) & all) != all;
            NativeBattleGeometryPrimitive? primitive = null;
            uint otz = registers.Geometry.Otz ?? throw new NotSupportedException("Effect314 fragment requires its averaged OTZ.");
            if (viewport)
            {
                if (!quad)
                {
                    Store(0, BitConverter.GetBytes(0x07000000u));
                    Store(4, BitConverter.GetBytes(0x24F0F0F0u));
                }
                scratch = scratch.Write(0, BitConverter.GetBytes(otz));
                frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporary, scratch, 8));
                short depth = unchecked((short)otz);
                if (depth > 0)
                {
                    if (quad)
                    {
                        Store(0, BitConverter.GetBytes(0x09000000u));
                        Store(4, BitConverter.GetBytes(0x2CF0F0F0u));
                    }
                    ImmutableArray<int> depths = quad ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect314 quad requires its actual SZ FIFO.")))).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
                    primitive = new(depth >> 2, Enumerable.Range(0, length / 4).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4, 4))).ToImmutableArray(), depths) { PhysicalWriteMask = mask.ToImmutableArray() };
                    if (worlds is not null && frame.PilotIdentity is { } owner)
                    {
                        NativeBattlePilotVisualIdentity rowIdentity = NativeBattleEffect32Presentation.TaskInstance(owner, frame.Effect, main, main + FragmentOffset, index)!.Value;
                        primitive = NativeBattleEffect32Presentation.Capture(primitive, NativeBattleEffect32Presentation.Resource(rowIdentity, address), worlds);
                    }
                }
            }
            frame = Publish(frame, registers);
            NativeBattleGfGeometryAttempt attempt = new(0, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);
            frame = inputs.Execute(frame, new NativeBattleEffect314DrawBatch(new([attempt], primitive is null ? 0 : length), CursorGlobal));

            void Store(int offset, ReadOnlySpan<byte> bytes)
            {
                bytes.CopyTo(data.AsSpan(offset));
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
            }
            void Clip(uint point, int corner)
            {
                short x = unchecked((short)point);
                short y = unchecked((short)(point >> 16));
                if (x < 0 || x > 0xA00)
                {
                    clip |= 1 << corner;
                }
                if (y < 0 || y > 0x6C0)
                {
                    clip |= 0x10 << corner;
                }
            }
        }
        return inputs.ReleaseTemporary(frame, 8);
    }

    private static NativeBattleEffectArena PointScratch(NativeBattleEffectArena scratch, NativeBattleEffectArena row, int corner)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            scratch = scratch.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(I16(row, 0x14 + corner * 6 + axis * 2) + I16(row, axis * 2)))));
        }
        return scratch;
    }

    private static NativeBattleGeometryRegisterStep LoadScratch(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena scratch)
    {
        (byte[] bytes, bool[] known) = scratch.ReadSnapshot(0, 8);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(scratch.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(scratch.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(scratch.Read(4, 2)));
        short? padding = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        return NativeBattleGeometry.LoadVector(registers, slot, vector, padding);
    }
}
