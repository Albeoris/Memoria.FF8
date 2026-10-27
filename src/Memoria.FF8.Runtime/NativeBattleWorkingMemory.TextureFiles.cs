using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleWorkingMemory
{
    private ImmutableArray<NativeBattleTextureFileSource>? textureFiles;
    private TextureFileCheckpoint? textureFileCheckpoint;

    internal ImmutableArray<NativeBattleTextureFileSource>? TextureFiles
    {
        get
        {
            RequireActive();
            return textureFiles;
        }
    }

    internal void SetTextureFiles(ImmutableArray<NativeBattleTextureFileSource>? files)
    {
        RequireActive();
        NativeBattleTextureFileMetadata.Validate(files, logicalLength);
        if (Nullable.Equals(textureFiles, files))
        {
            return;
        }
        NativeBattleTaskOperation.EnlistRetained(this);
        if (HasCheckpoint)
        {
            textureFileCheckpoint ??= new(this);
            textureFileCheckpoint.Reserve(CheckpointDepth);
            EnlistCheckpointParticipant(textureFileCheckpoint);
        }
        textureFiles = files;
        version++;
    }

    private sealed class TextureFileCheckpoint(NativeBattleWorkingMemory owner) : INativeBattleCheckpointParticipant
    {
        private readonly List<ImmutableArray<NativeBattleTextureFileSource>?> previous = [];
        internal void Reserve(int depth) => previous.EnsureCapacity(checked(previous.Count + depth));
        public void BeginCheckpoint() => previous.Add(owner.textureFiles);
        public void CommitCheckpoint() => previous.RemoveAt(previous.Count - 1);
        public void RollbackCheckpoint()
        {
            owner.textureFiles = previous[^1];
            previous.RemoveAt(previous.Count - 1);
        }
    }
}
