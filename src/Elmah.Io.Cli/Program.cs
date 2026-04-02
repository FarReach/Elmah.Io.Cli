using Spectre.Console;
using System.CommandLine;

namespace Elmah.Io.Cli
{
    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var rootCommand = new RootCommand("CLI for executing various actions against elmah.io");
            rootCommand.Add(new Option<bool>("--nologo") { Description = "Doesn't display the startup banner or the copyright message" });

            rootCommand.Add(ClearCommand.Create());
            rootCommand.Add(DataloaderCommand.Create());
            rootCommand.Add(DeploymentCommand.Create());
            rootCommand.Add(LoginCommand.Create());
            rootCommand.Add(LogoutCommand.Create());
            rootCommand.Add(DeploymentsCommand.Create());
            rootCommand.Add(DiagnoseCommand.Create());
            rootCommand.Add(ExportCommand.Create());
            rootCommand.Add(ImportCommand.Create());
            rootCommand.Add(LogCommand.Create());
            rootCommand.Add(LogsCommand.Create());
            rootCommand.Add(MessagesCommand.Create());
            rootCommand.Add(OrganizationsCommand.Create());
            rootCommand.Add(ProfileCommand.Create());
            rootCommand.Add(SourceMapCommand.Create());
            rootCommand.Add(TailCommand.Create());

            if (args == null || args.ToList().TrueForAll(arg => arg != "--nologo" && arg != "--json"))
            {
                AnsiConsole.Write(new FigletText("elmah.io")
                        .Color(new Color(13, 165, 142)));
                AnsiConsole.MarkupLine("[yellow]Copyright :copyright:[/] [rgb(13,165,142)]elmah.io[/]. All rights reserved.");
            }

            args = args?.Where(arg => arg != "--nologo").ToArray() ?? [];
            AnsiConsole.WriteLine();

            return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration(), CancellationToken.None);
        }
    }
}
