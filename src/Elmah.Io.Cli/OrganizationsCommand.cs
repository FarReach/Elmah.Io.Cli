using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using System.CommandLine;
using System.Net.Http.Headers;

namespace Elmah.Io.Cli
{
    class OrganizationsCommand : CommandBase
    {
        private const string ApiBase = "https://api.elmah.io/v3/";

        internal static Command Create()
        {
            var organizationsCommand = new Command("organizations", "Work with organizations");

            organizationsCommand.Add(CreateListSubcommand());
            organizationsCommand.Add(CreateGetSubcommand());

            return organizationsCommand;
        }

        private static Command CreateListSubcommand()
        {
            var apiKeyOption = ApiKeyOption();
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var listSubcommand = new Command("list", "List all organizations the current user is a member of")
            {
                apiKeyOption, jsonOption, proxyHostOption, proxyPortOption
            };
            listSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var json = result.GetValue(jsonOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                var resolvedKey = ResolveApiKey(apiKey);
                if (resolvedKey == null) return;
                try
                {
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching organizations...", async ctx =>
                        {
                            using var http = CreateHttpClient(resolvedKey, host, port);
                            var response = await http.GetAsync("organizations");
                            response.EnsureSuccessStatusCode();
                            var rawJson = await response.Content.ReadAsStringAsync();
                            var orgs = JArray.Parse(rawJson);

                            if (orgs.Count == 0)
                            {
                                if (json) Console.WriteLine("[]");
                                else AnsiConsole.MarkupLine("[yellow]No organizations found[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(orgs.ToString(Formatting.Indented));
                                return;
                            }

                            var table = new Table { Expand = true };
                            table.Border(TableBorder.Rounded).BorderColor(Color.Grey);
                            table.AddColumn("ID");
                            table.AddColumn("Name");
                            table.AddColumn("Slug");

                            foreach (var org in orgs)
                            {
                                table.AddRow(
                                    Markup.Escape(org["id"]?.ToString() ?? ""),
                                    $"[#0da58e]{Markup.Escape(org["name"]?.ToString() ?? "")}[/]",
                                    Markup.Escape(org["slug"]?.ToString() ?? "")
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
            var orgIdOption = new Option<string?>("--orgId") { Description = "The ID of the organization (omit for primary organization)" };
            var includeUsageOption = new Option<bool>("--includeUsage") { Description = "Include usage statistics for the current month", DefaultValueFactory = _ => false };
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var getSubcommand = new Command("get", "Get details for an organization")
            {
                apiKeyOption, orgIdOption, includeUsageOption, jsonOption, proxyHostOption, proxyPortOption
            };
            getSubcommand.SetAction(async (ParseResult result) =>
            {
                var apiKey = result.GetValue(apiKeyOption);
                var orgId = result.GetValue(orgIdOption);
                var includeUsage = result.GetValue(includeUsageOption);
                var json = result.GetValue(jsonOption);
                var host = result.GetValue(proxyHostOption);
                var port = result.GetValue(proxyPortOption);

                var resolvedKey = ResolveApiKey(apiKey);
                if (resolvedKey == null) return;
                try
                {
                    await AnsiConsole
                        .Status()
                        .Spinner(new BugShotSpinner())
                        .StartAsync("Fetching organization...", async ctx =>
                        {
                            using var http = CreateHttpClient(resolvedKey, host, port);
                            var path = string.IsNullOrWhiteSpace(orgId)
                                ? $"organizations?includeUsage={includeUsage}"
                                : $"organizations/{Uri.EscapeDataString(orgId)}?includeUsage={includeUsage}";

                            var response = await http.GetAsync(path);
                            response.EnsureSuccessStatusCode();
                            var rawJson = await response.Content.ReadAsStringAsync();

                            JObject? org = null;
                            if (rawJson.TrimStart().StartsWith('['))
                            {
                                var arr = JArray.Parse(rawJson);
                                org = arr.FirstOrDefault() as JObject;
                            }
                            else
                            {
                                org = JObject.Parse(rawJson);
                            }

                            if (org == null)
                            {
                                if (json) Console.WriteLine("null");
                                else AnsiConsole.MarkupLine("[yellow]Organization not found[/]");
                                return;
                            }

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(org.ToString(Formatting.Indented));
                                return;
                            }

                            RenderOrganization(org, includeUsage);
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return getSubcommand;
        }

        private static void RenderOrganization(JObject org, bool includeUsage)
        {
            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap());
            grid.AddColumn();

            foreach (var prop in org.Properties())
            {
                if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array)
                    continue;
                grid.AddRow($"[bold]{Markup.Escape(prop.Name)}[/]", Markup.Escape(prop.Value.ToString()));
            }

            AnsiConsole.Write(grid);

            if (includeUsage && org["usage"] is JObject usage)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Usage (current month):[/]");
                var usageGrid = new Grid();
                usageGrid.AddColumn(new GridColumn().NoWrap());
                usageGrid.AddColumn();
                foreach (var prop in usage.Properties())
                {
                    if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array)
                        continue;
                    usageGrid.AddRow($"[bold]{Markup.Escape(prop.Name)}[/]", Markup.Escape(prop.Value.ToString()));
                }
                AnsiConsole.Write(usageGrid);
            }
        }

        private static HttpClient CreateHttpClient(string apiKey, string? proxyHost, int? proxyPort)
        {
            HttpMessageHandler handler = new HttpClientHandler();
            if (!string.IsNullOrWhiteSpace(proxyHost) && proxyPort.HasValue)
            {
                handler = new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy(proxyHost, proxyPort.Value),
                    UseProxy = true,
                };
            }

            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri(ApiBase),
            };
            http.DefaultRequestHeaders.Add("api_key", apiKey);
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue(new ProductHeaderValue("Elmah.Io.Cli", _assemblyVersion ?? "1.0")));
            return http;
        }
    }
}
