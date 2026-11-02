using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    public void ConnectNativeTutorialExam(NativeTutorialExamCatalog catalog, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        NativeTutorialExamProgress ReadProgress()
        {
            NativeCharacterVitals? party = session.State.CharacterVitals?.DeepCopy();
            int level = party is null ? 1 : NativeStatDefinitions.Level(party.Records[0].Experience, party.Definitions.Characters[0]);
            NativeSeeDState? seeD = session.SeeD;
            int rank = seeD is { SalaryUiEnabled: true } ? seeD.RankPoints / 100 : -1;
            return new NativeTutorialExamProgress(seeD?.TestLevel ?? 0, rank, level, party);
        }
        void CommitProgress(NativeTutorialExamProgress before, NativeTutorialExamProgress after)
        {
            NativeSeeDState seeD = session.SeeD ?? throw new NotSupportedException("SeeD tests require persistent rank and test progress.");
            NativeTutorialExamProgress current = ReadProgress();
            if (current.PassedTests != before.PassedTests || current.SeeDRank != before.SeeDRank || current.CharacterLevel != before.CharacterLevel)
            {
                throw new InvalidOperationException("SeeD test eligibility changed while answering the questions.");
            }
            // A passed written test sets the next whole rank; rank A leaves its points untouched.
            ushort points = before.SeeDRank == 31 ? seeD.RankPoints : (ushort)Math.Clamp(after.SeeDRank * 100, 100, 3100);
            session.SetSeeD(seeD with { TestLevel = checked((byte)after.PassedTests), RankPoints = points });
        }
        ConnectNativeTutorialExam(catalog, ReadProgress, CommitProgress);
    }
}
