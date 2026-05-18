namespace DropShot.Models;

public class CompetitionEntryConsent
{
    public int CompetitionEntryConsentId { get; set; }
    public int CompetitionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime ConsentGivenUtc { get; set; }
    public string ConsentWordingShown { get; set; } = "";
    public string ConsentVersion { get; set; } = "";
    public DateTime? WithdrawnUtc { get; set; }

    public Competition Competition { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
