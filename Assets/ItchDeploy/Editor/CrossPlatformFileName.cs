using System.Linq;

namespace ItchDeploy.Editor
{
    internal static class CrossPlatformFileName
    {
        private const string ReservedCharacters = @"<>:""/\|?*";

        public static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var sanitized = new string(value
                    .Select(character =>
                        ReservedCharacters.IndexOf(character) >= 0 || character < 0x20
                            ? '_'
                            : character)
                    .ToArray())
                .Trim()
                .TrimEnd('.');

            return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
        }
    }
}
