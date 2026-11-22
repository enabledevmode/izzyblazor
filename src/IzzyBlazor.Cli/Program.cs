using IzzyBlazor.Cli;

// IZZY_PRO_CATALOG points at the private Pro catalog while working on it; without its files it adds nothing.
var pro = Environment.GetEnvironmentVariable("IZZY_PRO_CATALOG") is { Length: > 0 } folder ? folder : null;
var cli = new Cli(Path.Combine(AppContext.BaseDirectory, "catalog"), Environment.CurrentDirectory, Console.Out, Console.Error, pro);
return cli.Run(args);
