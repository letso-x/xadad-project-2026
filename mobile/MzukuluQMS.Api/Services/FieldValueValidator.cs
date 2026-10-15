using System.Globalization;
using System.Text.Json;

namespace MzukuluQMS.Api.Services;

public sealed class FieldValueValidator : IFieldValueValidator
{
    public void Validate(
        JsonElement value,
        string fieldType,
        bool isRequired,
        string? optionsJson,
        string? validationRulesJson)
    {
        if (IsEmpty(value))
        {
            if (isRequired)
            {
                throw new ArgumentException(
                    "A value is required for this field.");
            }

            return;
        }

        switch (fieldType)
        {
            case "Text":
            case "LongText":
            case "Reference":
                ValidateText(
                    value,
                    validationRulesJson);
                break;

            case "Number":
                ValidateNumber(
                    value,
                    requireInteger: true,
                    validationRulesJson);
                break;

            case "Decimal":
                ValidateNumber(
                    value,
                    requireInteger: false,
                    validationRulesJson);
                break;

            case "Boolean":
                ValidateBoolean(value);
                break;

            case "YesNo":
                ValidateYesNo(value);
                break;

            case "Date":
                ValidateDate(value);
                break;

            case "DateTime":
                ValidateDateTime(value);
                break;

            case "Dropdown":
                ValidateDropdown(
                    value,
                    optionsJson);
                break;

            case "MultiSelect":
                ValidateMultiSelect(
                    value,
                    optionsJson);
                break;

            case "Photo":
                throw new ArgumentException(
                    "Photo fields must be handled through evidence.");

            case "Signature":
                throw new ArgumentException(
                    "Signature fields must be handled through the signature/evidence workflow.");

            default:
                throw new ArgumentException(
                    $"Unsupported field type '{fieldType}'.");
        }
    }

    private static bool IsEmpty(JsonElement value)
    {
        if (value.ValueKind is
            JsonValueKind.Null or
            JsonValueKind.Undefined)
        {
            return true;
        }

        return value.ValueKind == JsonValueKind.String &&
               string.IsNullOrWhiteSpace(
                   value.GetString());
    }

    private static void ValidateText(
        JsonElement value,
        string? validationRulesJson)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException(
                "The field value must be text.");
        }

        var text =
            value.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(
                validationRulesJson))
        {
            return;
        }

        using var document =
            JsonDocument.Parse(
                validationRulesJson);

        var rules =
            document.RootElement;

        if (rules.TryGetProperty(
                "minLength",
                out var minLengthElement) &&
            minLengthElement.TryGetInt32(
                out var minLength) &&
            text.Length < minLength)
        {
            throw new ArgumentException(
                $"The value must contain at least {minLength} characters.");
        }

        if (rules.TryGetProperty(
                "maxLength",
                out var maxLengthElement) &&
            maxLengthElement.TryGetInt32(
                out var maxLength) &&
            text.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.");
        }
    }

    private static void ValidateNumber(
        JsonElement value,
        bool requireInteger,
        string? validationRulesJson)
    {
        if (value.ValueKind !=
            JsonValueKind.Number)
        {
            throw new ArgumentException(
                "The field value must be numeric.");
        }

        if (!value.TryGetDecimal(
                out var number))
        {
            throw new ArgumentException(
                "The numeric value is invalid.");
        }

        if (requireInteger &&
            decimal.Truncate(number) != number)
        {
            throw new ArgumentException(
                "The field value must be a whole number.");
        }

        if (string.IsNullOrWhiteSpace(
                validationRulesJson))
        {
            return;
        }

        using var document =
            JsonDocument.Parse(
                validationRulesJson);

        var rules =
            document.RootElement;

        if (rules.TryGetProperty(
                "min",
                out var minElement) &&
            minElement.TryGetDecimal(
                out var min) &&
            number < min)
        {
            throw new ArgumentException(
                $"The value cannot be less than {min}.");
        }

        if (rules.TryGetProperty(
                "max",
                out var maxElement) &&
            maxElement.TryGetDecimal(
                out var max) &&
            number > max)
        {
            throw new ArgumentException(
                $"The value cannot be greater than {max}.");
        }
    }

    private static void ValidateBoolean(
        JsonElement value)
    {
        if (value.ValueKind is not
            JsonValueKind.True and not
            JsonValueKind.False)
        {
            throw new ArgumentException(
                "The field value must be true or false.");
        }
    }

    private static void ValidateYesNo(
        JsonElement value)
    {
        if (value.ValueKind !=
            JsonValueKind.String)
        {
            throw new ArgumentException(
                "The field value must be Yes or No.");
        }

        var text =
            value.GetString();

        if (!string.Equals(
                text,
                "Yes",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                text,
                "No",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The field value must be Yes or No.");
        }
    }

    private static void ValidateDate(
        JsonElement value)
    {
        if (value.ValueKind !=
            JsonValueKind.String ||
            !DateOnly.TryParseExact(
                value.GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new ArgumentException(
                "The field value must be a valid date in yyyy-MM-dd format.");
        }
    }

    private static void ValidateDateTime(
        JsonElement value)
    {
        if (value.ValueKind !=
            JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new ArgumentException(
                "The field value must be a valid ISO 8601 date and time.");
        }
    }

    private static void ValidateDropdown(
        JsonElement value,
        string? optionsJson)
    {
        if (value.ValueKind !=
            JsonValueKind.String)
        {
            throw new ArgumentException(
                "The selected value must be text.");
        }

        var options =
            ReadOptions(optionsJson);

        var selectedValue =
            value.GetString();

        if (!options.Contains(
                selectedValue,
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The selected value is not a valid option for this field.");
        }
    }

    private static void ValidateMultiSelect(
        JsonElement value,
        string? optionsJson)
    {
        if (value.ValueKind !=
            JsonValueKind.Array)
        {
            throw new ArgumentException(
                "The field value must be an array of selected options.");
        }

        var options =
            ReadOptions(optionsJson);

        foreach (var item in
                 value.EnumerateArray())
        {
            if (item.ValueKind !=
                JsonValueKind.String)
            {
                throw new ArgumentException(
                    "Every selected option must be text.");
            }

            var selectedValue =
                item.GetString();

            if (!options.Contains(
                    selectedValue,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"'{selectedValue}' is not a valid option for this field.");
            }
        }
    }

    private static IReadOnlyList<string> ReadOptions(
        string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(
                optionsJson))
        {
            throw new ArgumentException(
                "This field does not have any configured options.");
        }

        using var document =
            JsonDocument.Parse(
                optionsJson);

        if (document.RootElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new ArgumentException(
                "The field options configuration is invalid.");
        }

        return document.RootElement
            .EnumerateArray()
            .Where(
                option =>
                    option.ValueKind ==
                    JsonValueKind.String)
            .Select(
                option =>
                    option.GetString()!)
            .ToList();
    }
}