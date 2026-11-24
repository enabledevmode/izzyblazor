using IzzyBlazor.Cli;

// IZZY_PRO_CATALOG points at the private catalog (the account items' files, the Pro items) while working on
// it. Otherwise the account's copy is used, once izzy login has brought it. A folder that isn't that catalog
// is not taken: the variable alone would otherwise stand for Pro access (izzy set) on any machine.
var pro = Environment.GetEnvironmentVariable("IZZY_PRO_CATALOG") is { Length: > 0 } folder ? folder : null;
if (pro is not null && !(Directory.Exists(pro) && Directory.EnumerateDirectories(pro).Any(item => Directory.Exists(Path.Combine(item, "files")))))
{
    Console.Error.WriteLine($"IZZY_PRO_CATALOG is {pro}, which doesn't hold the catalog's items: it's ignored.");
    pro = null;
}
var cli = new Cli(Path.Combine(AppContext.BaseDirectory, "catalog"), Environment.CurrentDirectory, Console.Out, Console.Error, pro, account: AccountSession.FromEnvironment());
return cli.Run(args);
