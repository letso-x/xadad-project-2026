using System.Threading.Tasks;
namespace MzuApplication.Data
{
 internal static class ItpData
 {
  public static string Route(string project){return "api/projects/"+long.Parse(project)+"/itps";}
  public static Task<object> ListAsync(string project){return ApiClient.Shared.SendAsync("GET",Route(project));}
  public static Task<object> GetAsync(string project,long version){return ApiClient.Shared.SendAsync("GET",Route(project)+"/"+version);}
  public static Task<object> ProgressAsync(string project){return ApiClient.Shared.SendAsync("GET",Route(project)+"/progress");}
  public static Task<object> AuditAsync(string project,long version){return ApiClient.Shared.SendAsync("GET",Route(project)+"/"+version+"/audit");}
  public static Task<object> CreateAsync(string project,object definition){return ApiClient.Shared.SendAsync("POST",Route(project),definition);}
  public static Task<object> SaveAsync(string project,long version,long stamp,object definition){return ApiClient.Shared.SendAsync("PUT",Route(project)+"/"+version,new{expectedStamp=stamp,definition});}
  public static Task<object> ActAsync(string project,long version,long stamp,string action,long? activity,string comments,long? qc)
  {return ApiClient.Shared.SendAsync("POST",Route(project)+"/"+version+"/"+action,new{expectedStamp=stamp,activityId=activity,comments,qcFormId=qc});}
 }
}
