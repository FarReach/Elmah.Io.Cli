using Spectre.Console;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    class LogoutCommand : CommandBase
    {
        internal static Command Create()
        {
            var logoutCommand = new Command("logout", "Remove the locally stored elmah.io API key");
            logoutCommand.SetAction(_ =>
            {
                CredentialStore.Clear();
                AnsiConsole.MarkupLine("[#0da58e]Logged out.[/] API key removed.");
            });

            return logoutCommand;
        }
    }
}
