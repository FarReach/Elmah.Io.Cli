using Spectre.Console;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    class DeploymentCommand : CommandBase
    {
        internal static Command Create()
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
            var deploymentCommand = new Command("deployment", "Create a new deployment")
            {
                apiKeyOption, versionOption, createdOption, descriptionOption, userNameOption, userEmailOption, logIdOption, proxyHostOption, proxyPortOption
            };
            deploymentCommand.SetAction(async (ParseResult result) =>
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
            });

            return deploymentCommand;
        }
    }
}
