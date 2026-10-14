using DohnaDohna.Scripts;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace DohnaDohna.Content;

[RegisterOwnedCardKeyword(nameof(Advance), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public class SquadKeywords
{
    public static readonly CardKeyword Advance =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Advance)).GetModCardKeyword();
}
