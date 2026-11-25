using IzzyBlazor.Generation;

namespace IzzyBlazor.Generation.Tests;

/// <summary>
/// izzy add crud as a free user has it: the grid page, in the starter with accounts, for a model in its DbContext
/// (the EF store, behind sign-in) and one named like a type the pages use (Task). They build together, and a second
/// run changes nothing. The list, detail and form pages are Pro; a key people type needs them.
/// </summary>
[Collection(ProjectBuild.Collection)]
public sealed class CrudTests : IDisposable
{
    readonly string _work = Directory.CreateTempSubdirectory("izzy-crud-").FullName;

    (int Exit, string Out, string Error) Izzy(string directory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), directory, output, error).Run(args);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task The_free_grid_builds_for_two_models_and_pages_are_Pro()
    {
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Acme", "--auth", "Individual", "--no-restore").Exit);
        var app = Path.Combine(_work, "Acme");
        Directory.CreateDirectory(Path.Combine(app, "Models"));
        File.WriteAllText(Path.Combine(app, "Models", "Models.cs"), """
            using System.ComponentModel.DataAnnotations;

            namespace Acme.Models;

            public enum CustomerStatus { Active, Trial, Churned }

            public abstract class Entity
            {
                public int Id { get; set; }
            }

            public class Customer : Entity
            {
                [Required, StringLength(40)] public string FirstName { get; set; } = "";
                [Required, StringLength(40)] public string LastName { get; set; } = "";
                [Required, EmailAddress] public string Email { get; set; } = "";
                public CustomerStatus? Status { get; set; }
                public decimal LifetimeValue { get; set; }
                public DateOnly Joined { get; set; }
                public bool Newsletter { get; set; }
                public List<Invoice> Invoices { get; set; } = [];
            }

            public enum Priority { Low, Normal, High }

            public class Task
            {
                public int Id { get; set; }
                public string Title { get; set; } = "";
                public Priority Priority { get; set; }
                public DateOnly? Due { get; set; }
            }

            public class Invoice
            {
                [Key, StringLength(20)] public string Number { get; set; } = "";
                public DateOnly Issued { get; set; }
                public decimal Total { get; set; }
            }
            """);
        var context = Path.Combine(app, "Data", "ApplicationDbContext.cs");
        File.WriteAllText(context, File.ReadAllText(context).Replace(
            "IdentityDbContext<ApplicationUser>(options)\n{\n}",
            "IdentityDbContext<ApplicationUser>(options)\n{\n    public DbSet<Acme.Models.Customer> Customers => Set<Acme.Models.Customer>();\n}"));

        var customers = Izzy(app, "add", "crud", "Customer");
        Assert.True(customers.Exit == 0, customers.Error);
        Assert.True(Izzy(app, "add", "crud", "Task").Exit == 0);
        Assert.Contains("--mode pages (Pro)", Izzy(app, "add", "crud", "Invoice").Error);
        Assert.Equal(3, Izzy(app, "add", "crud", "Customer", "--mode", "pages").Exit);

        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));
        Assert.Contains("AddScoped<Acme.Data.ICustomerStore, Acme.Data.EfCustomerStore>()", program);
        Assert.Contains("[Microsoft.AspNetCore.Authorization.Authorize]", File.ReadAllText(Path.Combine(app, "Components", "Pages", "Customers", "Index.razor")));
        Assert.False(File.Exists(Path.Combine(app, "Components", "Pages", "Customers", "Edit.razor")));
        // The model named Task is TaskItem in its pages, so Task stays System's.
        Assert.Contains("@using TaskItem = global::Acme.Models.Task", File.ReadAllText(Path.Combine(app, "Components", "Pages", "Tasks", "Index.razor")));
        Assert.Contains("Nothing to change", Izzy(app, "add", "crud", "Customer").Out);

        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(app, "Acme with crud grids for Customer and Task");
    }

    [AccountFact]
    public async Task With_Pro_the_http_store_puts_the_pages_in_the_client_project_over_an_api()
    {
        // An app whose pages run in the browser: crud refuses it, but for the Http store.
        Assert.Equal(0, Izzy(_work, "new", "starter", "blazor", "-n", "Shop", "--interactivity", "WebAssembly", "--all-interactive", "true", "--no-restore").Exit);
        var app = Path.Combine(_work, "Shop");
        Directory.CreateDirectory(Path.Combine(app, "Shop.Client", "Models"));
        File.WriteAllText(Path.Combine(app, "Shop.Client", "Models", "Product.cs"), """
            using System.ComponentModel.DataAnnotations;

            namespace Shop.Client.Models;

            public enum Stock { InStock, Low, Out }

            public class Product
            {
                public int Id { get; set; }
                [Required, StringLength(60)] public string Name { get; set; } = "";
                public Stock Stock { get; set; }
                public decimal Price { get; set; }
                public DateOnly Added { get; set; }
            }
            """);
        Assert.Contains("--store http (Pro)", Izzy(app, "add", "crud", "Product").Error);
        Assert.Equal(3, Izzy(app, "add", "crud", "Product", "--store", "http").Exit);

        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = new Cli.Cli(Path.Combine(ProjectBuild.RepoRoot, "catalog"), app, output, error, proRoot: ProjectBuild.PrivateCatalog).Run(["add", "crud", "Product", "--store", "http"]);
        Assert.True(exit == 0, error.ToString());

        Assert.True(File.Exists(Path.Combine(app, "Shop.Client", "Pages", "Products", "Index.razor")));
        Assert.Contains("AddScoped<Shop.Client.Data.IProductStore, Shop.Client.Data.HttpProductStore>()", File.ReadAllText(Path.Combine(app, "Shop.Client", "Program.cs")));
        var program = File.ReadAllText(Path.Combine(app, "Shop", "Program.cs"));
        Assert.Contains("AddSingleton<Shop.Client.Data.IProductStore, Shop.Data.MemoryProductStore>()", program);
        Assert.Contains("Shop.Data.ProductApi.Map(app);", program);

        ProjectBuild.PointAtRepo(_work);
        await ProjectBuild.BuildAsync(Path.Combine(app, "Shop"), "Shop, a WebAssembly app with a crud grid over HTTP");
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);
}
