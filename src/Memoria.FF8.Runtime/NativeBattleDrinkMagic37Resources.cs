using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, ImmutableArray<byte> Trajectory, ImmutableArray<byte> Sparkle, ImmutableArray<byte> OrbSprite, ImmutableArray<byte> OrbInitial, ImmutableArray<byte> OrbShape, uint Sound)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != NativeBattleDrinkMagic37Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != Animation.Data.Length || Executable is null || Executable.ResourceAddress != NativeBattleDrinkMagic37Profile.ExecutableAddress || Executable.Data.IsDefault || Executable.Data.Length != NativeBattleDrinkMagic37Profile.ExecutableLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Drink Magic requires its complete original camera and executable bank.");
        }
        ValidateSprite(Trajectory, 0x15D92F4, 0x210, 6, 6);
        ValidateSprite(Sparkle, 0x15D9504, 0x120, 6, 0);
        ValidateSprite(OrbSprite, 0x15D9624, 0x1AC, 16, 6);
        if (OrbInitial.IsDefault || OrbInitial.Length != NativeBattleDrinkMagic37Profile.OrbLength || OrbShape.IsDefault || OrbShape.Length != NativeBattleDrinkMagic37Profile.ShapeLength || BinaryPrimitives.ReadUInt32LittleEndian(OrbInitial.AsSpan().Slice(4)) != 276 || BinaryPrimitives.ReadUInt32LittleEndian(OrbShape.AsSpan().Slice(4)) != 276)
        {
            throw new InvalidDataException("Drink Magic requires its separate original orb mesh and vertex-only shape.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(OrbInitial.AsSpan()));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] counts = [0, 0, 0, 62, 0, 0, 0, 146];
        for (int group = 0; group < counts.Length; group++)
        {
            if (cursor < 0 || cursor > OrbInitial.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(OrbInitial.AsSpan().Slice(cursor)) != counts[group])
            {
                throw new InvalidDataException("Drink Magic orb geometry contradicts its original polygon groups.");
            }
            cursor = checked(cursor + 4 + counts[group] * strides[group]);
        }
        if (cursor != OrbInitial.Length)
        {
            throw new InvalidDataException("Drink Magic orb geometry leaves its original mesh extent.");
        }
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, 0) { Effect = 37, Executable = Executable };
    }

    public NativeBattleModuleResource Sprite(uint address)
    {
        ImmutableArray<byte> bytes = address switch
        {
            0x15D92F4 => Trajectory,
            0x15D9504 => Sparkle,
            0x15D9624 => OrbSprite,
            _ => throw new NotSupportedException("Drink Magic sprite leaves its original resource directory.")
        };
        return new(0, address, 0, 0, bytes);
    }

    private static void ValidateSprite(ImmutableArray<byte> bytes, uint address, int length, int count, byte kind)
    {
        if (bytes.IsDefault || bytes.Length != length || bytes[3] != kind || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Drink Magic requires its complete original sprite frames.");
        }
        NativeBattleModuleResource resource = new(0, address, 0, 0, bytes);
        for (int frame = 0; frame < count; frame++)
        {
            _ = resource.ProjectedFrame571C80(frame);
        }
    }
}
