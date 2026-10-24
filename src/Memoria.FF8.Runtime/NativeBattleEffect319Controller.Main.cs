using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect319Controller
{
    public static NativeBattleEffect319Step AdvanceMain65DDA0(NativeBattleEffect319Frame frame, uint address, NativeBattleEffect319Resources resources, NativeBattleEffect319Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age == 2)
        {
            NativeBattleEffect319PointStep point = inputs.Point502170(frame, frame.State.ReadDword(CasterGlobal), 13, 2048, TaskView(Read(frame, address), 0x5C, 8));
            frame = Write(point.Frame, address, CopyTask(Read(point.Frame, address), 0x5C, point.Output8, 8));
            NativeBattleVector mouth = Vector(point.Output8);
            NativeBattleVector target = Vector(TaskView(Read(frame, address), 0x64, 8));
            int x = target.X - mouth.X;
            int y = mouth.Y - target.Y;
            int z = mouth.Z - target.Z;
            NativeBattleActionMatrix first = NativeBattleFunguarLaser277.Basis50CBA0(x, y, z).Matrix;
            frame = StoreRotation(frame, address + 0x84, first);
            for (int index = 0; index < 3; index++)
            {
                NativeBattleVector local = Vector(NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(0xFF7780u + (uint)(index * 8), 8)));
                NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(first, local);
                frame = WriteBytes(frame, address + 0x6Cu + (uint)(index * 8), PutPoint(ReadBytes(frame, address + 0x6Cu + (uint)(index * 8), 8), 0, rotated), 6);
            }
            frame = StoreRotation(frame, address + 0xA4, NativeBattleFunguarLaser277.Basis50CBA0(-x, y, -z).Matrix);
        }
        if ((uint)(age - 2) <= 6)
        {
            if (age == 2)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect319MeshMask(frame.State.ReadDword(CasterGlobal), 0xFFFFFFFD, 0));
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            short fraction = (short)(((age - 2) << 12) / 6);
            NativeBattleWeightedPointsStep blend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Vector(TaskView(task, 0x64, 8)), Vector(TaskView(task, 0x5C, 8)), fraction, (short)(4096 - fraction), inputs.Colors);
            frame = frame with { Memory = blend.Memory };
            NativeBattleVector offset = Vector(TaskView(task, 0x6C, 8));
            frame = StoreTranslation(frame, address + 0x84, blend.Position.X + offset.X, blend.Position.Y + offset.Y, blend.Position.Z + offset.Z);
            frame = AdvanceProgramme(frame, address + 0xC4, 0x368, address + 0x84, resources, inputs);
            frame = StoreTranslation(frame, address + 0x84, blend.Position.X, blend.Position.Y, blend.Position.Z);
            NativeBattlePackedMatrix32 matrix = Compose(frame, address + 0x84, inputs);
            frame = LoadMatrix(frame, matrix);
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xFF13F0u)).Write(0x1C, BitConverter.GetBytes(0u));
            frame = PublishContext(frame, temporary, context, 88);
            NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(0xFF13F0), inputs.ReadProjection(frame), context, inputs.Colors);
            frame = frame with { Memory = draw.Memory };
            frame = PublishContext(frame, temporary, draw.Context, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect319DrawBatch(draw.Batch, CursorGlobal));
            frame = Release(frame, 88);
        }
        if ((uint)(age - 7) < 32)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleVector target = Vector(TaskView(task, 0x64, 8));
            NativeBattleVector offset = Vector(TaskView(task, 0x6C, 8));
            frame = StoreTranslation(frame, address + 0xA4, target.X - offset.X, target.Y - offset.Y, target.Z - offset.Z);
            frame = AdvanceProgramme(frame, address + 0x42C, 0x46C, address + 0xA4, resources, inputs);
            int elapsed = age - 7;
            if (elapsed >= 8 && elapsed < 15 && (elapsed & 1) == 0)
            {
                frame = BirthSprites(frame, address);
            }
        }
        frame = EmitMainEvents(frame, age, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        if (Signed(Read(frame, address), 12) >= 39)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect319Queue5341D0(360, frame.State.ReadDword(ArenaGlobal) + ThirdPacketOffset, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect319Frame EmitMainEvents(NativeBattleEffect319Frame frame, int age, NativeBattleEffect319Inputs inputs)
    {
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect319Sound501330(SoundAddress, 0, 128));
        }
        if (age == 12)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect319Contact506690(FirstContact(frame, inputs.ReadPackets(frame))));
        }
        if (age == 27)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect319MeshMask(frame.State.ReadDword(CasterGlobal), uint.MaxValue, 2));
            foreach ((int bone, int delay) in new (int, int)[] { (10, 0), (13, 8), (23, 16), (24, 16), (25, 16) })
            {
                frame = NativeBattleEffect319Lower.Birth65E4B0(frame, bone, delay, 1024, 512, inputs);
            }
        }
        return frame;
    }

    private static NativeBattleEffect319Frame StoreRotation(NativeBattleEffect319Frame frame, uint address, NativeBattleActionMatrix matrix) => WriteBytes(frame, address, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 18);

    private static NativeBattleEffect319Frame StoreTranslation(NativeBattleEffect319Frame frame, uint matrix, int x, int y, int z)
    {
        return WriteBytes(frame, matrix + 20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z)), 12);
    }

    private static NativeBattlePackedMatrix32 Compose(NativeBattleEffect319Frame frame, uint worldAddress, NativeBattleEffect319Inputs inputs)
    {
        (byte[] bytes, bool[] known) = ReadBytes(frame, worldAddress, 32).ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 world = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        return NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world.ReadMatrix()), null);
    }

    private static NativeBattleEffect319Frame AdvanceProgramme(NativeBattleEffect319Frame frame, uint workspace, int length, uint world, NativeBattleEffect319Resources resources, NativeBattleEffect319Inputs inputs)
    {
        NativeBattleEffectArena caller = PackedArena(Compose(frame, world, inputs)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + MorphOffset));
        return NativeBattleEffect319Programme.Advance(frame, workspace, length, caller, resources, inputs).Frame;
    }

    public static NativeBattleEffect319Frame BirthSprites(NativeBattleEffect319Frame frame, uint main)
    {
        for (int index = 0; index < 4; index++)
        {
            (frame, uint sprite) = Append(frame, SpriteDescriptor, 0x65E430);
            if (sprite == 0)
            {
                continue;
            }
            frame = Write(frame, sprite, Read(frame, sprite).WriteWord(12, 0));
            for (int axis = 0; axis < 3; axis++)
            {
                NativeCrtRandomState random = frame.Random.Draw(out ushort value);
                frame = frame with { Random = random };
                int coordinate = Signed(Read(frame, main), 0x64 + axis * 2) + (value & 1023) - 512;
                frame = Write(frame, sprite, Read(frame, sprite).WriteWord(16 + axis * 2, unchecked((ushort)coordinate)));
            }
        }
        return frame;
    }
}
