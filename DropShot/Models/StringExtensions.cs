namespace DropShot.Models
{
    public static class StringExtensions
    {
        public static string GetInitials(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return
            string.Concat(value
          .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
          .Where(x => x.Length >= 1 && char.IsLetter(x[0]))
          .Select(x => char.ToUpper(x[0])));
        }

        /// <summary>
        /// Returns "FirstName S." where S is the first initial of the surname,
        /// or just the first name if no surname is present.
        /// </summary>
        public static string GetShortName(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0];

            return $"{parts[0]} {char.ToUpper(parts[^1][0])}.";
        }
    }
}
