using System.Text.Json.Nodes;

namespace MzukuluQMS.Mobile.Core;

// Presentation relationships live in the existing, versioned ValidationRules JSON.
// Every member remains a normal ChecklistField/QCFormField with its own response.
public sealed record ChecklistItemGroup(JsonNode Primary, IReadOnlyList<JsonNode> Fields, bool IsInspectionItem);

public static class ChecklistItems
{
    public static JsonObject NewField() => new()
    {
        ["key"]="field_"+Guid.NewGuid().ToString("N"), ["label"]="", ["type"]="Text",
        ["required"]=false, ["requiresPhoto"]=false, ["requiresSignature"]=false,
        ["options"]=new JsonArray(), ["validationRules"]=new JsonObject()
    };

    public static JsonObject NewItem()
    {
        var field=NewField();field["type"]="Dropdown";field["required"]=true;
        field["options"]=new JsonArray("A","R","N/A");
        field["validationRules"]!["inspectionItem"]=new JsonObject{["key"]=field["key"]!.ToString(),["role"]="Result"};
        return field;
    }

    public static string? ItemKey(JsonNode field) => field["validationRules"]?["inspectionItem"]?["key"]?.ToString();
    public static string? Role(JsonNode field) => field["validationRules"]?["inspectionItem"]?["role"]?.ToString();
    public static string? Unit(JsonNode field) => field["validationRules"]?["unit"]?.ToString();
    private static string Key(JsonNode field) => (field["fieldKey"]??field["key"])?.ToString()??"";

    public static IReadOnlyList<ChecklistItemGroup> Groups(JsonArray fields)
    {
        var nodes=fields.Where(f=>f is not null).Select(f=>f!).ToArray();
        var results=nodes.Where(f=>Role(f)=="Result"&&ItemKey(f)==Key(f)).ToDictionary(Key,StringComparer.Ordinal);
        var groups=new List<ChecklistItemGroup>();
        foreach(var field in nodes)
        {
            if(Role(field)=="Additional"&&ItemKey(field) is { } parent&&results.ContainsKey(parent))continue;
            var item=results.TryGetValue(Key(field),out var result)&&ReferenceEquals(result,field);
            groups.Add(new(field,item?new[]{field}.Concat(nodes.Where(f=>Role(f)=="Additional"&&ItemKey(f)==Key(field))).ToArray():new[]{field},item));
        }
        return groups;
    }

    public static void AddAssociated(JsonArray fields,JsonNode item)
    {
        var field=NewField();field["validationRules"]!["inspectionItem"]=new JsonObject{["key"]=Key(item),["role"]="Additional"};
        var group=Groups(fields).Single(g=>ReferenceEquals(g.Primary,item));
        fields.Insert(fields.IndexOf(group.Fields.Last())+1,field);
    }

    public static void Remove(JsonArray fields,ChecklistItemGroup group)
    {foreach(var field in group.Fields)fields.Remove(field);}

    public static void Move(JsonArray fields,ChecklistItemGroup group,int direction)
    {
        var groups=Groups(fields).ToList();var from=groups.FindIndex(g=>ReferenceEquals(g.Primary,group.Primary));var to=from+direction;
        if(from<0||to<0||to>=groups.Count)return;
        var moving=groups[from];groups.RemoveAt(from);groups.Insert(to,moving);
        var ordered=groups.SelectMany(g=>g.Fields).ToArray();fields.Clear();foreach(var field in ordered)fields.Add(field);
    }

    public static bool IsResultOptions(IReadOnlyCollection<string> options) => options.Count==3&&
        (options.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"A","R","N/A"})||options.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"Pass","Fail","N/A"})||options.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"Accepted","Rejected","N/A"}));
}
