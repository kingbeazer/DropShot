namespace DropShot.Models
{

    public enum CompetitionFormat
    {
        Singles = 1,
        Doubles = 2,
        Team = 3
    }

    public class Competition
    {
        public int CompetitionID { get; set; }

        public string CompetitionName { get; set; }

        public CompetitionFormat CompetitionFormat { get; set; }
    }
}
