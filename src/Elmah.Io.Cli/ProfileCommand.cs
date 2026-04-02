using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using System.CommandLine;
using System.Net.Http.Headers;

namespace Elmah.Io.Cli
{
    class ProfileCommand : CommandBase
    {
        private const string ApiBase = "https://api.elmah.io/v3/";

        internal static Command Create()
        {
            var apiKeyOption = ApiKeyOption();
            var jsonOption = JsonOption();
            var proxyHostOption = ProxyHostOption();
            var proxyPortOption = ProxyPortOption();
            var profileCommand = new Command("profile", "Show details for the current user")
            {
                apiKeyOption, jsonOption, proxyHostOption, proxyPortOption
            };
            profileCommand.SetAction(async (ParseResult result) =>
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
                        .StartAsync("Fetching profile...", async ctx =>
                        {
                            using var http = CreateHttpClient(resolvedKey, host, port);
                            var response = await http.GetAsync("users/current");
                            response.EnsureSuccessStatusCode();
                            var rawJson = await response.Content.ReadAsStringAsync();
                            var user = JObject.Parse(rawJson);

                            ctx.Refresh();

                            if (json)
                            {
                                Console.WriteLine(user.ToString(Formatting.Indented));
                                return;
                            }

                            var grid = new Grid();
                            grid.AddColumn(new GridColumn().NoWrap());
                            grid.AddColumn();

                            foreach (var prop in user.Properties())
                            {
                                if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array)
                                    continue;
                                var label = prop.Name switch
                                {
                                    "email" => "[bold]Email[/]",
                                    "username" => "[bold]Username[/]",
                                    "name" => "[bold]Name[/]",
                                    "id" => "[bold]ID[/]",
                                    _ => $"[bold]{Markup.Escape(prop.Name)}[/]",
                                };
                                grid.AddRow(label, Markup.Escape(prop.Value.ToString()));
                            }

                            AnsiConsole.Write(grid);
                        });
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                }
            });

            return profileCommand;
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
