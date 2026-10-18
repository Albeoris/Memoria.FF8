namespace Memoria.FF8.Runtime;

/// <summary>Normalized87E230/88D550/8836F0 task/header and actor/target anchor initialization; bank ownership stays with each caller.</summary>
public sealed record NativeBattleEffectNodeDogOrigins(NativeBattleVector ActorOrigin, NativeBattleVector TargetOrigin);

public static class NativeBattleEffectNodeContext
{
    private const int Context = 0x34;

    public static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleModulePoolTask root, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, uint executableAddress, bool singleSelectedTarget = false, NativeBattleEffectNodeDogOrigins? dogOrigins = null)
    {
        WriteWord(0x22, task.ReadWord(0x29A));
        WriteWord(0x24, task.ReadWord(0x29C));
        Write(0x224, executableAddress + 4);
        Write(0x228, executableAddress + 0x44);
        Write(0x22C, executableAddress + 0x84);
        Write(0x230, executableAddress + 0xC4);
        WriteWord(0x1C, singleSelectedTarget ? (ushort)1 : root.ReadWord(0x5A));
        WriteWord(0x1E, task.ReadByte(0x2C));
        int count = unchecked((short)ReadWord(0x1C));
        int headerIndex = checked(parameters.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        int firstContact = packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native87E230 requires the selected group's actual contact pointer.");
        for (int index = 0; index < count; index++)
        {
            Write(4 + 4 * index, packets.ReadContact(checked(firstContact + (singleSelectedTarget ? unchecked((sbyte)task.ReadByte(0x2B)) : index))).Primary.ActorIndex);
        }
        WriteWord(0x1A, task.ReadWord(0x29E));
        short mode = unchecked((short)ReadWord(0x24));
        if (mode == 2 && dogOrigins is null)
        {
            //87E4C0/88D7E0/883980 and their target variants skip502170 in this branch and reuse their original uninitialized stack VECTOR.
            throw new NotSupportedException("Native shared node context mode2 requires the observed prior stack VECTOR at87E4C0/88D7E0/883980 and their target variants.");
        }
        int actor = unchecked((short)ReadWord(0x1E));
        NativeBattleGraphicsModel actorModel = readModel(actor);
        NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native87E4C0 requires actorWORD36 and its model transform.");
        NativeBattleVector actorFirst = Bone(actor, 241);
        SetVector(0x1C4, actorFirst with { Y = actorTransform.Angle36 });
        SetVector(0x1D4, actorFirst with { Y = 0 });
        SetVector(0x1E4, Bone(actor, 241));
        SetVector(0x1F4, Bone(actor, 240));
        short actorBound60 = (actorModel.GeometryBounds52 ?? throw new NotSupportedException("Native87E4C0 requires the actual geometry bounds WORD60.")).Maximum.X;
        SetVector(0x204, actorFirst with { Y = actorBound60 });
        if (mode == 4)
        {
            SetVector(0x1B4, new(Word(task, 0x290), Word(task, 0x292), Word(task, 0x294)));
            foreach (int destination in new[] { 0x204, 0x1F4, 0x1E4, 0x1D4, 0x1C4 })
            {
                CopyVector(0x1B4, destination);
            }
        }
        for (int index = 0; index < count; index++)
        {
            int target = unchecked((int)Read(4 + 4 * index));
            NativeBattleGraphicsModel model = readModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native87E810 requires targetWORD36 and its model transform.");
            NativeBattleVector first = Bone(target, 241, true);
            int offset = index * 16;
            SetVector(0x54 + offset, first with { Y = transform.Angle36 });
            SetVector(0x94 + offset, first with { Y = 0 });
            SetVector(0xD4 + offset, Bone(target, 241, true));
            SetVector(0x114 + offset, Bone(target, 240, true));
            short bound60 = (model.GeometryBounds52 ?? throw new NotSupportedException("Native87E810 requires target geometry bounds WORD60.")).Maximum.X;
            SetVector(0x154 + offset, first with { Y = bound60 });
        }
        int minimumX = 0;
        int maximumX = 0;
        int minimumZ = 0;
        int maximumZ = 0;
        for (int index = 0; index < count; index++)
        {
            int x = unchecked((int)Read(0x54 + index * 16));
            int z = unchecked((int)Read(0x5C + index * 16));
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
        Write(0x194, unchecked((uint)(unchecked(minimumX + maximumX) / 2)));
        Write(0x198, 0);
        Write(0x19C, unchecked((uint)(unchecked(minimumZ + maximumZ) / 2)));
        if (mode is 1 or 3)
        {
            int source = 0x194;
            if (mode == 3)
            {
                source = 0x1A4;
                SetVector(source, new(Word(task, 0x288), Word(task, 0x28A), Word(task, 0x28C)));
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
        NativeBattleModelTransform firstTarget = readModel(unchecked((int)Read(4))).Transform ?? throw new NotSupportedException("Native87E230 requires its first target's position WORDs.");
        short dx = unchecked((short)(actorPosition.X - firstTarget.Position28.X));
        short dy = unchecked((short)(actorPosition.Y - firstTarget.Position28.Y));
        short dz = unchecked((short)(actorPosition.Z - firstTarget.Position28.Z));
        int square = unchecked(dx * dx + dy * dy + dz * dz);
        //56BEC0 loads the signed DWORD, FSQRTs and returns the low DWORD of _ftol's signed64-bit truncation.
        uint distance = unchecked((uint)(long)Math.Sqrt(square));
        Write(0, distance);
        return task;

        NativeBattleVector Bone(int index, byte selector, bool target = false)
        {
            if (mode == 2)
            {
                return target ? dogOrigins!.TargetOrigin : dogOrigins!.ActorOrigin;
            }
            return NativeBattlePoseMatrices.ReadBone(readModel(index), readPoseSource(index), selector, 0);
        }
        uint Read(int offset) => task.ReadDword(Context + offset);
        ushort ReadWord(int offset) => task.ReadWord(Context + offset);
        void Write(int offset, uint value) => task = task.WriteDword(Context + offset, value);
        void WriteWord(int offset, ushort value) => task = task.WriteWord(Context + offset, value);
        void SetVector(int offset, NativeBattleVector value)
        {
            Write(offset, unchecked((uint)(value.X << 16)));
            Write(offset + 4, unchecked((uint)(value.Y << 16)));
            Write(offset + 8, unchecked((uint)(value.Z << 16)));
        }
        void CopyVector(int source, int destination)
        {
            uint[] words = [Read(source), Read(source + 4), Read(source + 8), Read(source + 12)];
            for (int index = 0; index < words.Length; index++)
            {
                Write(destination + 4 * index, words[index]);
            }
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
