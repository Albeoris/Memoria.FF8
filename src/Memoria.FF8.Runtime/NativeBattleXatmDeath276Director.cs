using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleXatmDeath276Controller
{
    private sealed partial class Execution
    {
        private int Director(int index)
        {
            NativeBattleModulePoolTask task = Read(7, index);
            int counter = Word(task, 12);
            if (counter >= 144)
            {
                return 2;
            }
            NativeBattleActionMatrix view = inputs.Projection.View ?? throw new NotSupportedException("Native6851F0 requires its actual current camera matrix.");
            short zsf3 = Frame.Memory.ScriptedGraphics?.Zsf3 ?? throw new NotSupportedException("Native6851F0 requires its current GTE ZSF3 register.");
            short zsf4 = Frame.Memory.ScriptedGraphics?.Zsf4 ?? throw new NotSupportedException("Native6851F0 requires its current GTE ZSF4 register.");
            Execute(new NativeBattleXatmDeath276Packets(NativeBattleXatmDeath276DebrisDrawing.DrawDebris(task, view, inputs.Projection, zsf3, zsf4, inputs.CapturePresentation)));
            if (counter is >= 100 and < 116)
            {
                Execute(new NativeBattleXatmDeath276White((counter - 100) << 8));
            }
            else if (counter is >= 116 and < 124)
            {
                Execute(new NativeBattleXatmDeath276White(4096));
            }
            else if (counter is >= 124 and < 144)
            {
                Execute(new NativeBattleXatmDeath276White(4096 - ((counter - 124) << 12) / 20));
            }
            if (counter <= 108)
            {
                byte[] casterContext = new byte[55];
                Span<bool> casterKnown = stackalloc bool[55];
                Read(7, index).ReadWithKnownness(14, casterContext, casterKnown);
                Execute(new NativeBattleXatmDeath276Caster(casterContext.ToImmutableArray()));
            }
            NativeBattleGraphicsModel caster = inputs.ReadCaster();
            Execute(new NativeBattleXatmDeath276Model(caster with { Flags0 = (ushort)(caster.Flags0 | 4) }));
            if (counter == 0)
            {
                Execute(new NativeBattleXatmDeath276Sound(resources.Sound));
            }
            else if (counter == 140)
            {
                Execute(new NativeBattleXatmDeath276StopVibration(unchecked((int)Read(7, index).ReadDword(0x4C))));
            }
            Put(7, index, NativeBattleXatmDeath276Debris.MoveDebris(Read(7, index), Frame.Effect.Pools[7].BankAddress));
            int selected = counter switch
            {
                0 => 0,
                12 => 1,
                38 => 2,
                50 => 3,
                65 => 4,
                _ => -1
            };
            if (selected >= 0)
            {
                int eventSlot = TryAppend(6, 0x685CF0);
                if (eventSlot >= 0)
                {
                    Put(6, eventSlot, Read(6, eventSlot).WriteDword(12, 0).WriteDword(16, resources.Events[selected].Address));
                }
            }
            Put(7, index, Add(Read(7, index), 12, 1));
            return 0;
        }
    }
}

public static class NativeBattleXatmDeath276DebrisDrawing
{
    public static NativeBattleGfGeometryBatch DrawDebris(NativeBattleModulePoolTask director, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short zsf3, short zsf4)
    {
        return DrawDebris(director, view, projection, zsf3, zsf4, false);
    }

    public static NativeBattleGfGeometryBatch DrawDebris(NativeBattleModulePoolTask director, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short zsf3, short zsf4, bool capturePresentation)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 128; index++)
        {
            int record = 0x54 + index * 0x48;
            int kind = Word(record + 6);
            if (kind == 0)
            {
                continue;
            }
            bool quad = kind == 2;
            int count = quad ? 4 : 3;
            System.Numerics.Vector3[]? world = capturePresentation ? new System.Numerics.Vector3[count] : null;
            NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < count; corner++)
            {
                int source = record + 0x24 + corner * 6;
                NativeBattleVector point = new(unchecked((short)(Word(record) + Word(source))), unchecked((short)(Word(record + 2) + Word(source + 2))), unchecked((short)(Word(record + 4) + Word(source + 4))));
                vertices[corner] = NativeBattleModuleProjection.Project45FE10(view, point, projection);
                if (world is not null)
                {
                    world[corner] = new(point.X, point.Y, point.Z);
                }
            }
            int size = quad ? 40 : 32;
            byte[] bytes = new byte[size];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[size];
            void Write32(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
            void Write16(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
            Write32(12, director.ReadDword(record + 0x3C));
            Write32(20, director.ReadDword(record + 0x40));
            Write16(28, director.ReadWord(record + 0x44));
            Write32(8, vertices[0].Screen);
            Write32(16, vertices[1].Screen);
            Write32(24, vertices[2].Screen);
            int horizontal = 0;
            int vertical = 0;
            for (int corner = 0; corner < count; corner++)
            {
                short x = unchecked((short)vertices[corner].Screen);
                short y = unchecked((short)(vertices[corner].Screen >> 16));
                if (x is < 0 or > 2560)
                {
                    horizontal++;
                }
                if (y is < 0 or > 1728)
                {
                    vertical++;
                }
            }
            if (quad)
            {
                Write16(36, director.ReadWord(record + 0x46));
                Write32(32, vertices[3].Screen);
            }
            bool clipped = horizontal == count || vertical == count;
            int depth = Math.Clamp(unchecked(vertices.Sum(vertex => (int)vertex.Depth) * (quad ? zsf4 : zsf3)) >> 12, 0, 65535);
            bool accepted = !clipped && unchecked((short)depth) > 0;
            if (!quad && !clipped)
            {
                Write32(0, 0x07000000);
                Write32(4, 0x24F0F0F0);
            }
            else if (quad && accepted)
            {
                Write32(0, 0x09000000);
                Write32(4, 0x2CF0F0F0);
            }
            NativeBattleGeometryPrimitive? primitive = null;
            if (accepted)
            {
                ImmutableArray<uint> words = Enumerable.Range(0, size / 4).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(word * 4))).ToImmutableArray();
                ImmutableArray<int> payload = quad ? vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray() : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
                primitive = new(depth >> 2, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() };
                if (world is not null)
                {
                    NativeBattleTaskBankOwner owner = director.RequireOwner();
                    uint address = checked(owner.Address + (uint)(director.PilotSlot * owner.Stride + record));
                    NativeBattlePilotVisualIdentity identity = NativeBattleSelectedProgrammeVisuals.Workspace(276, director, address, 0x687150, index);
                    primitive = primitive with { PilotGeometry = new(identity, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)count)) { SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
                }
            }
            attempts.Add(new(allocated, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (accepted)
            {
                allocated += size;
            }
        }
        return new(attempts.ToImmutable(), allocated);
        short Word(int offset) => unchecked((short)director.ReadWord(offset));
    }
}

