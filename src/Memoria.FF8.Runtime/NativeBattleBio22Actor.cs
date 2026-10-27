using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22ActorFrame(NativeBattleModulePoolTask Task, NativeBattleModulePoolTask Root, NativeBattleEffectMemory Memory, ImmutableArray<NativeBattleGraphicsModel> Models);
public sealed record NativeBattleBio22ActorStep(NativeBattleBio22ActorFrame Frame, uint Result);
public sealed record NativeBattleBio22ActorServices(Func<NativeBattleBio22ActorFrame, int, NativeBattleBio22ActorFrame> ReadLocal508C90, Func<NativeBattleBio22ActorFrame, int, NativeBattleActionMatrix, int, int, NativeBattleBio22ActorFrame> DrawShadow5088A0, Func<NativeBattleBio22ActorFrame, int, NativeBattleActionMatrix, short, short, NativeBattleBio22ActorFrame> DrawBodyAndExtra89D780);

public static class NativeBattleBio22Actor
{
    public static NativeBattleBio22ActorStep Advance89D3D0(NativeBattleBio22ActorFrame frame, Func<NativeBattleBio22ActorFrame, NativeBattleBio22ActorFrame> drawActualActor)
    {
        if (frame.Task.Callback != 0x89D3D0 || frame.Task.Payload.IsDefault || frame.Task.PayloadLength != 0x38 - 12)
        {
            throw new InvalidDataException("Native Bio final actor requires its actual hierarchy auxiliary task.");
        }
        NativeBattleBio22ActorMemory owner = RequireOwner(frame.Memory);
        NativeBattleModulePoolTask task = frame.Task;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                RequireModels(frame.Models);
                int target = Target(task.ReadByte(0x2D));
                owner = Initialize89DC30(owner, target);
                NativeBattleGraphicsModel model = frame.Models[target];
                frame = frame with { Models = frame.Models.SetItem(target, model with { Flags0 = (ushort)(model.Flags0 | 4) }) };
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8)).WriteByte(0x29, 1);
                break;
            case 1:
                owner = Ramp(owner);
                owner = owner.WriteWord(0xCE, W(owner.Word(0xCE) + 0x200));
                if (owner.SignedWord(0xCE) >= 0x1000)
                {
                    owner = owner.WriteWord(0xCE, 0x1000);
                    task = task.WriteByte(0x29, 2);
                }
                break;
            case 2:
                owner = Ramp(owner);
                if (S(task, 0x24) >= 26)
                {
                    task = task.WriteByte(0x29, 3);
                }
                break;
            case 3:
                ushort ramp = W(owner.Word(0xF2) - 0x40);
                owner = owner.WriteWord(0xF2, unchecked((short)ramp) < 0 ? (ushort)0 : ramp).WriteWord(0xCE, W(owner.Word(0xCE) - 0x100));
                if (owner.SignedWord(0xCE) <= 0)
                {
                    RequireModels(frame.Models);
                    target = Target(task.ReadByte(0x2D));
                    model = frame.Models[target];
                    frame = frame with { Models = frame.Models.SetItem(target, model with { Flags0 = (ushort)(model.Flags0 & 0xFFFB) }), Root = frame.Root.WriteByte(0x63, 0) };
                    task = task.WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~8) | 1)).WriteByte(0x29, 4);
                }
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Native Bio final actor phase leaves its actual auxiliary table.");
        }
        owner = owner.WriteWord(0xCC, W((owner.Word(0xCC) + 0xA0) & 0xFFF));
        frame = frame with { Task = task, Memory = frame.Memory with { Bio22ActorMemory = owner } };
        if ((frame.Task.ReadByte(0x26) & 8) != 0)
        {
            frame = drawActualActor(frame);
        }
        byte flags = frame.Task.ReadByte(0x26);
        task = frame.Task.WriteWord(0x24, W(frame.Task.ReadWord(0x24) + 1));
        return new(frame with { Task = task }, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    public static NativeBattleBio22ActorMemory Initialize89DC30(NativeBattleBio22ActorMemory owner, int target)
    {
        Target(target);
        owner.Validate();
        owner = owner.WriteDword(0, 0x2705160).WriteDword(4, 0x2714DE8).WriteDword(8, checked(0x1D972C0u + (uint)target * 156)).WriteDword(0xC, 0x1D8E054).WriteDword(0x10, 0x270456C).WriteWord(0xDA, (ushort)target);
        foreach (int offset in new[] { 0xB4, 0xB6, 0xB8, 0xCE, 0xF2, 0xA4, 0xA6, 0xA8, 0xAC, 0xAE, 0xB0, 0xBC, 0xBE, 0xC0 })
        {
            owner = owner.WriteWord(offset, 0);
        }
        owner = owner.WriteSettings(0xC4, new byte[] { 0x80, 0x80, 0x80 }).WriteDword(0xC8, 0x2000).WriteWord(0xD2, 1);
        Span<byte> dword = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(dword, 0x270E560);
        owner = owner.WriteLighting(4, dword).WriteLighting(0x1C, new byte[] { 0x80, 0x80, 0x80 });
        owner = owner.WriteLighting(0x14, new byte[] { 0, 0, 0, 0, 0x40, 1, 0, 0 }).WriteLighting(0x20, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }).WriteLighting(0x24, new byte[2]);
        return owner;
    }

    public static NativeBattleBio22ActorFrame Draw89D460(NativeBattleBio22ActorFrame frame, NativeBattleActionMatrix camera, NativeBattleBio22ActorServices services)
    {
        RequireModels(frame.Models);
        NativeBattleBio22ActorMemory owner = RequireOwner(frame.Memory);
        Target(owner.Word(0xDA));
        uint pointer = owner.Dword(8);
        uint offset = unchecked(pointer - 0x1D972C0u);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new NotSupportedException("Native Bio captured model pointer leaves its seven physical graphics rows.");
        }
        int target = (int)(offset / 156);
        NativeBattleActionMatrix original = frame.Models[target].GeometryWorldMatrix ?? throw new NotSupportedException("Native Bio actor requires its previously published model world matrix.");
        NativeBattleVector localOffset = new(owner.SignedWord(0xB4), owner.SignedWord(0xB6), owner.SignedWord(0xB8));
        NativeBattleActionMatrix moved = NativeBattlePoseMatrices.CureMovedModelWorld8D7200(original, localOffset, owner.SignedWord(0xD2));
        owner = WriteMatrix(owner, 0x14, original, true);
        owner = WriteMatrix(owner, 0x54, moved, true);
        frame = frame with { Memory = frame.Memory with { Bio22ActorMemory = owner } };
        frame = services.ReadLocal508C90(frame, target);
        owner = RequireOwner(frame.Memory);
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.Compose56C2F0(camera, ReadMatrix(owner, 0x14));
        owner = WriteMatrix(owner, 0x34, main, false);
        frame = frame with { Memory = frame.Memory with { Bio22ActorMemory = owner } };
        if ((frame.Models[target].Flags0 & 0x20) == 0)
        {
            frame = services.DrawShadow5088A0(frame, target, main, 4112, 16);
        }
        owner = RequireOwner(frame.Memory);
        original = ReadMatrix(owner, 0x14);
        moved = ReadMatrix(owner, 0x54);
        NativeBattleVector center = new(owner.SignedWord(0xDC), owner.SignedWord(0xDE), owner.SignedWord(0xE0));
        NativeBattleActionMatrix overlay = NativeBattlePoseMatrices.CureOverlayMatrix8D72C0(original, moved, center, unchecked((int)owner.Dword(0xC8)));
        owner = WriteMatrix(owner, 0x74, overlay, false);
        frame = frame with { Memory = frame.Memory with { Bio22ActorMemory = owner } };
        frame = services.DrawBodyAndExtra89D780(frame, target, main, owner.SignedWord(0xCC), owner.SignedWord(0xCE));
        return services.ReadLocal508C90(frame, target);
    }

    private static NativeBattleBio22ActorMemory WriteMatrix(NativeBattleBio22ActorMemory owner, int offset, NativeBattleActionMatrix matrix, bool copiedPaddingUnknown)
    {
        matrix.Validate();
        byte[] rotation = new byte[18];
        for (int word = 0; word < 9; word++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(word * 2), matrix.Rotation[word]);
        }
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), matrix.Z);
        owner = owner.WriteSettings(offset, rotation).WriteSettings(offset + 20, translation);
        //The typed published matrix has no raw padding WORD; copying it cannot establish those native bytes.
        return copiedPaddingUnknown ? owner with { Settings = owner.Settings.MarkUnknown(offset + 18, 2) } : owner;
    }

    public static NativeBattleActionMatrix ReadMatrix(NativeBattleBio22ActorMemory owner, int offset)
    {
        byte[] rotation = owner.ReadSettings(offset, 18);
        byte[] translation = owner.ReadSettings(offset + 20, 12);
        ImmutableArray<short>.Builder words = ImmutableArray.CreateBuilder<short>(9);
        for (int word = 0; word < 9; word++)
        {
            words.Add(BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(word * 2)));
        }
        return new(words.MoveToImmutable(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    private static NativeBattleBio22ActorMemory Ramp(NativeBattleBio22ActorMemory owner)
    {
        ushort value = W(owner.Word(0xF2) + 0x40);
        return owner.WriteWord(0xF2, unchecked((short)value) > 0x300 ? (ushort)0x300 : value);
    }
    private static int Target(int target) => target is >= 0 and < 7 ? target : throw new NotSupportedException("Native Bio actor auxiliary requires an actual physical target row from zero through six.");
    private static void RequireModels(ImmutableArray<NativeBattleGraphicsModel> models)
    {
        if (models.IsDefault || models.Length != 7 || models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native Bio actor requires its actual seven physical graphics rows.");
        }
    }
    private static NativeBattleBio22ActorMemory RequireOwner(NativeBattleEffectMemory memory) => memory.Bio22ActorMemory ?? throw new NotSupportedException("Native Bio actor requires its retained process settings, lighting and vertex work.");
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
