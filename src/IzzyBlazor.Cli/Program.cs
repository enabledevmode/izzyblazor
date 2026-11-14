using IzzyBlazor.Cli;

var cli = new Cli(Path.Combine(AppContext.BaseDirectory, "catalog"), Environment.CurrentDirectory, Console.Out, Console.Error);
return cli.Run(args);
