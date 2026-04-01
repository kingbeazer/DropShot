namespace DropShot.Models;

public class ClubEmailTemplate
{
    public int ClubEmailTemplateId { get; set; }
    public int ClubId { get; set; }
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    public Club Club { get; set; } = null!;
}
