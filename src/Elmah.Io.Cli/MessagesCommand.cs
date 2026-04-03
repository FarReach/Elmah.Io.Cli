using Newtonsoft.Json;
using Spectre.Console;
using System.CommandLine;
using System.Globalization;

namespace Elmah.Io.Cli
{
    class MessagesCommand : CommandBase
    {
        internal static Command Create()
        {
            var messagesCommand = new Command("messages", "Work with log messages");

            messagesCommand.Add(CreateCountSubcommand());
            messagesCommand.Add(CreateGetSubcommand());
            messagesCommand.Add(CreateListRecentSubcommand());
            messagesCommand.Add(CreateListFrequentSubcommand());
            messagesCommand.Add(LogCommand.CreateSubcommand());

            return messagesCommand;
        }

        private static Command CreateCountSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid>("--logId") { Description = "The ID of the log", Required = true };
            var queryOption = new Option<string?>("--query") { Description = "Full-text or Lucene query to filter messages" };
            var severityOption = new Option<string?>("--severity") { Description = "Filter by severity (Verbose, Debug, Information, Warning, Error, Fatal)" };
            var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Count messages from this date (defaults to 90 days ago)" };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var countSubcommand = new Command("count", "Count log messages matching optional filters")
            {
                apiKeyOption, logIdOption, queryOption, severityOption, fromOption, jsonOption, proxyHostOption, proxyPortOption
            };
            countSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var query = result.GetValue(queryOption);
                var severity = result.GetValue(severityOption);
                var from = result.GetValue(fromOption);
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
                        .StartAsync("Counting messages...", async ctx =>
                        {
                            var effectiveFrom = from ?? DateTimeOffset.UtcNow.AddDays(-90);
                            var effectiveQuery = BuildQuery(query, severity);
                            var countResult = await api.Messages.GetAllAsync(logId.ToString(), 0, 0, effectiveQuery, effectiveFrom, null, false);
                            var total = countResult?.Total ?? 0;

                            ctx.Refresh();

                            if (json)
                                Console.WriteLine(JsonConvert.SerializeObject(new { total }, Formatting.Indented));
                            else
                                AnsiConsole.MarkupLine($"[#0da58e]Total messages:[/] [bold]{total}[/]");
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return countSubcommand;
        }

        private static Command CreateGetSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid>("--logId") { Description = "The ID of the log", Required = true };
            var messageIdOption = new Option<string>("--messageId") { Description = "The ID of the message to fetch", Required = true };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var getSubcommand = new Command("get", "Fetch a single log message by ID")
            {
                apiKeyOption, logIdOption, messageIdOption, jsonOption, proxyHostOption, proxyPortOption
            };
            getSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var messageId = result.GetValue(messageIdOption);
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
                        .StartAsync("Fetching message...", async ctx =>
                        {
                            var msg = await api.Messages.GetAsync(logId.ToString(), messageId);
                            if (msg == null)
                            {
                                if (json) Console.WriteLine("null");
                                else AnsiConsole.MarkupLine("[yellow]Message not found[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(msg, Formatting.Indented));
                                return;
                            }

                            var grid = new Grid();
                            grid.AddColumn(new GridColumn().NoWrap());
                            grid.AddColumn();

                            grid.AddRow("[bold]ID[/]", Markup.Escape(msg.Id ?? ""));
                            grid.AddRow("[bold]Title[/]", $"[#0da58e]{Markup.Escape(msg.Title ?? "")}[/]");
                            grid.AddRow("[bold]Severity[/]", GetColoredSeverity(msg.Severity));
                            grid.AddRow("[bold]DateTime[/]", $"[dim]{msg.DateTime?.ToLocalTime().ToString(CultureInfo.CurrentCulture) ?? ""}[/]");
                            if (!string.IsNullOrWhiteSpace(msg.Type))
                                grid.AddRow("[bold]Type[/]", Markup.Escape(msg.Type));
                            if (!string.IsNullOrWhiteSpace(msg.Source))
                                grid.AddRow("[bold]Source[/]", Markup.Escape(msg.Source));
                            if (msg.StatusCode.HasValue)
                                grid.AddRow("[bold]Status Code[/]", msg.StatusCode.Value.ToString());
                            if (!string.IsNullOrWhiteSpace(msg.Url))
                                grid.AddRow("[bold]URL[/]", Markup.Escape(msg.Url));
                            if (!string.IsNullOrWhiteSpace(msg.Method))
                                grid.AddRow("[bold]Method[/]", Markup.Escape(msg.Method));
                            if (!string.IsNullOrWhiteSpace(msg.Hostname))
                                grid.AddRow("[bold]Hostname[/]", Markup.Escape(msg.Hostname));
                            if (!string.IsNullOrWhiteSpace(msg.User))
                                grid.AddRow("[bold]User[/]", Markup.Escape(msg.User));
                            if (!string.IsNullOrWhiteSpace(msg.Application))
                                grid.AddRow("[bold]Application[/]", Markup.Escape(msg.Application));
                            if (!string.IsNullOrWhiteSpace(msg.Version))
                                grid.AddRow("[bold]Version[/]", Markup.Escape(msg.Version));
                            if (!string.IsNullOrWhiteSpace(msg.CorrelationId))
                                grid.AddRow("[bold]Correlation ID[/]", Markup.Escape(msg.CorrelationId));
                            if (!string.IsNullOrWhiteSpace(msg.Detail))
                            {
                                grid.AddRow("[bold]Detail[/]", "");
                                AnsiConsole.Write(grid);
                                AnsiConsole.WriteLine();
                                AnsiConsole.Write(new Panel(Markup.Escape(msg.Detail)).Header("Detail").BorderColor(Color.Grey));
                                return;
                            }

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

        private static Command CreateListRecentSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid>("--logId") { Description = "The ID of the log", Required = true };
            var countOption = new Option<int>("--count") { Description = "Number of messages to return (1-100)", DefaultValueFactory = _ => 10 };
            var queryOption = new Option<string?>("--query") { Description = "Full-text or Lucene query to filter messages" };
            var severityOption = new Option<string?>("--severity") { Description = "Filter by severity (Verbose, Debug, Information, Warning, Error, Fatal)", DefaultValueFactory = _ => "Error" };
            var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Return messages from this date" };
            var toOption = new Option<DateTimeOffset?>("--to") { Description = "Return messages up to this date" };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var listRecentSubcommand = new Command("list-recent", "List the most recent log messages")
            {
                apiKeyOption, logIdOption, countOption, queryOption, severityOption, fromOption, toOption, jsonOption, proxyHostOption, proxyPortOption
            };
            listRecentSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var count = result.GetValue(countOption);
                var query = result.GetValue(queryOption);
                var severity = result.GetValue(severityOption);
                var from = result.GetValue(fromOption);
                var to = result.GetValue(toOption);
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
                        .StartAsync("Fetching messages...", async ctx =>
                        {
                            var effectiveCount = Math.Clamp(count, 1, 100);
                            var effectiveQuery = BuildQuery(query, severity);
                            var listResult = await api.Messages.GetAllAsync(logId.ToString(), 0, effectiveCount, effectiveQuery, from, to, false);

                            if (listResult == null || listResult.Messages == null || listResult.Messages.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine("[yellow]No messages found[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(new { total = listResult.Total, messages = listResult.Messages }, Formatting.Indented));
                                return;
                            }

                            var table = new Table { Expand = true };
                            table.Border(TableBorder.Rounded).BorderColor(Color.Grey);
                            table.AddColumn("DateTime");
                            table.AddColumn("Severity");
                            table.AddColumn("Title");
                            table.AddColumn("Source");

                            foreach (var msg in listResult.Messages)
                            {
                                table.AddRow(
                                    $"[dim]{msg.DateTime?.ToLocalTime().ToString(CultureInfo.CurrentCulture) ?? ""}[/]",
                                    GetColoredSeverity(msg.Severity),
                                    Markup.Escape(msg.Title ?? "(no title)"),
                                    Markup.Escape(msg.Source ?? "")
                                );
                            }

                            AnsiConsole.MarkupLine($"[dim]Showing {listResult.Messages.Count} of {listResult.Total} messages[/]");
                            AnsiConsole.Write(table);
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return listRecentSubcommand;
        }

        private static Command CreateListFrequentSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid>("--logId") { Description = "The ID of the log", Required = true };
            var countOption = new Option<int>("--count") { Description = "Number of frequent groups to return (1-25)", DefaultValueFactory = _ => 5 };
            var queryOption = new Option<string?>("--query") { Description = "Full-text or Lucene query to filter messages" };
            var severityOption = new Option<string?>("--severity") { Description = "Filter by severity (Verbose, Debug, Information, Warning, Error, Fatal)", DefaultValueFactory = _ => "Error" };
            var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Search from this date" };
            var toOption = new Option<DateTimeOffset?>("--to") { Description = "Search to this date" };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var listFrequentSubcommand = new Command("list-frequent", "List the most frequently occurring error groups")
            {
                apiKeyOption, logIdOption, countOption, queryOption, severityOption, fromOption, toOption, jsonOption, proxyHostOption, proxyPortOption
            };
            listFrequentSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var count = result.GetValue(countOption);
                var query = result.GetValue(queryOption);
                var severity = result.GetValue(severityOption);
                var from = result.GetValue(fromOption);
                var to = result.GetValue(toOption);
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
                        .StartAsync("Fetching frequent messages...", async ctx =>
                        {
                            var effectiveCount = Math.Clamp(count, 1, 25);
                            var effectiveQuery = BuildQuery(query, severity);
                            var effectiveFrom = from ?? DateTimeOffset.UtcNow.AddDays(-30);

                            var freqResult = await api.Messages.GetAllAsync(logId.ToString(), 0, 200, effectiveQuery, effectiveFrom, to, false);

                            if (freqResult == null || freqResult.Messages == null || freqResult.Messages.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine("[yellow]No messages found[/]");
                                return;
                            }

                            var groups = freqResult.Messages
                                .GroupBy(m => m.TitleTemplate ?? m.Title ?? "(no title)")
                                .Select(g => new { title = g.Key, count = g.Count(), sample = g.First() })
                                .OrderByDescending(g => g.count)
                                .Take(effectiveCount)
                                .ToList();

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(groups, Formatting.Indented));
                                return;
                            }

                            var table = new Table { Expand = true };
                            table.Border(TableBorder.Rounded).BorderColor(Color.Grey);
                            table.AddColumn("Count");
                            table.AddColumn("Severity");
                            table.AddColumn("Title");
                            table.AddColumn("Source");

                            foreach (var g in groups)
                            {
                                table.AddRow(
                                    $"[bold]{g.count}[/]",
                                    GetColoredSeverity(g.sample.Severity),
                                    Markup.Escape(g.title),
                                    Markup.Escape(g.sample.Source ?? "")
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

            return listFrequentSubcommand;
        }

        private static string BuildQuery(string? query, string? severity)
        {
            if (string.IsNullOrWhiteSpace(severity))
                return query ?? "*";
            var severityClause = $"severity:{severity}";
            return string.IsNullOrWhiteSpace(query) ? severityClause : $"({query}) AND {severityClause}";
        }

        private static string GetColoredSeverity(string? severity)
        {
            return severity switch
            {
                "Verbose" => $"[#cccccc]{Markup.Escape(severity)}[/]",
                "Debug" => $"[#95c1ba]{Markup.Escape(severity)}[/]",
                "Information" => $"[#0da58e]{Markup.Escape(severity)}[/]",
                "Warning" => $"[#ffc936]{Markup.Escape(severity ?? "")}[/]",
                "Error" => $"[#e6614f]{Markup.Escape(severity ?? "")}[/]",
                "Fatal" => $"[#993636]{Markup.Escape(severity ?? "")}[/]",
                _ => Markup.Escape(severity ?? ""),
            };
        }
    }
}
