using System.Text;

namespace DropShot.Shared.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Converts a PascalCase enum value to a spaced display name.
    /// e.g. MixedDoubles → "Mixed Doubles", MixedTeam → "Mixed Team"
    /// </summary>
    public static string ToDisplayName<T>(this T value) where T : struct, Enum
    {
        var name = value.ToString();
        if (name.Length <= 1) return name;

        var sb = new StringBuilder(name.Length + 4);
        sb.Append(name[0]);
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
                sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
