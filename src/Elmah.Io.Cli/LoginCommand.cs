using Spectre.Console;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    class LoginCommand : CommandBase
    {
        internal static Command Create()
        {
            var apiKeyOption = new Option<string?>("--apiKey") { Description = "An API key with permission to execute commands" };
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var loginCommand = new Command("login", "Authenticate with elmah.io and store your API key locally")
            {
                apiKeyOption, proxyHostOption, proxyPortOption
            };
            loginCommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                var key = apiKey;

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter your [#0da58e]elmah.io[/] API key:")
                            .Secret());
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    AnsiConsole.MarkupLine("[red]No API key provided.[/]");
                    return;
                }

                try
                {
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Validating API key...", async ctx =>
                        {
                            var api = Api(key, host, port);
                            // Validate by fetching logs — a lightweight authenticated call
                            await api.Logs.GetAllAsync();

                            CredentialStore.SaveApiKey(key);

                            ctx.Refresh();
                            AnsiConsole.MarkupLine($"[#0da58e]Successfully logged in.[/] API key stored at [dim]{CredentialStore.CredentialsPath}[/]");
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Login failed: {e.Message}[/]");
                }
            });

            return loginCommand;
        }
    }
}
