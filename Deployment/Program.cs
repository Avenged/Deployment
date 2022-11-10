using CliWrap;
using Deployment;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;
using Spectre.Console;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.Json;

internal class Program
{
    private static SshClient SshClient { get; set; } = null!;

    private static readonly IEnumerable<string> projects = new List<string>()
    {
        "sgui-app-container",
        "sgui-api-container",
        "sgui-hf-container"
    };

    private static IEnumerable<string>? tasks;

    private static bool IsHangFireSelected()
    {
        return selectedProjects.Contains("sgui-hf-container");
    }

    private static bool IsApiSelected()
    {
        return selectedProjects.Contains("sgui-api-container");
    }

    private static bool IsAppSelected()
    {
        return selectedProjects.Contains("sgui-app-container");
    }

    private static List<string> selectedProjects = new();
    private static List<string> selectedTasks = new();
    private static Configuration? configuration;

    private static void Main(string[] args)
    {
        try
        {
            var configFile = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Config.json"));
            var config = JsonSerializer.Deserialize<Configuration>(configFile, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });

            if (config is null)
            {
                throw new Exception("No se pudo leer la configuración");
            }

            configuration = config;

            PasswordAuthenticationMethod method = new(config.Username, config.Password);
            ConnectionInfo connectionInfo = new(config.Host, config.Username, method);

            selectedProjects = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Selecciona los proyectos a deployar")
                    .Required()
                    .PageSize(10)
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle a project, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(projects));

            tasks = new List<string>()
            {
                "Copiar contenido de SGUI a SGUI-ASI",
                "Enviar cambios de GIT de SGUI-ASI a Origin Master",
                $"Actualizar repositorio en {configuration!.Host}",
                $"Deploy de aplicaciones ({string.Join(", ", selectedProjects!)})"
            };

            selectedTasks = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Selecciona las tareas a realizar")
                    .Required()
                    .PageSize(10)
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle a project, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(tasks));

            SshClient = new(connectionInfo);

            ExecuteNextCommands();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            Console.WriteLine("Finalizado con errores. Presione cualquier tecla para cerrar.");
            Console.ReadKey();
        }
    }

    private static void ExecuteNextCommands()
    {
        try
        {
            if (selectedTasks.Contains(tasks!.ElementAt(0)))
            {
                DirectoryInfo di = new(configuration!.SguiAsiSourcePath!);

                foreach (var dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }

                var result = Cli.Wrap("robocopy")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments($"{configuration.SguiPath} {configuration.SguiAsiSourcePath} -e -Xd .vs .git bin obj")
                    .ExecuteAsync().Task.Result;

                if (result.ExitCode >= 8)
                {
                    Console.WriteLine("Finalizado con errores. Presione cualquier tecla para cerrar.");
                    Console.ReadKey();
                    Environment.Exit(1);
                }
            }

            if (selectedTasks.Contains(tasks!.ElementAt(1)))
            {
                var result = Cli.Wrap("git")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments($"status")
                    .ExecuteAsync().Task.Result;

                result = Cli.Wrap("git")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments("add :/")
                    .ExecuteAsync().Task.Result;

                result = Cli.Wrap("git")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments($"commit -a -m \"deploy {DateTime.Now:yyyyMMdd HH:mm:ss}\"")
                    .ExecuteAsync().Task.Result;

                result = Cli.Wrap("git")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments("pull origin master")
                    .ExecuteAsync().Task.Result;


                result = Cli.Wrap("git")
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithArguments("push origin master")
                    .ExecuteAsync().Task.Result;
            }

            SshClient.Connect();

            if (selectedTasks.Contains(tasks!.ElementAt(2)))
            {
                PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; git pull origin master"));
            }

            if (selectedTasks.Contains(tasks!.ElementAt(3)))
            {
                PrintAndWaitCommand(SshClient.CreateCommand($"mysql -u {configuration!.MySqlUsername} -p{configuration.MySqlPassword} -e 'UPDATE Parametros SET ValorChar=date_format(Now(), \"%Y.%m.%d.%H%i\") WHERE CodigoParametro=\"SGUI.Version\";' SGUI_Datos"));

                foreach (var project in projects)
                {
                    if (IsAppSelected() && project == "sgui-app-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-app-container"));

                    if (IsApiSelected() && project == "sgui-api-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-api-container"));

                    if (IsHangFireSelected() && project == "sgui-hf-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman stop sgui-hf-container"));

                    if (IsAppSelected() && project == "sgui-app-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-app-container"));

                    if (IsApiSelected() && project == "sgui-api-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-api-container"));

                    if (IsHangFireSelected() && project == "sgui-hf-container")
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rm sgui-hf-container"));

                    PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman rmi --all"));

                    if (IsAppSelected() && project == "sgui-app-container")
                    {
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-app-image -f source/Dockerfile.app"));
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -dt -e ASPNETCORE_URLS=http://+:4000 --name sgui-app-container --pod sgui-pod localhost/sgui-app-image"));
                    }

                    if (IsApiSelected() && project == "sgui-api-container")
                    {
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-api-image -f source/Dockerfile.api"));
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -dt -e ASPNETCORE_URLS=http://+:5000 --name sgui-api-container --pod sgui-pod localhost/sgui-api-image"));
                    }

                    if (IsHangFireSelected() && project == "sgui-hf-container")
                    {
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman build -t sgui-hf-image -f source/Dockerfile.hf"));
                        PrintAndWaitCommand(SshClient.CreateCommand("cd sgui; sudo podman run -dt -e ASPNETCORE_URLS=http://+:6000 --name sgui-hf-container --pod sgui-pod localhost/sgui-hf-image"));
                    }
                }
            }

            Console.WriteLine("Finalizado. Presione cualquier tecla para cerrar.");
            Console.ReadKey();

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            Console.WriteLine("Finalizado con errores. Presione cualquier tecla para cerrar.");
            Console.ReadKey();
            Environment.Exit(-1);
        }
    }

    private static void PrintAndWaitCommand(SshCommand command)
    {
        var result = command.BeginExecute();

        new Thread(() =>
        {
            using var reader = new StreamReader(command.ExtendedOutputStream, Encoding.UTF8, true, 1024, true);
            while (!result.IsCompleted || !reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line is not null)
                {
                    Console.WriteLine(line);
                }
            }
        }).Start();

        using (var reader = new StreamReader(command.OutputStream, Encoding.UTF8, true, 1024, true))
        {
            while (!result.IsCompleted || !reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line is not null)
                {
                    Console.WriteLine(line);
                }
            }
        }

        command.EndExecute(result);
    }
}