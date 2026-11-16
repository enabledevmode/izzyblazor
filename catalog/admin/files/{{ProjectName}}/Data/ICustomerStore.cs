using IzzyBlazor;
using Customer = global::{{RootNamespace}}.Data.Customer;
using CustomerStatus = global::{{RootNamespace}}.Data.CustomerStatus;
using CustomerPlan = global::{{RootNamespace}}.Data.CustomerPlan;

namespace {{RootNamespace}}.Data;

/// <summary>
/// Where the customers pages read and write customers, from izzy add crud. The pages ask only this,
/// so moving the customers to another database or an API means a new store, not new pages.
/// </summary>
public interface ICustomerStore
{
    /// <summary>How many customers the list's filters keep; with none, how many there are.</summary>
    Task<int> CountAsync(string? search = null, CustomerStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The page of customers the grid asks for, sorted as it asks, after the list's own filters:
    /// text to look for (<paramref name="search"/>), and a Status.
    /// </summary>
    ValueTask<DataResult<Customer>> FindAsync(DataRequest<Customer> request, string? search, CustomerStatus? status);

    /// <summary>
    /// Writes the customers to an Excel file, as the grid's address asks (<paramref name="query"/>: its search,
    /// sort and columns).
    /// </summary>
    Task ExportAsync(Stream output, string? query, CancellationToken cancellationToken = default);

    /// <summary>The customer with this Id, or null when there's none.</summary>
    Task<Customer?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a customer, giving it its Id when it has none. False when it can't be added, such as when
    /// its Id is taken.
    /// </summary>
    Task<bool> AddAsync(Customer item, CancellationToken cancellationToken = default);

    /// <summary>Saves a customer read with <see cref="GetAsync"/> and changed.</summary>
    Task UpdateAsync(Customer item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the customer with this Id. False when it can't be, because other records point to it.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
