namespace MzukuluQMS.Api.Exceptions;
public sealed class OperationReplayException(int status, string json, long revision, long formId) : Exception("Operation already completed.")
{
 public int Status {get;}=status; public string Json {get;}=json; public long Revision {get;}=revision; public long FormId {get;}=formId;
}
