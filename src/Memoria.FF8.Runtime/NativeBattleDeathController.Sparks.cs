using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    private sealed partial class Execution
    {
        private void Burst(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (Stage(task) == 0)
            {
                int actor = task.ReadByte(0x2D);
                NativeBattleVector position = NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), 0xF1, 0);
                Store(address, Next(task.WriteWord(0x1C, unchecked((ushort)position.X)).WriteWord(0x1E, unchecked((ushort)position.Y)).WriteWord(0x20, unchecked((ushort)position.Z))));
                return;
            }
            if (Stage(task) != 1)
            {
                return;
            }
            short yaw = inputs.ReadModel(task.ReadByte(0x2D)).Rotation14 ?? throw new NotSupportedException("Native Death sparks require the target's current yaw WORD.");
            NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
            basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, 0x155);
            basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, 0xEAB);
            int age = S(task, 0x24);
            int steep = age switch { 1 => 4, 2 => 3, 3 => 2, 4 => 1, _ => 0 };
            int flat = age switch { 1 or 2 => 2, 3 or 4 => 1, _ => 0 };
            for (int index = 0; index < steep; index++)
            {
                Group(false);
            }
            for (int index = 0; index < flat; index++)
            {
                Group(true);
            }
            if (age >= 4)
            {
                Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
            }

            void Group(bool shallow)
            {
                uint child = Append(6, 0x8BDBD0, 0x118, address);
                NativeBattleModulePoolTask spark = Read(child);
                for (int index = 0; index < 4; index++)
                {
                    spark = spark.WriteDword(0xD4 + index * 8, task.ReadDword(0x1C)).WriteDword(0xD8 + index * 8, task.ReadDword(0x20));
                    short angle = (short)(Roll() & 4095);
                    int pitch = ((Roll() & (shallow ? 255 : 511)) + (shallow ? 896 : -256)) & 4095;
                    NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(basis, angle);
                    matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, (short)pitch);
                    spark = WriteMatrix(spark, 0x54 + index * 32, matrix);
                    short speed = unchecked((short)(-4096 - (Roll() & 8191)));
                    NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(S(spark, 0xF4 + index * 8), speed, S(spark, 0xF8 + index * 8)));
                    spark = spark.WriteWord(0xF4 + index * 8, unchecked((ushort)velocity.X)).WriteWord(0xF6 + index * 8, unchecked((ushort)velocity.Y)).WriteWord(0xF8 + index * 8, unchecked((ushort)velocity.Z));
                }
                Store(child, spark);
            }
        }

        private void Spark(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            int age = S(task, 0x24);
            if (Stage(task) == 0)
            {
                task = Next(task.WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x4C, 0x162B86C).WriteByte(0x40, 40).WriteByte(0x41, 0).WriteByte(0x42, 10));
            }
            else if (Stage(task) == 1 && age >= 14)
            {
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            }
            for (int index = 0; index < 4; index++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    int velocity = 0xF4 + index * 8 + lane * 2;
                    short speed = S(task, velocity);
                    speed = unchecked((short)(speed - speed / 4));
                    task = task.WriteWord(velocity, unchecked((ushort)speed));
                    int position = velocity - 32;
                    task = task.WriteWord(position, unchecked((ushort)(S(task, position) + speed / 16)));
                }
            }
            if ((uint)age >= 16)
            {
                throw new InvalidDataException("Native Death spark schedule leaves its original sixteen rows.");
            }
            int scaleX = Table(0);
            int scaleY = Table(32);
            int scaleZ = Table(64);
            short fade = Table(96);
            task = task.WriteDword(0x30, unchecked((uint)scaleX)).WriteDword(0x34, unchecked((uint)scaleY)).WriteDword(0x38, unchecked((uint)scaleZ)).WriteWord(0x50, unchecked((ushort)fade));
            Store(address, task);
            for (int index = 0; index < 4; index++)
            {
                NativeBattleVector position = new(S(task, 0xD4 + index * 8), S(task, 0xD6 + index * 8), S(task, 0xD8 + index * 8));
                task = task.WriteDword(0x1C, task.ReadDword(0xD4 + index * 8)).WriteDword(0x20, task.ReadDword(0xD8 + index * 8));
                NativeBattleActionMatrix matrix = ReadMatrix(task, 0x54 + index * 32) with { X = position.X, Y = position.Y, Z = position.Z };
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scaleX, scaleY, scaleZ);
                matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Death sparks require the current camera matrix."), matrix);
                NativeBattleEsunaMeshContext context = new(0xF0, task.ReadDword(0x40), fade, S(task, 0x52), 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
                NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(resources.Spark.Bytes, matrix, inputs.Projection, context, inputs.Colors);
                PublishSparkTemporary();
                Emit(new NativeBattleDeathPackets(batch));
            }
            Store(address, task);

            short Table(int start) => BinaryPrimitives.ReadInt16LittleEndian(resources.SparkTables.AsSpan().Slice(start + age * 2));
        }

        private void PublishSparkTemporary()
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Death requires its actual primitive process.");
            NativeBattlePrimitiveTemporaryLanes lanes = process.Temporary5082B0Lanes ?? throw new NotSupportedException("Native Death spark context requires its retained shared temporary lanes.");
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(resources.Spark.Bytes.AsSpan());
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            foreach (int stride in strides)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Spark.Bytes.AsSpan().Slice(cursor));
                cursor = checked(cursor + 4 + count * stride);
            }
            lanes = lanes with { Word2E = unchecked((ushort)((resources.Spark.Address + (uint)cursor) >> 16)) };
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? throw new NotSupportedException("Native Death spark context requires its current color register owner.");
            current = current with { Memory = current.Memory with { PrimitiveProcess = process with { Temporary5082B0Lanes = lanes, ModelRendererRegisters = registers.PublishModelColor(0xA0028) } } };
        }

        private static NativeBattleModulePoolTask WriteMatrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            return task.WriteWord(offset + 18, 0).WriteDword(offset + 20, unchecked((uint)matrix.X)).WriteDword(offset + 24, unchecked((uint)matrix.Y)).WriteDword(offset + 28, unchecked((uint)matrix.Z));
        }

        private static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => S(task, offset + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
    }
}
