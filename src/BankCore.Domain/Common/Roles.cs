namespace BankCore.Domain.Common;

/// <summary>
/// Application roles. Segregation of duties: Admins manage, Tellers transact, Auditors observe.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Teller = "Teller";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All = [Admin, Teller, Auditor];
}
