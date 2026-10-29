using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Controller
{
    public static NativeBattleEffect207Step AdvanceMain6A3D90(NativeBattleEffect207Frame frame, uint address, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if ((uint)age < 80)
        {
            NativeBattleEffectArena caller = CopyBytes(TaskView(Read(frame, address), 40, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 20), 0, 8).Write(8, BitConverter.GetBytes(SelectorsAddress)).Write(12, BitConverter.GetBytes((short)-128)).Write(14, BitConverter.GetBytes(unchecked((ushort)(age * 32)))).Write(16, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0xCE8u));
            frame = NativeBattleEffect207Programme.Advance(frame, address + ProgrammeWorkspaceOffset, caller, resources, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(207, Read(frame, address))).Frame;
        }
        int elapsed = age - 32;
        if ((uint)elapsed < 48)
        {
            frame = DrawWarp(frame, address, elapsed, resources, inputs);
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect207Sound501330(SoundAddress, 0, 128));
        }
        if ((uint)(age - 48) < 16)
        {
            frame = BirthParticles(frame, address);
        }
        if (age < 80)
        {
            return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
        }
        if (age == 80)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect207 requires its current header.");
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
            frame = inputs.Execute(frame, new NativeBattleEffect207AllContacts506BA0(header.FirstContactIndex ?? throw new NotSupportedException("Effect207 requires its actual contact pointer."), header.ReadByte(16)));
            frame = inputs.Execute(frame, new NativeBattleEffect207AppendCommonTextureLoad508630(frame.State.ReadDword(ArenaGlobal) + 0x1A2Cu, address + 16));
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 81));
        }
        return new(frame, Read(frame, address).ReadByte(16) != 0 ? 2 : 0);
    }

    private static NativeBattleEffect207Frame DrawWarp(NativeBattleEffect207Frame frame, uint address, int elapsed, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleEffectArena rectangle = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32767)).Write(2, BitConverter.GetBytes((short)32767)).Write(4, BitConverter.GetBytes((short)-32767)).Write(6, BitConverter.GetBytes((short)-32767));
        int metric = 65536;
        bool included = false;
        int count = Signed(Read(frame, address), 14);
        for (int index = 0; index < count; index++)
        {
            uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address + 20u + (uint)(index * 4), 4).Read(0, 4));
            NativeBattleEffect207BoundsStep bounds = NativeBattleEffect207Lower.Bounds5F10(frame, model, rectangle, metric, resources, inputs);
            frame = bounds.Frame;
            rectangle = bounds.Rect8;
            metric = bounds.MinimumDepth;
            included |= bounds.Included;
        }
        metric = unchecked(metric - 128);
        if (!included || metric <= 0)
        {
            return frame;
        }
        short width = unchecked((short)(Word(rectangle, 4) - Word(rectangle, 0)));
        short height = unchecked((short)(Word(rectangle, 6) - Word(rectangle, 2)));
        int centerX = unchecked((short)(Word(rectangle, 0) + width / 2));
        int centerY = unchecked((short)(Word(rectangle, 2) + height / 2));
        NativeBattleEffectArena centroid = TaskView(Read(frame, address), 40, 8);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect207 requires its actual projection controls.");
        NativeBattleGeometryRegisterStep projected = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(centroid), Word(centroid, 6)), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        frame = Publish(frame, projected);
        uint screen = projected.Geometry.Sxy[2]!.Value;
        short depth = unchecked((short)projected.Geometry.Sz[3]!.Value);
        _ = unchecked((short)screen) / 8;
        _ = unchecked((short)(screen >> 16)) / 8;
        short minimum = unchecked((short)(0x18000 / depth));
        width = Math.Max(width, minimum);
        height = Math.Max(height, minimum);
        if (elapsed < 8)
        {
            width = unchecked((short)((uint)(width * elapsed) >> 3));
            height = unchecked((short)((uint)(height * elapsed) >> 3));
        }
        frame = frame with { State = frame.State.WriteWord(MatrixAddress, unchecked((ushort)width)).WriteWord(MatrixAddress + 8, unchecked((ushort)height)).WriteDword(MatrixAddress + 20, unchecked((uint)centerX)).WriteDword(MatrixAddress + 24, unchecked((uint)centerY)) };
        var snapshot = ReadBytes(frame, MatrixAddress, 32).ReadSnapshot(0, 32);
        frame = LoadMatrix(frame, new NativeBattlePackedMatrix32(snapshot.Data.ToImmutableArray(), snapshot.Known.ToImmutableArray()));
        int fade = 4096 - NativeBattleCameraMath.Sine(elapsed * 2048 / 48);
        return NativeBattleEffect207Lower.Draw5700(frame, MutableMeshAddress, MutableMeshAddress + 8, (metric >> 2) + 17, fade, PaletteIndex(elapsed), resources, inputs, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(207, Read(frame, address), 0x5700) with { ResourceGeneration = MutableMeshAddress });
    }

    public static int PaletteIndex(int elapsed)
    {
        int index = elapsed < 12 ? 16 - elapsed * 16 / 12 : elapsed < 24 ? (elapsed - 12) * 16 / 12 : 16 + (elapsed - 24) * 16 / 12;
        if (index > 31)
        {
            index = 64 - index;
        }
        return Math.Min(index, 31);
    }

    public static NativeBattleEffect207Frame BirthParticles(NativeBattleEffect207Frame frame, uint main)
    {
        for (int index = 0; index < 4; index++)
        {
            (frame, uint particle) = Append(frame, ParticleDescriptor, 0x6A5650);
            if (particle == 0)
            {
                break;
            }
            frame = Write(frame, particle, Read(frame, particle).WriteWord(18, 0));
            frame = frame with { Random = frame.Random.Draw(out ushort first) };
            frame = frame with { Random = frame.Random.Draw(out ushort second) };
            NativeBattleEffectArena direction = NativeBattleEffect207Lower.Direction6020(second, first, TaskView(Read(frame, particle), 12, 8));
            frame = frame with { Random = frame.Random.Draw(out ushort magnitude) };
            int factor = (magnitude & 0x7FF) + 0x800;
            NativeBattleVector vector = Vector(direction);
            int x = vector.X * factor >> 12;
            int y = vector.Y * factor >> 12;
            int z = vector.Z * factor >> 12;
            NativeBattleModulePoolTask task = Read(frame, particle).WriteWord(20, 0).WriteWord(22, 0).WriteWord(24, 0).WriteByte(26, unchecked((byte)(-x >> 7))).WriteByte(27, unchecked((byte)(-y >> 7))).WriteByte(28, unchecked((byte)(-z >> 7)));
            NativeBattleModulePoolTask source = Read(frame, main);
            task = task.WriteWord(12, unchecked((ushort)(x + Signed(source, 40)))).WriteWord(14, unchecked((ushort)(y + Signed(source, 42)))).WriteWord(16, unchecked((ushort)(z + Signed(source, 44))));
            frame = Write(frame, particle, task);
        }
        return frame;
    }

    private static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
}
