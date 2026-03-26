namespace DropShot.Models;

public class SetScore
{
    public int SetNumber { get; set; }
    public int UserGames { get; set; }
    public int OpponentGames { get; set; }

    public SetScore Clone()
        => new SetScore
        {
            UserGames = this.UserGames,
            OpponentGames = this.OpponentGames,
            SetNumber = this.SetNumber,
        };
}
