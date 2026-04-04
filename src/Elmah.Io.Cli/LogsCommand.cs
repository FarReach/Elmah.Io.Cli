using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Json;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    class LogsCommand : CommandBase
    {
        internal static Command Create()
        {
            var logsCommand = new Command("logs", "Work with logs")
            {
                ClearCommand.CreateSubcommand(),
                DataloaderCommand.CreateSubcommand(),
                ExportCommand.CreateSubcommand(),
                CreateGetSubcommand(),
                ImportCommand.CreateSubcommand(),
                CreateListSubcommand(),
                SourceMapCommand.CreateSubcommand(),
                TailCommand.CreateSubcommand()
            };

            return logsCommand;
        }

        private static Command CreateListSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var environmentOption = new Option<string?>("--environment") { Description = "Filter logs by environment name" };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var listSubcommand = new Command("list", "List all logs accessible with the API key")
            {
                apiKeyOption, environmentOption, jsonOption, proxyHostOption, proxyPortOption
            };
            listSubcommand.SetAction(async result =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var environment = result.GetValue(environmentOption);
                var json = result.GetValue(jsonOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                var resolvedKey = ResolveApiKey(apiKey);
                if (resolvedKey == null) return;
                var api = Api(resolvedKey, host, port);
                try
                {
                    ICollection<Client.Log>? logs = null;
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching logs...", async ctx =>
                        {
                            logs = await api.Logs.GetAllAsync();
                        });

                    if (logs == null || logs.Count == 0)
                    {
                        if (json) AnsiConsole.Write(new JsonText("[]"));
                        else AnsiConsole.MarkupLine("[yellow]No logs found[/]");
                        return;
                    }

                    var filtered = string.IsNullOrWhiteSpace(environment)
                        ? logs
                        : [.. logs.Where(l => string.Equals(l.EnvironmentName, environment, StringComparison.OrdinalIgnoreCase))];

                    if (filtered.Count == 0)
                    {
                        if (json) AnsiConsole.Write(new JsonText("[]"));
                        else AnsiConsole.MarkupLine($"[yellow]No logs found for environment '[/][#0da58e]{Markup.Escape(environment!)}[/][yellow]'[/]");
                        return;
                    }

                    if (json)
                    {
                        AnsiConsole.Write(new JsonText(JsonConvert.SerializeObject(filtered, Formatting.Indented)));
                        return;
                    }

                    var table = new Table { Expand = true };
                    table.Border(TableBorder.Rounded).BorderColor(Color.Grey);
                    table.AddColumn("ID");
                    table.AddColumn("Name");
                    table.AddColumn("Environment");
                    table.AddColumn("Disabled");

                    foreach (var log in filtered)
                    {
                        table.AddRow(
                            Markup.Escape(log.Id ?? ""),
                            Markup.Escape(log.Name ?? ""),
                            Markup.Escape(log.EnvironmentName ?? ""),
                            log.Disabled == true ? "[red]Yes[/]" : "[green]No[/]"
                        );
                    }

                    AnsiConsole.Write(table);
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return listSubcommand;
        }

        private static Command CreateGetSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid>("--logId") { Description = "The ID of the log to fetch details for", Required = true };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var getSubcommand = new Command("get", "Get details for a specific log")
            {
                apiKeyOption, logIdOption, jsonOption, proxyHostOption, proxyPortOption
            };
            getSubcommand.SetAction(async result =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var json = result.GetValue(jsonOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                var resolvedKey = ResolveApiKey(apiKey);
                if (resolvedKey == null) return;
                var api = Api(resolvedKey, host, port);
                try
                {
                    Client.Log? log = null;
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching log...", async ctx =>
                        {
                            log = await api.Logs.GetAsync(logId.ToString());
                        });

                    if (log == null)
                    {
                        if (json) AnsiConsole.Write(new JsonText("{}"));
                        else AnsiConsole.MarkupLine("[yellow]Log not found[/]");
                        return;
                    }

                    if (json)
                    {
                        AnsiConsole.Write(new JsonText(JsonConvert.SerializeObject(log, Formatting.Indented)));
                        return;
                    }

                    var grid = new Grid();
                    grid.AddColumn(new GridColumn().NoWrap());
                    grid.AddColumn();

                    grid.AddRow("[bold]ID[/]", Markup.Escape(log.Id ?? ""));
                    grid.AddRow("[bold]Name[/]", Markup.Escape(log.Name ?? ""));
                    grid.AddRow("[bold]Environment[/]", Markup.Escape(log.EnvironmentName ?? ""));
                    grid.AddRow("[bold]Disabled[/]", log.Disabled == true ? "[red]Yes[/]" : "[green]No[/]");
                    grid.AddRow("[bold]Color[/]", $"{GetLogColor(log.Color)}{Markup.Escape(log.Color)}[/]");

                    AnsiConsole.Write(grid);
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return getSubcommand;
        }

        private static string GetLogColor(string color)
        {
            return color switch
            {
                "lightgreen" => "[#8cc152]",
                "lime" => "[#cdda49]",
                "yellow" => "[#fdc02f]",
                "orange" => "[#fd9727]",
                "deeporange" => "[#fc5830]",
                "red" => "[#e2202c]",
                "pink" => "[#e62565]",
                "purple" => "[#9b2fae]",
                "deeppurple" => "[#673fb4]",
                "blue" => "[#4054b2]",
                "lightblue" => "[#587bf8]",
                _ => "[#0da58e]",
            };
        }
    }
}
