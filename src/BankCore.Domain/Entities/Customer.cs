using BankCore.Domain.Common;
using BankCore.Domain.Exceptions;

namespace BankCore.Domain.Entities;

public class Customer
{
    private readonly List<Account> _accounts = [];

    // Required by EF Core.
    private Customer()
    {
    }

    public Customer(
        string customerNumber,
        string fullName,
        string nic,
        string email,
        string phoneNumber,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerNumber))
        {
            throw new DomainException("INVALID_CUSTOMER", "Customer number is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("INVALID_CUSTOMER", "Full name is required.");
        }

        if (!SriLankanNic.IsValid(nic))
        {
            throw new DomainException("INVALID_NIC", "NIC is not a valid Sri Lankan NIC number.");
        }

        CustomerNumber = customerNumber;
        FullName = fullName.Trim();
        Nic = SriLankanNic.Normalize(nic);
        CreatedAtUtc = createdAtUtc;
        UpdateContactDetails(email, phoneNumber);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string CustomerNumber { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string Nic { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

    public void UpdateContactDetails(string email, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("INVALID_EMAIL", "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException("INVALID_PHONE", "Phone number is required.");
        }

        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
    }
}
