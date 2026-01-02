namespace Guardhouse.SDK.Models.Validation;

using System;
using System.ComponentModel.DataAnnotations;
using Models;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RequiredIfIntrospectionAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var options = (GuardhouseResourceOptions?)validationContext.ObjectInstance;

        if (options == null)
        {
            return new ValidationResult("Unable to validate GuardhouseResourceOptions");
        }

        if (options.ValidationMode == TokenValidationMode.Introspection &&
            (value == null || string.IsNullOrWhiteSpace(value as string)))
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.MemberName} is required when ValidationMode is Introspection");
        }

        return ValidationResult.Success;
    }
}
