namespace DropShot.Shared.Helpers;

public static class PhoneMasker
{
    public static string Mask(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 5)
        {
            return new string('*', digits.Length);
        }

        var prefix = digits[..2];
        var suffix = digits[^3..];
        var stars = new string('*', digits.Length - 5);
        return $"{prefix}{stars}{suffix}";
    }
}
