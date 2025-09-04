using Spectre.Console;

namespace Elmah.Io.Cli
{
    public class BugShotSpinner : Spinner
    {
        public override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

        public override bool IsUnicode => true;

        public override IReadOnlyList<string> Frames =>
        [
            "🐛      🔫",
            "🐛    💥🔫",
            "🐛   •💥🔫",
            "🐛   •  🔫",
            "🐛  •   🔫",
            "🐛 •    🔫",
            "🐛•     🔫",
            "💥      🔫",
            "💥      🔫",
            "        🔫",
            "        🔫",
            "        🔫",
            "🐛      🔫",
            "🐛      🔫",
        ];
    }
}
