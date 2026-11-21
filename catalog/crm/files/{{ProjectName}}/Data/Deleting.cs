using Microsoft.EntityFrameworkCore;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Deleting a record, and what goes with it. The rule: a note or an activity goes with the record when it's
/// about nothing else; one that's also about another record stays there. The database does the rest
/// (ApplicationDbContext.OnModelCreating): a deal's files go with it, and whatever pointed at the record points
/// at nothing. Each deletion is in the audit log, with the notes and activities it took.
/// </summary>
public static class Deleting
{
    /// <summary>
    /// Deletes a contact, with the notes and activities about them alone. Those on a deal or a company stay
    /// there, and their deals stay, with no primary contact.
    /// </summary>
    public static async Task DeleteAsync(this ApplicationDbContext db, Contact contact)
    {
        db.Notes.RemoveRange(await db.Notes.Where(n => n.ContactId == contact.Id && n.DealId == null && n.CompanyId == null).ToListAsync());
        db.Activities.RemoveRange(await db.Activities.Where(a => a.ContactId == contact.Id && a.DealId == null).ToListAsync());
        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a deal, with its files and the notes and activities about it alone. Those with a contact stay
    /// with the contact.
    /// </summary>
    public static async Task DeleteAsync(this ApplicationDbContext db, Deal deal)
    {
        db.Notes.RemoveRange(await db.Notes.Where(n => n.DealId == deal.Id && n.ContactId == null && n.CompanyId == null).ToListAsync());
        db.Activities.RemoveRange(await db.Activities.Where(a => a.DealId == deal.Id && a.ContactId == null).ToListAsync());
        db.Deals.Remove(deal);
        await db.SaveChangesAsync();
    }

    /// <summary>How many deals a company has, open or closed: one with any can't be deleted.</summary>
    public static Task<int> DealsOfAsync(this ApplicationDbContext db, int company) => db.Deals.CountAsync(d => d.CompanyId == company);

    /// <summary>
    /// Deletes a company that has no deals (<see cref="DealsOfAsync"/>), with its logo and the notes about it
    /// alone. Its people stay, with no company.
    /// </summary>
    public static async Task DeleteAsync(this ApplicationDbContext db, Company company)
    {
        db.Notes.RemoveRange(await db.Notes.Where(n => n.CompanyId == company.Id && n.ContactId == null && n.DealId == null).ToListAsync());
        db.Companies.Remove(company);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes an activity, with the notes written on it that are about no one and nothing else. The rest stay
    /// with their contact, deal or company.
    /// </summary>
    public static async Task DeleteAsync(this ApplicationDbContext db, Activity activity)
    {
        db.Notes.RemoveRange(await db.Notes
            .Where(n => n.ActivityId == activity.Id && n.ContactId == null && n.DealId == null && n.CompanyId == null).ToListAsync());
        db.Activities.Remove(activity);
        await db.SaveChangesAsync();
    }
}
