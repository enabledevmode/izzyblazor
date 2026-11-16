using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>The keys that protect cookies and form tokens (Program.cs, AddDataProtection).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Whether saves are written to the audit log; the seeder turns it off to write its own history.</summary>
    public bool Auditing { get; set; } = true;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
        builder.Entity<Invoice>().HasIndex(i => i.Number).IsUnique();
        builder.Entity<Invoice>().HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Invoice>().HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuditEvent>().HasIndex(e => e.Time);
        builder.Entity<AuditEvent>().HasIndex(e => new { e.EntityType, e.EntityId });
        builder.Entity<AuditEvent>().HasIndex(e => e.ActorId);
    }
}
