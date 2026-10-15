using System.Text.Json;
namespace MzukuluQMS.Api.DTOs;
public sealed record CreateChecklistTemplateRequest(string DocumentNumber,string DocumentName,string? Subtitle,string Revision,DateOnly IssueDate,bool Active,string? Scope,string? AcceptanceCriteria,IReadOnlyList<TemplateFieldInput> ProjectFields,IReadOnlyList<TemplateSectionInput> Sections,IReadOnlyList<string> RequiredSignOffTypes);
public sealed record TemplateSectionInput(string? Title,bool Required,IReadOnlyList<TemplateFieldInput> Fields);
public sealed record TemplateFieldInput(string Key,string Label,string Type,bool Required,bool RequiresPhoto,bool RequiresSignature,string? HelpText,string? Placeholder,JsonElement? Options,JsonElement? ValidationRules);
