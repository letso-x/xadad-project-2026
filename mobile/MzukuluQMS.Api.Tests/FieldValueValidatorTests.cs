using System.Text.Json;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Tests;

public sealed class FieldValueValidatorTests
{
    private readonly FieldValueValidator _validator = new();

    [Fact]
    public void Validate_RequiredTextWithValidValue_Succeeds()
    {
        var value = Json("\"CBL-001\"");

        _validator.Validate(
            value,
            "Text",
            isRequired: true,
            optionsJson: null,
            validationRulesJson:
                """
                {
                    "minLength": 1,
                    "maxLength": 50
                }
                """);
    }

    [Fact]
    public void Validate_RequiredTextWithEmptyValue_ThrowsArgumentException()
    {
        var value = Json("\"\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Text",
                isRequired: true,
                optionsJson: null,
                validationRulesJson: null));
    }

    [Fact]
    public void Validate_TextExceedingMaxLength_ThrowsArgumentException()
    {
        var value = Json("\"123456\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Text",
                isRequired: true,
                optionsJson: null,
                validationRulesJson:
                    """
                    {
                        "maxLength": 5
                    }
                    """));
    }

    [Fact]
    public void Validate_ValidDropdownOption_Succeeds()
    {
        var value = Json("\"Pass\"");

        _validator.Validate(
            value,
            "Dropdown",
            isRequired: true,
            optionsJson:
                """
                ["Pass", "Fail", "N/A"]
                """,
            validationRulesJson: null);
    }

    [Fact]
    public void Validate_InvalidDropdownOption_ThrowsArgumentException()
    {
        var value = Json("\"Banana\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Dropdown",
                isRequired: true,
                optionsJson:
                    """
                    ["Pass", "Fail", "N/A"]
                    """,
                validationRulesJson: null));
    }

    [Fact]
    public void Validate_IntegerNumberWithWholeNumber_Succeeds()
    {
        var value = Json("10");

        _validator.Validate(
            value,
            "Number",
            isRequired: true,
            optionsJson: null,
            validationRulesJson: null);
    }

    [Fact]
    public void Validate_IntegerNumberWithDecimal_ThrowsArgumentException()
    {
        var value = Json("10.5");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Number",
                isRequired: true,
                optionsJson: null,
                validationRulesJson: null));
    }

    [Fact]
    public void Validate_DecimalOutsideMaximum_ThrowsArgumentException()
    {
        var value = Json("101.5");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Decimal",
                isRequired: true,
                optionsJson: null,
                validationRulesJson:
                    """
                    {
                        "min": 0,
                        "max": 100
                    }
                    """));
    }

    [Fact]
    public void Validate_ValidBoolean_Succeeds()
    {
        var value = Json("true");

        _validator.Validate(
            value,
            "Boolean",
            isRequired: true,
            optionsJson: null,
            validationRulesJson: null);
    }

    [Fact]
    public void Validate_InvalidBoolean_ThrowsArgumentException()
    {
        var value = Json("\"true\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Boolean",
                isRequired: true,
                optionsJson: null,
                validationRulesJson: null));
    }

    [Fact]
    public void Validate_ValidDate_Succeeds()
    {
        var value = Json("\"2026-10-08\"");

        _validator.Validate(
            value,
            "Date",
            isRequired: true,
            optionsJson: null,
            validationRulesJson: null);
    }

    [Fact]
    public void Validate_InvalidDate_ThrowsArgumentException()
    {
        var value = Json("\"08/10/2026\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Date",
                isRequired: true,
                optionsJson: null,
                validationRulesJson: null));
    }

    [Fact]
    public void Validate_PhotoField_ThrowsArgumentException()
    {
        var value = Json("\"photo.jpg\"");

        Assert.Throws<ArgumentException>(() =>
            _validator.Validate(
                value,
                "Photo",
                isRequired: true,
                optionsJson: null,
                validationRulesJson: null));
    }

    private static JsonElement Json(string json)
    {
        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}