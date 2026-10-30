using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Particles
{
    public static NativeBattleEffect69Frame DrawDebris6FF2F0(NativeBattleEffect69Frame frame, NativeBattleEffect69Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        (frame, uint temporaryAddress) = Allocate(frame, 8);
        NativeBattleEffectArena temporary = ReadBytes(frame, temporaryAddress, 8);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect69 debris requires its current projection controls.");
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 364; index++)
        {
            NativeBattleEffectArena record = ReadBytes(frame, arena + (uint)index * 60, 60);
            short kind = I16(record, 6);
            if (kind == 0)
            {
                continue;
            }
            bool quad = kind == 2;
            int corners = quad ? 4 : 3;
            uint[] screens = new uint[4];
            int[] depths = new int[4];
            for (int corner = 0; corner < 3; corner++)
            {
                temporary = DebrisPoint(record, corner, temporary);
                registers = LoadParticleVector(registers, corner, temporary);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            for (int corner = 0; corner < 3; corner++)
            {
                screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                depths[corner] = unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value);
            }
            byte[] packet = new byte[quad ? 40 : 32];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[packet.Length];
            Put32(12, U32(record, 44));
            Put32(20, U32(record, 48));
            Put16(28, unchecked((ushort)I16(record, 52)));
            for (int corner = 0; corner < 3; corner++)
            {
                Put32(8 + corner * 8, screens[corner]);
            }
            int outsideX = 0;
            int outsideY = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                ObserveScreen(corner);
            }
            if (quad)
            {
                temporary = DebrisPoint(record, 3, temporary);
                registers = NativeBattleGeometry.ProjectSingle(LoadParticleVector(registers, 0, temporary), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                screens[3] = registers.Geometry.Sxy[2]!.Value;
                depths[3] = unchecked((ushort)registers.Geometry.Sz[3]!.Value);
                Put16(36, unchecked((ushort)I16(record, 54)));
                Put32(32, screens[3]);
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect69 debris requires current ZSF4."));
                ObserveScreen(3);
            }
            else
            {
                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect69 debris requires current ZSF3."));
            }
            if (outsideX == (1 << corners) - 1 || outsideY == (1 << corners) - 1)
            {
                attempts.Add(Attempt(null));
                continue;
            }
            short depth = unchecked((short)registers.Geometry.Otz!.Value);
            if (!quad)
            {
                Put32(0, 0x07000000);
                Put32(4, 0x24F0F0F0);
            }
            temporary = Dword(temporary, 0, registers.Geometry.Otz!.Value);
            if (depth <= 0)
            {
                attempts.Add(Attempt(null));
                continue;
            }
            if (quad)
            {
                Put32(0, 0x09000000);
                Put32(4, 0x2CF0F0F0);
            }
            ImmutableArray<uint> words = Enumerable.Range(0, packet.Length / 4).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(depth >> 2, words, quad ? depths.ToImmutableArray() : [depths[0], depths[1], depths[2], 0]);
            if (inputs.CapturePresentation)
            {
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(69, frame.Memory.Arena ?? throw new InvalidDataException("Effect69 capture requires its debris arena owner."), arena, index) with { ResourceGeneration = unchecked((long)U32(record, 44) << 32 | U32(record, 48)) };
                System.Numerics.Vector3 Point(int corner)
                {
                    short x = unchecked((short)(I16(record, 0) + I16(record, 20 + corner * 6)));
                    short y = unchecked((short)(I16(record, 2) + I16(record, 22 + corner * 6)));
                    short z = unchecked((short)(I16(record, 4) + I16(record, 24 + corner * 6)));
                    return new(x, y, z);
                }
                primitive = primitive with { PilotGeometry = new(identity, Point(0), Point(1), Point(2), quad ? Point(3) : default, (byte)corners) { SourceDepths = new(depths[0], depths[1], depths[2], quad ? depths[3] : 0), UseSourceDepths = true } };
            }
            attempts.Add(Attempt(primitive));
            allocated += packet.Length;

            void ObserveScreen(int corner)
            {
                short x = unchecked((short)screens[corner]);
                short y = unchecked((short)(screens[corner] >> 16));
                if (x < 0 || x > 2560)
                {
                    outsideX |= 1 << corner;
                }
                if (y < 0 || y > 1728)
                {
                    outsideY |= 1 << corner;
                }
            }
            void Put16(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
            void Put32(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
            NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        }
        frame = WriteBytes(Publish(frame, registers), temporaryAddress, temporary, 8);
        frame = inputs.Execute(frame, new NativeBattleEffect69DeliverCommonBatch(new(attempts.ToImmutable(), allocated)));
        return Release(frame, 8);
    }

    private static NativeBattleEffectArena DebrisPoint(NativeBattleEffectArena record, int corner, NativeBattleEffectArena temporary)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            temporary = Word(temporary, axis * 2, unchecked((short)(I16(record, axis * 2) + I16(record, 20 + corner * 6 + axis * 2))));
        }
        return temporary;
    }

    private static short I16(NativeBattleEffectArena record, int offset) => BinaryPrimitives.ReadInt16LittleEndian(record.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena record, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(record.Read(offset, 4));
}
