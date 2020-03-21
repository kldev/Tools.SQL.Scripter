using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Tools.SQL.Scripter {
    class Program {
        static void Main(string[] args) {
            var app = new CommandLineApplication ( );
            app.Name = "Tools.SQL.Scripter";
            app.Description = ".NET Core console app for describe SQL Server database.";
            app.HelpOption ("-?|-h|--help");

            app.VersionOption ("-v|--version", () => $"Version {Assembly.GetEntryAssembly ( ).GetCustomAttribute<AssemblyInformationalVersionAttribute> ( ).InformationalVersion}");

            var dbOption = app.Option ("-d|--database",
                "The database name",
                CommandOptionType.SingleValue);

            var serverOption = app.Option ("-s|--server",
                "The server eg. 127.0.0.1,1443",
                CommandOptionType.SingleValue);

            var passwordOption = app.Option ("-p|--password",
                "The server password",
                CommandOptionType.SingleValue);

            var usernameOption = app.Option ("-u|--username",
                "The user",
                CommandOptionType.SingleValue);


            var outputOption = app.Option ("-o|--output",
                "Output files path",
                CommandOptionType.SingleValue);

            app.OnExecute (() => {
                var db = dbOption.Value ( );
                var outputDir = outputOption.Value ( );
                var password = passwordOption.Value ( );
                var username = usernameOption.Value ( );
                var server = serverOption.Value ( );

                if (string.IsNullOrEmpty (db)) {
                    Console.WriteLine ("Missing database name.");
                    return 0;
                }

                if (string.IsNullOrEmpty (server)) {
                    Console.WriteLine ("Missing server.");
                    return 0;
                }

                if (string.IsNullOrEmpty (password)) {
                    Console.WriteLine ("Missing user password.");
                    return 0;
                }

                if (string.IsNullOrEmpty (username)) {
                    Console.WriteLine ("Missing user.");
                    return 0;
                }

                if (string.IsNullOrEmpty (server)) {
                    Console.WriteLine ("Missing server address.");
                    return 0;
                }

                if (string.IsNullOrEmpty (outputDir)) {
                    Console.WriteLine ("Missing output directory.");
                    return 0;
                }

                var config = new DescriberConfig {
                    Password = password,
                    Schema = db,
                    Server = server,
                    User = username,
                    OutputDir = outputDir
                };
                var logger = CreateLogger ( ).CreateLogger<DatabaseDescriber> ( );

                var describer = new DatabaseDescriber (config, logger);
                Task.Run (() => describer.Execute ( )).Wait ( );

                return 0;

            });

            try {
                app.Execute (args);
            }
            catch (CommandParsingException ex) {
                Console.WriteLine (ex.Message);
            }
            catch (Exception ex) {
                Console.WriteLine ("Unable to execute application: {0}", ex.Message);
            }

        }

        public static ILoggerFactory CreateLogger() {
            var loggerFactory = LoggerFactory.Create (builder => {
                builder.AddFilter ("Microsoft", LogLevel.Warning)
                    .AddFilter ("System", LogLevel.Warning)
                    .AddFilter ("Tools.SQL.Scripter", LogLevel.Debug)
                    .AddConsole ( );
            }
            );

            return loggerFactory;
        }
    }
}
