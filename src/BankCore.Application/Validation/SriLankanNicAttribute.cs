using System.ComponentModel.DataAnnotations;
using BankCore.Domain.Common;

namespace BankCore.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class SriLankanNicAttribute()
    : ValidationAttribute("The {0} field must be a valid Sri Lankan NIC (e.g. 199012345678 or 901234567V).")
{
    public override bool IsValid(object? value) =>
        value is null || (value is string nic && SriLankanNic.IsValid(nic));
}
