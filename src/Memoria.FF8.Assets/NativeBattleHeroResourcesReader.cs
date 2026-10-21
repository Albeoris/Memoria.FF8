using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleHeroResourcesReader
{
    public static NativeBattleHeroResources Read(byte[] executable, ushort effect)
    {
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(effect);
        NativeExecutableMemory memory = new(executable);
        string filename = Encoding.ASCII.GetString(memory.Read(NativeBattleHeroProfile.FilenameAddress, NativeBattleHeroProfile.TextureName.Length + 1));
        if (filename != NativeBattleHeroProfile.TextureName + "\0")
        {
            throw new InvalidDataException("The Hero initializer requires its actual original TIM filename.");
        }
        NativeBattleHeroResources result = new(effect, new(profile.MainSource, NativeBattleEffectStaticResourcesReader.Read(memory, profile.MainSource, 0x888)), new(profile.OrbitSource, NativeBattleEffectStaticResourcesReader.Read(memory, profile.OrbitSource, 0x7BC)), NativeBattleEffectStaticResourcesReader.Read(memory, profile.FanSource, 0x2B8), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.SoundAddress, 4)));
        result.Validate();
        ValidateAnimation(result.Main, 0x13C, 40, 4);
        ValidateAnimation(result.Orbit, 0xBC, 18, 3);
        ValidateMesh(result.Fan, 0, 8, 28, 5, 18);
        return result;
    }

    private static void ValidateAnimation(NativeBattleGfAnimationSource source, int workspaceBytes, int duration, int nodes)
    {
        if (Read(source.Data, 0) != 2)
        {
            throw new InvalidDataException("The Hero ACT source requires its two actual geometry pointers.");
        }
        for (int index = 0; index < 2; index++)
        {
            int geometry = Read(source.Data, 8 + index * 4);
            if (Read(source.Data, geometry + 8) != 1)
            {
                throw new InvalidDataException("The Hero ACT geometry requires its original single shape.");
            }
            ValidateMesh(source.Data, geometry, 12, 25, 3, 16);
        }
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[workspaceBytes]));
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleHeroVertexSelection selection = NativeBattleHeroGeometry.Select(source, node);
                if (selection.RequiresMorph || selection.Count != 25)
                {
                    throw new InvalidDataException("The Hero ACT channel leaves its original geometry/shape envelope.");
                }
                return current;
            });
            if (advance.EmittedNodes != nodes || advance.Remaining != duration - frame - 1)
            {
                throw new InvalidDataException("The Hero ACT channel timeline leaves its original bounded duration.");
            }
            workspace = advance.Workspace;
        }
    }

    private static void ValidateMesh(ImmutableArray<byte> source, int geometry, int headerLength, int vertices, int activeGroup, int faces)
    {
        if (Read(source, geometry + 4) != vertices)
        {
            throw new InvalidDataException("The Hero geometry requires its actual vertex envelope.");
        }
        int first = checked(geometry + headerLength);
        RequireRange(source, first, checked(vertices * 8));
        int cursor = checked(geometry + Read(source, geometry));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = Read(source, cursor);
            cursor = checked(cursor + 4);
            if (count != (group == activeGroup ? faces : 0))
            {
                throw new InvalidDataException("The Hero mesh requires its original group and face counts.");
            }
            for (int index = 0; index < count; index++)
            {
                RequireRange(source, cursor, lengths[group]);
                int corners = (group & 1) == 0 ? 3 : 4;
                for (int corner = 0; corner < corners; corner++)
                {
                    ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(cursor + 4 + corner * 2));
                    int byteOffset = offset * 4;
                    if ((byteOffset & 7) != 0 || byteOffset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("The Hero face reads outside its actual vertex rows.");
                    }
                }
                cursor = checked(cursor + lengths[group]);
            }
        }
    }

    private static int Read(ImmutableArray<byte> bytes, int offset)
    {
        RequireRange(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }

    private static void RequireRange(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (bytes.IsDefault || offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The Hero typed resource read leaves its executable envelope.");
        }
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleHeroResources> nativeHeroResources = [];

    public NativeBattleHeroResources ReadNativeHeroResources(ushort effect)
    {
        if (!nativeHeroResources.TryGetValue(effect, out NativeBattleHeroResources? result))
        {
            result = NativeBattleHeroResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeHeroResources.Add(effect, result);
        }
        return result;
    }
}
