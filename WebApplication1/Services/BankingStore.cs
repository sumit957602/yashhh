using System.Collections.Concurrent;

namespace BankingPlatform.Api.Services;

public sealed class BankingStore
{
    public ConcurrentDictionary<Guid, Customer> Customers { get; } = new();
    public ConcurrentDictionary<Guid, Account> Accounts { get; } = new();
    public ConcurrentDictionary<Guid, Transaction> Transactions { get; } = new();
    public ConcurrentDictionary<Guid, Loan> Loans { get; } = new();
    public ConcurrentDictionary<Guid, Notification> Notifications { get; } = new();
    public ConcurrentDictionary<string, (string Password, Guid CustomerId)> Credentials { get; } = new();
}
