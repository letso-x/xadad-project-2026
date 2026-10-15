using MzukuluQMS.Api.Services.SignOffs;
namespace MzukuluQMS.Api.Tests;
public sealed class ContentHashTests
{
    private static string Hash(string json)=>QCFormContentHashService.HashCanonicalContent(json);
    [Fact] public void Workflow_metadata_does_not_change_version_2_hash()
    {
        var first=Hash("""{"qcForm":{"qcFormID":1,"status":"Submitted","submittedAt":"first"},"responses":[{"value":1}]}""");
        var second=Hash("""{"responses":[{"value":1.0}],"qcForm":{"submittedAt":"second","status":"Approved","qcFormID":1}}""");
        Assert.Equal(first,second); Assert.Matches("^[a-f0-9]{64}$",first);
    }
    [Theory]
    [InlineData("responses")][InlineData("projectFields")][InlineData("evidence")]
    public void Signable_content_changes_invalidate_hash(string property)
    {
        Assert.NotEqual(Hash("{\"qcForm\":{},\""+property+"\":[{\"value\":\"before\"}]}"),Hash("{\"qcForm\":{},\""+property+"\":[{\"value\":\"after\"}]}"));
    }
    [Fact] public void Nested_object_keys_are_ordinal_and_types_remain_distinct()
    {
        Assert.Equal(Hash("""{"qcForm":{},"responses":[{"b":false,"a":0}]}"""),Hash("""{"responses":[{"a":0,"b":false}],"qcForm":{}}"""));
        Assert.NotEqual(Hash("""{"qcForm":{},"value":false}"""),Hash("""{"qcForm":{},"value":"false"}"""));
    }
    [Theory][InlineData("executionHeader")][InlineData("remarks")]
    public void Captured_header_and_remarks_are_signable(string key)
    {Assert.NotEqual(Hash("{\"qcForm\":{},\""+key+"\":\"before\"}"),Hash("{\"qcForm\":{},\""+key+"\":\"after\"}"));}
    [Fact] public void Nonexistent_form_is_not_signed()=>Assert.Throws<KeyNotFoundException>(()=>Hash("""{"qcForm":null}"""));
}
