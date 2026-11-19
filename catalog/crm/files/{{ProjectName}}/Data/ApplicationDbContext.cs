using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyLogo> CompanyLogos => Set<CompanyLogo>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Note> Notes => Set<Note>();

    /// <summary>The workspace's settings: one row (Data/Workspace.cs).</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>The keys that protect cookies and form tokens (Program.cs, AddDataProtection).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Whether saves are written to the audit log; the seeder turns it off to write its own history.</summary>
    public bool Auditing { get; set; } = true;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // A team member who leaves takes no records with them: theirs are left with no owner.
        builder.Entity<Company>().HasOne(c => c.Owner).WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.SetNull);
        // A logo goes with its company.
        builder.Entity<Company>().HasOne(c => c.Logo).WithOne().HasForeignKey<CompanyLogo>(l => l.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Contact>().HasIndex(c => c.Email).IsUnique();
        builder.Entity<Contact>().HasOne(c => c.Owner).WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Contact>().HasOne(c => c.Company).WithMany(c => c.Contacts).HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.SetNull);
        // A company with deals can't be deleted; a deal's contact can leave it.
        builder.Entity<Deal>().HasOne(d => d.Company).WithMany(c => c.Deals).HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Deal>().HasOne(d => d.Contact).WithMany().HasForeignKey(d => d.ContactId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Deal>().HasOne(d => d.Owner).WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Deal>().HasIndex(d => d.Stage);
        builder.Entity<Activity>().HasOne(a => a.Contact).WithMany().HasForeignKey(a => a.ContactId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Activity>().HasOne(a => a.Deal).WithMany().HasForeignKey(a => a.DealId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Activity>().HasOne(a => a.AssignedTo).WithMany().HasForeignKey(a => a.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Activity>().HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Activity>().HasIndex(a => a.Start);
        builder.Entity<Note>().HasOne(n => n.Contact).WithMany().HasForeignKey(n => n.ContactId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Note>().HasOne(n => n.Deal).WithMany().HasForeignKey(n => n.DealId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Note>().HasOne(n => n.Company).WithMany().HasForeignKey(n => n.CompanyId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Note>().HasOne(n => n.Author).WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AuditEvent>().HasIndex(e => e.Time);
        builder.Entity<AuditEvent>().HasIndex(e => new { e.EntityType, e.EntityId });
        builder.Entity<AuditEvent>().HasIndex(e => e.ActorId);
    }
}
