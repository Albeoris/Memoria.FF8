using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBio22Controller
{
    private sealed partial class Execution
    {
        private NativeBattleBio22ParticleFrame Particle(uint address) => new(Read(address), Primary(address), Frame.Memory, Random);

        private void Publish(uint address, NativeBattleBio22ParticleFrame frame)
        {
            Frame = Frame with { Memory = frame.Memory };
            Random = frame.Random;
            Write(frame.Task.ReadDword(0x14), frame.Primary);
            Write(address, frame.Task);
        }

        private NativeBattleBio22ParticleFrame Billboard(uint address, NativeBattleBio22ParticleFrame frame, short scale)
        {
            Publish(address, frame);
            if ((frame.Task.ReadByte(0x26) & 4) == 0)
            {
                Execute(new NativeBattleBio22Sprite(NativeBattleBio22Sprites.Emit89BD90(frame.Task, scale, inputs.Projection, inputs.CameraRoll, resources.Sprite(frame.Task.ReadDword(0x4C)))));
            }
            return Particle(address);
        }

        private uint AdvanceParticle(uint address, uint callback)
        {
            NativeBattleBio22ParticleInputs particleInputs = new((current, scale) => Billboard(address, current, scale), current =>
            {
                Publish(address, current);
                if ((current.Task.ReadByte(0x26) & 4) == 0)
                {
                    Execute(new NativeBattleBio22Packets(NativeBattleBio22Sprites.Emit89BF80(current.Task, inputs.Projection, inputs.CameraRoll, resources.Sprite(current.Task.ReadDword(0x4C)))));
                }
                return Particle(address);
            }, (current, request) =>
            {
                Publish(address, current);
                uint child = Append(request.ParentAddress, NativeBattleBio22BillboardAppend.Callback, NativeBattleBio22BillboardAppend.Size, 3, true);
                NativeBattleModulePoolTask created = Read(child).WriteWord(0x68, request.Variant);
                for (int index = 0; index < 4; index++)
                {
                    created = created.WriteDword(0x30 + index * 4, BinaryPrimitives.ReadUInt32LittleEndian(request.ScaleBlock16.AsSpan().Slice(index * 4)));
                }
                Write(child, created);
                return Particle(address);
            });
            NativeBattleBio22ParticleFrame current = Particle(address);
            NativeBattleBio22ParticleStep step = callback switch
            {
                0x89BD20 => NativeBattleBio22Particles.Advance89BD20(current, particleInputs),
                0x89BF20 => NativeBattleBio22Particles.Advance89BF20(address, current, particleInputs),
                0x89C930 => NativeBattleBio22Particles.Advance89C930(current, particleInputs),
                0x89CE60 => NativeBattleBio22LaterParticles.Advance89CE60(current, (next, scale) => Billboard(address, next, scale)),
                0x89D120 => NativeBattleBio22LaterParticles.Advance89D120(current, (next, scale) => Billboard(address, next, scale)),
                _ => throw new NotSupportedException("Native Bio particle callback is unsupported.")
            };
            Publish(address, step.Frame);
            return step.Result;
        }

        private NativeBattleBio22EmitterFrame Emitter(uint address) => new(Read(address), Primary(address), Frame.Memory, Random);

        private void Publish(uint address, NativeBattleBio22EmitterFrame frame)
        {
            Frame = Frame with { Memory = frame.Memory };
            Random = frame.Random;
            Write(frame.Task.ReadDword(0x14), frame.Primary);
            Write(address, frame.Task);
        }

        private uint AdvanceEmitter(uint address, uint callback)
        {
            NativeBattleBio22EmitterSpawn AppendChild(NativeBattleBio22EmitterFrame current, uint parent, uint childCallback)
            {
                Publish(address, current);
                uint child = Append(parent, childCallback, 0x70, 3, true);
                return new(Emitter(address), child, Read(child));
            }

            NativeBattleBio22EmitterFrame PublishChild(NativeBattleBio22EmitterFrame current, uint child, NativeBattleModulePoolTask task)
            {
                Publish(address, current);
                Write(child, task);
                return Emitter(address);
            }

            NativeBattleBio22EmitterFrame current = Emitter(address);
            NativeBattleBio22EmitterStep step = callback switch
            {
                0x89BA80 => NativeBattleBio22TimedEmitter.Advance89BA80(address, current, resources, new(AppendChild, PublishChild, next => unchecked((short)(PositionY(next.Task) + S(next.Primary, 0x4A))))),
                0x89CD60 => NativeBattleBio22LaterEmitters.Advance89CD60(address, current, resources, new(AppendChild, PublishChild)),
                0x89D020 => NativeBattleBio22LaterEmitters.Advance89D020(address, current, resources, new(AppendChild, PublishChild)),
                _ => throw new NotSupportedException("Native Bio emitter callback is unsupported.")
            };
            Publish(address, step.Frame);
            return step.Result;
        }

        private NativeBattleBio22SecondGeometryFrame SecondGeometry(uint address) => new(Read(address), Primary(address), Frame.Memory, Random);

        private void Publish(uint address, NativeBattleBio22SecondGeometryFrame frame)
        {
            Frame = Frame with { Memory = frame.Memory };
            Random = frame.Random;
            Write(frame.Task.ReadDword(0x14), frame.Primary);
            Write(address, frame.Task);
        }

        private uint AdvanceSecondGeometry(uint address)
        {
            NativeBattleBio22SecondGeometryInputs geometryInputs = new((current, mesh, deformed) =>
            {
                Publish(address, current);
                Execute(new NativeBattleBio22Packets(deformed ? NativeBattleBio22GeometryPackets.Emit89AD30(current.Task, mesh.Bytes, inputs.Projection, inputs.Zsf3) : NativeBattleBio22GeometryPackets.Emit89AB90(current.Task, mesh.Bytes, inputs.Projection, inputs.Zsf3)));
                return SecondGeometry(address);
            }, (current, request) =>
            {
                Publish(address, current);
                uint child = Append(request.ParentAddress, NativeBattleBio22ParticleAppend.Callback, NativeBattleBio22ParticleAppend.Size, 3, true);
                NativeBattleModulePoolTask created = Read(child);
                created = Vector(created, 0x1C, request.Position);
                created = Vector(created, 0x58, request.Velocity);
                created = Vector(created, 0x60, request.Acceleration);
                Write(child, created);
                return SecondGeometry(address);
            }, (current, request) =>
            {
                Publish(address, current);
                uint child = Append(request.ParentAddress, NativeBattleBio22OverlayAppend.Callback, NativeBattleBio22OverlayAppend.Size, 5, true);
                Write(child, Read(child).WriteDword(0x30, request.ProgramAddress));
                return SecondGeometry(address);
            });
            NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(address, SecondGeometry(address), resources, geometryInputs);
            Publish(address, step.Frame);
            return step.Result;
        }

        private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    }
}
