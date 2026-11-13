namespace ItchDeploy.Editor
{
    /// <summary>
    /// Where this copy of the tool came from. Every user who imports the package
    /// carries these values, so they decide which repository their editor asks for
    /// updates and which page the buttons open. Change <see cref="Slug"/> in one
    /// place when the tool moves; nothing else needs touching.
    /// </summary>
    internal static class ItchDeployProject
    {
        /// <summary>owner/repo on github.com.</summary>
        public const string Slug = "AndriiSviatenko/ItchDeploy";

        public const string Repository = "https://github.com/" + Slug;
        public const string Releases = Repository + "/releases";
        public const string LatestRelease = "https://api.github.com/repos/" + Slug + "/releases/latest";
        public const string Issues = Repository + "/issues";
    }
}
