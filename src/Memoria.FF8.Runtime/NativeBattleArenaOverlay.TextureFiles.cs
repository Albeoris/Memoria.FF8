using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleArenaOverlay
{
    private readonly record struct TextureFileChange(int Offset, int Length, ImmutableArray<NativeBattleTextureFileSource> Files);
    private readonly List<TextureFileChange> textureFileChanges = [];
    private int checkpointTextureFileChanges;
    private ImmutableArray<NativeBattleTextureFileSource>? preparedTextureFiles;

    private ImmutableArray<NativeBattleTextureFileSource>? AllTextureFiles
    {
        get
        {
            _ = Source;
            ImmutableArray<NativeBattleTextureFileSource>? files = privateParent is null ? Kernel.TextureFiles : privateParent.AllTextureFiles;
            foreach (TextureFileChange change in textureFileChanges)
            {
                files = NativeBattleTextureFileMetadata.Replace(files, change.Offset, change.Length, change.Files);
            }
            return files;
        }
    }

    internal ImmutableArray<NativeBattleTextureFileSource>? TextureFiles => NativeBattleTextureFileMetadata.Project(AllTextureFiles, Source.WorkingOffset, Source.Bound);

    internal void SetTextureFiles(int offset, int length, ImmutableArray<NativeBattleTextureFileSource> files)
    {
        Source.RequireRange(offset, length);
        NativeBattleTextureFileMetadata.Validate(files, length);
        int absolute = checked(Source.WorkingOffset + offset);
        ImmutableArray<NativeBattleTextureFileSource>.Builder translated = ImmutableArray.CreateBuilder<NativeBattleTextureFileSource>(files.Length);
        foreach (NativeBattleTextureFileSource file in files)
        {
            translated.Add(file with { Offset = checked(absolute + file.Offset) });
        }
        textureFileChanges.Add(new(absolute, length, translated.MoveToImmutable()));
    }
}
