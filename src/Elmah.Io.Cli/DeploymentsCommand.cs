using Newtonsoft.Json;
using Spectre.Console;
using System.CommandLine;
using System.Globalization;

namespace Elmah.Io.Cli
{
    class DeploymentsCommand : CommandBase
    {
        internal static Command Create()
        {
            var deploymentsCommand = new Command("deployments", "Work with deployments");

            deploymentsCommand.Add(CreateListSubcommand());
            deploymentsCommand.Add(CreateCreateSubcommand());

            return deploymentsCommand;
        }

        internal static async Task ExecuteCreateAsync(
            string? apiKey,
            string? version,
            DateTimeOffset? created,
            string? description,
            string? userName,
            string? userEmail,
            Guid? logId,
            string? host,
            int? port)
        {
            var resolvedKey = ResolveApiKey(apiKey);
            if (resolvedKey == null) return;
            var api = Api(resolvedKey, host, port);
            try
            {
                await api.Deployments.CreateAsync(new Client.CreateDeployment
                {
                    Version = version,
                    Created = created,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    UserName = string.IsNullOrWhiteSpace(userName) ? null : userName,
                    UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
                    LogId = logId.HasValue ? logId.Value.ToString() : null,
                });

                AnsiConsole.MarkupLine($"[#0da58e]Deployment successfully created[/]");
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
            }
        }

        private static Command CreateCreateSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var versionOption = new Option<string>("--version") { Description = "The version number of this deployment", Required = true };
            var createdOption = new Option<DateTimeOffset?>("--created") { Description = "When was this deployment created in UTC" };
            var descriptionOption = new Option<string?>("--description") { Description = "Description of this deployment" };
            var userNameOption = new Option<string?>("--userName") { Description = "The name of the person responsible for creating this deployment" };
            var userEmailOption = new Option<string?>("--userEmail") { Description = "The email of the person responsible for creating this deployment" };
            var logIdOption = new Option<Guid?>("--logId") { Description = "The ID of a log if this deployment is specific to a single log" };
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var createSubcommand = new Command("create", "Create a new deployment")
            {
                apiKeyOption, versionOption, createdOption, descriptionOption, userNameOption, userEmailOption, logIdOption, proxyHostOption, proxyPortOption
            };
            createSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var version = result.GetValue(versionOption);
                var created = result.GetValue(createdOption);
                var description = result.GetValue(descriptionOption);
                var userName = result.GetValue(userNameOption);
                var userEmail = result.GetValue(userEmailOption);
                var logId = result.GetValue(logIdOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                await ExecuteCreateAsync(apiKey, version, created, description, userName, userEmail, logId, host, port);
            });

            return createSubcommand;
        }

        private static Command CreateListSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var logIdOption = new Option<Guid?>("--logId") { Description = "Filter deployments to a specific log ID" };
            var countOption = new Option<int>("--count") { Description = "Number of deployments to return (max 25)", DefaultValueFactory = _ => 5 };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var listSubcommand = new Command("list", "List recent deployments")
            {
                apiKeyOption, logIdOption, countOption, jsonOption, proxyHostOption, proxyPortOption
            };
            listSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var logId = result.GetValue(logIdOption);
                var count = result.GetValue(countOption);
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
                        .StartAsync("Fetching deployments...", async ctx =>
                        {
                            var deployments = await api.Deployments.GetAllAsync();
                            if (deployments == null || deployments.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine("[yellow]No deployments found[/]");
                                return;
                            }

                            var filtered = deployments.AsEnumerable();
                            if (logId.HasValue)
                                filtered = filtered.Where(d => d.LogId == logId.Value.ToString());

                            var results = filtered.Take(Math.Clamp(count, 1, 25)).ToList();

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));
                                return;
                            }

                            var table = new Table { Expand = true };
                            table.Border(TableBorder.Rounded).BorderColor(Color.Grey);
                            table.AddColumn("ID");
                            table.AddColumn("Version");
                            table.AddColumn("Created");
                            table.AddColumn("Created By");
                            table.AddColumn("Description");

                            foreach (var d in results)
                            {
                                table.AddRow(
                                    Markup.Escape(d.Id ?? ""),
                                    $"[#0da58e]{Markup.Escape(d.Version ?? "")}[/]",
                                    $"[dim]{d.Created?.ToLocalTime().ToString(CultureInfo.CurrentCulture) ?? ""}[/]",
                                    Markup.Escape(d.UserName ?? d.CreatedBy ?? ""),
                                    Markup.Escape(d.Description ?? "")
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
    }
}
