using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    public static NativeBattleMagmaBreath48Frame InitializeEngine825440(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        _ = resources;
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadDword(0x30) != 0x15B7C88 || frame.State.ReadDword(0x15B7C88) == 0)
        {
            throw new NotSupportedException("Magma Breath requires its actual already-addressed source header.");
        }
        frame = frame with { State = frame.State.WriteDword(0x26495E8, checked(address + 0x34)) };
        NativeBattleModulePoolTask root = Read(frame, task.ReadDword(0x10));
        NativeBattleEffectParameters parameters = Parameters(frame);
        NativeBattleActionPacketsState packets = inputs.Packets;
        Func<int, NativeBattleGraphicsModel> readModel = actor => inputs.ReadModel(frame, actor);
        const int Context = 0x34;
        WriteWord(0x22, task.ReadWord(0x29A));
        WriteWord(0x24, task.ReadWord(0x29C));
        WriteContextDword(0x224, 0x15B7C88u + 4);
        WriteContextDword(0x228, 0x15B7C88u + 0x44);
        WriteContextDword(0x22C, 0x15B7C88u + 0x84);
        WriteContextDword(0x230, 0x15B7C88u + 0xC4);
        WriteWord(0x1C, root.ReadWord(0x5A));
        WriteWord(0x1E, task.ReadByte(0x2C));
        int count = unchecked((short)ReadWord(0x1C));
        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        int firstContact = packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Magma Breath requires the selected group's actual contact pointer.");
        for (int index = 0; index < count; index++)
        {
            WriteContextDword(4 + 4 * index, packets.ReadContact(checked(firstContact + index)).Primary.ActorIndex);
        }
        WriteWord(0x1A, task.ReadWord(0x29E));
        short mode = unchecked((short)ReadWord(0x24));
        if (mode == 2)
        {
            throw new NotSupportedException("Magma Breath placement mode2 requires its observed prior stack vector.");
        }
        int actor = unchecked((short)ReadWord(0x1E));
        NativeBattleGraphicsModel actorModel = readModel(actor);
        NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Magma Breath requires actorWORD36 and its model transform.");
        NativeBattleVector actorFirst = Bone(actor, 241);
        SetVector(0x1C4, actorFirst with { Y = actorTransform.Angle36 });
        SetVector(0x1D4, actorFirst with { Y = 0 });
        SetVector(0x1E4, Bone(actor, 241));
        SetVector(0x1F4, Bone(actor, 240));
        short actorBound60 = (actorModel.GeometryBounds52 ?? throw new NotSupportedException("Magma Breath requires the actual geometry bounds WORD60.")).Maximum.Y;
        SetVector(0x204, actorFirst with { Y = actorBound60 });
        if (mode == 4)
        {
            SetVector(0x1B4, new(Signed(task, 0x290), Signed(task, 0x292), Signed(task, 0x294)));
            foreach (int destination in new[] { 0x204, 0x1F4, 0x1E4, 0x1D4, 0x1C4 })
            {
                CopyVector(0x1B4, destination);
            }
        }
        for (int index = 0; index < count; index++)
        {
            int target = unchecked((int)ReadContextDword(4 + 4 * index));
            NativeBattleGraphicsModel model = readModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Magma Breath requires targetWORD36 and its model transform.");
            NativeBattleVector first = Bone(target, 241, true);
            int offset = index * 16;
            SetVector(0x54 + offset, first with { Y = transform.Angle36 });
            SetVector(0x94 + offset, first with { Y = 0 });
            SetVector(0xD4 + offset, Bone(target, 241, true));
            SetVector(0x114 + offset, Bone(target, 240, true));
            short bound60 = (model.GeometryBounds52 ?? throw new NotSupportedException("Magma Breath requires target geometry bounds WORD60.")).Maximum.Y;
            SetVector(0x154 + offset, first with { Y = bound60 });
        }
        int minimumX = 0;
        int maximumX = 0;
        int minimumZ = 0;
        int maximumZ = 0;
        for (int index = 0; index < count; index++)
        {
            int x = unchecked((int)ReadContextDword(0x54 + index * 16));
            int z = unchecked((int)ReadContextDword(0x5C + index * 16));
            if (index == 0)
            {
                minimumX = maximumX = x;
                minimumZ = maximumZ = z;
            }
            else
            {
                minimumX = Math.Min(minimumX, x);
                maximumX = Math.Max(maximumX, x);
                minimumZ = Math.Min(minimumZ, z);
                maximumZ = Math.Max(maximumZ, z);
            }
        }
        WriteContextDword(0x194, unchecked((uint)(unchecked(minimumX + maximumX) / 2)));
        WriteContextDword(0x198, 0);
        WriteContextDword(0x19C, unchecked((uint)(unchecked(minimumZ + maximumZ) / 2)));
        if (mode is 1 or 3)
        {
            int source = 0x194;
            if (mode == 3)
            {
                source = 0x1A4;
                SetVector(source, new(Signed(task, 0x288), Signed(task, 0x28A), Signed(task, 0x28C)));
            }
            for (int index = 0; index < count; index++)
            {
                foreach (int destination in new[] { 0x54, 0x94, 0xD4, 0x114, 0x154 })
                {
                    CopyVector(source, destination + 16 * index);
                }
            }
        }
        NativeBattleVector actorPosition = actorTransform.Position28;
        NativeBattleModelTransform firstTarget = readModel(unchecked((int)ReadContextDword(4))).Transform ?? throw new NotSupportedException("Magma Breath requires its first target's position WORDs.");
        short dx = unchecked((short)(actorPosition.X - firstTarget.Position28.X));
        short dy = unchecked((short)(actorPosition.Y - firstTarget.Position28.Y));
        short dz = unchecked((short)(actorPosition.Z - firstTarget.Position28.Z));
        int square = unchecked(dx * dx + dy * dy + dz * dz);
        //56BEC0 loads the signed DWORD, FSQRTs and returns the low DWORD of _ftol's signed64-bit truncation.
        uint distance = unchecked((uint)(long)Math.Sqrt(square));
        WriteContextDword(0, distance);
        return Write(frame, address, task);

        NativeBattleVector Bone(int index, byte selector, bool target = false)
        {
            _ = target;
            frame = Write(frame, address, task);
            NativeBattleMagmaBreath48PointStep point = inputs.ReadBone502170(frame, index, selector, 0);
            frame = point.Frame;
            task = Read(frame, address);
            byte[] bytes = point.Point8.Read(0, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
        uint ReadContextDword(int offset) => task.ReadDword(Context + offset);
        ushort ReadWord(int offset) => task.ReadWord(Context + offset);
        void WriteContextDword(int offset, uint value) => task = task.WriteDword(Context + offset, value);
        void WriteWord(int offset, ushort value) => task = task.WriteWord(Context + offset, value);
        void SetVector(int offset, NativeBattleVector value)
        {
            WriteContextDword(offset, unchecked((uint)(value.X << 16)));
            WriteContextDword(offset + 4, unchecked((uint)(value.Y << 16)));
            WriteContextDword(offset + 8, unchecked((uint)(value.Z << 16)));
        }
        void CopyVector(int source, int destination)
        {
            uint[] words = [ReadContextDword(source), ReadContextDword(source + 4), ReadContextDword(source + 8), ReadContextDword(source + 12)];
            for (int index = 0; index < words.Length; index++)
            {
                WriteContextDword(destination + 4 * index, words[index]);
            }
        }
    }
}
