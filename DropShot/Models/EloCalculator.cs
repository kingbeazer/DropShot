namespace DropShot.Models
{
    public static class EloCalculator
    {
        private const double Scale = 400.0;

        public static double ExpectedScore(double ratingA, double ratingB) =>
            1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / Scale));

        public static double UpdateRating(
            double rating,
            double expected,
            double score,
            double K,
            double? marginOfVictoryMultiplier = null)
        {
            double delta = K * (score - expected);
            if (marginOfVictoryMultiplier.HasValue)
                delta *= marginOfVictoryMultiplier.Value;
            return rating + delta;
        }

        public static double MarginOfVictoryMultiplier(
            double pointsA, double pointsB, double ratingA, double ratingB)
        {
            double PD = pointsA - pointsB;
            double diff = Math.Abs(PD);
            double factor = 2.2 / ((Math.Min(ratingA, ratingB) - Math.Max(ratingA, ratingB)) * 0.001 + 2.2);
            return Math.Log(diff + 1) * factor;
        }
    }
}
