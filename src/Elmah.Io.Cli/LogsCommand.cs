using Newtonsoft.Json;
using Spectre.Console;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    class LogsCommand : CommandBase
    {
        internal static Command Create()
        {
            var logsCommand = new Command("logs", "Work with logs");

            logsCommand.Add(CreateListSubcommand());
            logsCommand.Add(CreateGetSubcommand());
            logsCommand.Add(ClearCommand.CreateSubcommand());
            logsCommand.Add(DataloaderCommand.CreateSubcommand());
            logsCommand.Add(ExportCommand.CreateSubcommand());
            logsCommand.Add(ImportCommand.CreateSubcommand());
            logsCommand.Add(SourceMapCommand.CreateSubcommand());
            logsCommand.Add(TailCommand.CreateSubcommand());

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
            listSubcommand.SetAction(async (ParseResult result) =>
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
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching logs...", async ctx =>
                        {
                            var logs = await api.Logs.GetAllAsync();
                            if (logs == null || logs.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine("[yellow]No logs found[/]");
                                return;
                            }

                            var filtered = string.IsNullOrWhiteSpace(environment)
                                ? logs
                                : logs.Where(l => string.Equals(l.EnvironmentName, environment, StringComparison.OrdinalIgnoreCase)).ToList();

                            if (filtered.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine($"[yellow]No logs found for environment '[/][#0da58e]{Markup.Escape(environment!)}[/][yellow]'[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(filtered, Formatting.Indented));
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
                                    $"[#0da58e]{Markup.Escape(log.Name ?? "")}[/]",
                                    Markup.Escape(log.EnvironmentName ?? ""),
                                    log.Disabled == true ? "[red]Yes[/]" : "[green]No[/]"
                                );
                            }

                            AnsiConsole.Write(table);
                        });
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
            getSubcommand.SetAction(async (ParseResult result) =>
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
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching log...", async ctx =>
                        {
                            var log = await api.Logs.GetAsync(logId.ToString());
                            if (log == null)
                            {
                                if (json) Console.WriteLine("null");
                                else AnsiConsole.MarkupLine("[yellow]Log not found[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(log, Formatting.Indented));
                                return;
                            }

                            var grid = new Grid();
                            grid.AddColumn(new GridColumn().NoWrap());
                            grid.AddColumn();

                            grid.AddRow("[bold]ID[/]", Markup.Escape(log.Id ?? ""));
                            grid.AddRow("[bold]Name[/]", $"[#0da58e]{Markup.Escape(log.Name ?? "")}[/]");
                            grid.AddRow("[bold]Environment[/]", Markup.Escape(log.EnvironmentName ?? ""));
                            grid.AddRow("[bold]Disabled[/]", log.Disabled == true ? "[red]Yes[/]" : "[green]No[/]");
                            grid.AddRow("[bold]Color[/]", Markup.Escape(log.Color ?? ""));

                            AnsiConsole.Write(grid);
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return getSubcommand;
        }
    }
}
