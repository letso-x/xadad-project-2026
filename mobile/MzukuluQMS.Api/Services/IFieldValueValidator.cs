using System.Text.Json;

namespace MzukuluQMS.Api.Services;

public interface IFieldValueValidator
{
    void Validate(
        JsonElement value,
        string fieldType,
        bool isRequired,
        string? optionsJson,
        string? validationRulesJson);
}