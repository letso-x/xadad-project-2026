using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Models;

internal static class Program
{
 private static int passed,failed;
 [STAThread] private static int Main()
 {
  LocalDrafts.StorageRoot=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synthetic-drafts");
  Run("Single live HTTPS API origin",()=>Check(ApiConfiguration.BaseUrl=="https://xadad-project-2026.onrender.com/"));
  Run("Desktop assembly has no PostgreSQL provider dependency",()=>Check(!typeof(Auth).Assembly.GetReferencedAssemblies().Any(a=>a.Name=="Npgsql")));
  Run("JSON boundary preserves unknown properties",()=>Check(Json.Text(Json.Clone(Json.Read("{\"extra\":\"retained\"}")),"extra")=="retained"));
  Run("JSON boundary handles case-insensitive API properties",()=>Check(Json.Id(Json.Read("{\"QCFormID\":37}"),"qcFormID")==37));
  Run("Anonymous protected request is blocked",()=>Throws<InvalidOperationException>(()=>new Fixture().Session.ProtectedTokenAsync().GetAwaiter().GetResult()));
  Run("Password login does not grant protected API access",()=>{var f=new Fixture();f.Login();Check(!f.Session.Verified);Throws<InvalidOperationException>(()=>f.Session.ProtectedTokenAsync().GetAwaiter().GetResult());});
  Run("Returning-user factors skip enrollment",()=>{var f=new Fixture();f.Login();Check(f.Session.FactorId==f.Factor && f.EnrollCalls==0 && f.Session.SetupKey==null);});
  Run("MFA upgrades session and loads API permissions",()=>{var f=new Fixture();f.Login();f.Verify();Check(f.Session.Verified && f.Session.Can("ReadProjects"));});
  Run("MFA incorrect code cannot open protected session",()=>{var f=new Fixture();f.RejectCode=true;f.Login();Throws<InvalidOperationException>(()=>f.Verify());Check(!f.Session.Verified);});
  Run("MFA response with AAL1 is rejected",()=>{var f=new Fixture();f.WeakUpgrade=true;f.Login();Throws<InvalidOperationException>(()=>f.Verify());Check(!f.Session.Verified);});
  Run("First enrollment creates one pending factor across repeats",()=>{var f=new Fixture(false);f.Login();f.Session.EnrollAsync().GetAwaiter().GetResult();f.Session.EnrollAsync().GetAwaiter().GetResult();Check(f.EnrollCalls==1&&f.Session.SetupKey!=null);});
  Run("Verified factors are never deleted during enrollment",()=>{var f=new Fixture();f.Login();f.Session.EnrollAsync().GetAwaiter().GetResult();Check(f.EnrollCalls==0&&f.DeleteCalls==0);});
  Run("Verified-factor session clears on logout",()=>{var f=new Fixture();f.Login();f.Verify();Auth.SignOut();Check(!f.Session.Verified && Auth.Current==null && Repository.Db.Projects.Count==0);});
  Run("Protected requests use bearer token and live origin",()=>{var f=Ready();f.Api.SendAsync("GET","api/projects").GetAwaiter().GetResult();Check(f.LastUri.StartsWith(ApiConfiguration.BaseUrl,StringComparison.Ordinal)&&f.LastAuth.Scheme=="Bearer");});
  Run("Absolute routes cannot redirect bearer tokens",()=>{var f=Ready();Throws<ArgumentException>(()=>f.Api.SendAsync("GET","https://other.invalid/").GetAwaiter().GetResult());});
  Run("Root-relative routes cannot escape API origin",()=>{var f=Ready();Throws<ArgumentException>(()=>f.Api.SendAsync("GET","//other.invalid/").GetAwaiter().GetResult());});
  Run("Form writes carry exact If-Match and idempotency header",()=>{var f=Ready();f.Api.SendAsync("PUT","api/qc-forms/9/responses/1",new{value="R"},"12").GetAwaiter().GetResult();Check(f.LastMatch=="\"12\""&&Guid.TryParse(f.LastKey,out var ignored));});
  Run("Mutation revision comes from response ETag",()=>{var f=Ready();var form=new QcForm{Id="9",Revision=12};FormData.WriteAsync(form,"PUT","/responses/1",new{value="R"}).GetAwaiter().GetResult();Check(form.Revision==13);});
  Run("Conflict never silently advances local form revision",()=>{var f=Ready();f.ApiStatus=HttpStatusCode.Conflict;var form=new QcForm{Id="9",Revision=12};Throws<ApiException>(()=>FormData.WriteAsync(form,"PUT","/responses/1",new{value="R"}).GetAwaiter().GetResult());Check(form.Revision==12);});
  Run("Server errors do not expose internal response bodies",()=>{var f=Ready();f.ApiStatus=HttpStatusCode.InternalServerError;try{f.Api.SendAsync("GET","api/projects").GetAwaiter().GetResult();throw new Exception("Expected error");}catch(ApiException ex){Check(!ex.Message.Contains("SENSITIVE_INTERNAL"));}});
  Run("Client edits preserve hidden address and client code",()=>{var f=Ready();SharedData.LoadClientsAsync().GetAwaiter().GetResult();var c=Repository.Db.Clients.Single();SharedData.SaveClientAsync(c,"Renamed","Name","contact@example.invalid","123",true).GetAwaiter().GetResult();Check(Json.Text(f.LastClientWrite,"addressLine1")=="Retain address"&&Json.Text(f.LastClientWrite,"clientCode")=="C1"&&Json.Text(f.LastClientWrite,"contactEmail")=="contact@example.invalid");});
  Run("Project creation uses numeric client ID and API DTO names",()=>{var f=Ready();SharedData.CreateProjectAsync(new Project{ClientId="7",Name="Project",Number="P1",Site="Site"}).GetAwaiter().GetResult();Check(Json.Id(f.LastProjectWrite,"clientID")==7&&Json.Text(f.LastProjectWrite,"projectName")=="Project");});
  Run("QC form pagination collects every server page",()=>{var f=Ready();FormData.LoadListAsync("4").GetAwaiter().GetResult();Check(Repository.Db.Forms.Count==101&&f.PageCalls==2);});
  Run("Submitted form is not represented as approved",()=>Check(FormData.Map(Json.Read("{\"status\":\"Submitted\"}")).Status==RecordStatus.Submitted));
  Run("Template mapping retains version and dynamic metadata",()=>{var t=TemplateData.Map(TemplateFixture());Check(t.VersionId==3&&t.RemoteDefinition!=null&&t.RequiredSignOffTypes.Single()=="Inspector");});
  Run("Legacy editor refuses destructive flattening of number fields",()=>{var t=TemplateData.Map(TemplateFixture());Check(!TemplateData.CanEdit(t));});
  Run("A/R/N/A dynamic field keeps segmented buttons",()=>{using(var c=ApiFieldEditor.Create(Json.Read("{\"fieldType\":\"Dropdown\",\"options\":[\"A\",\"R\",\"N/A\"]}"),"R",v=>{})){Check(c is ResponseSelector && ((ResponseSelector)c).Value==ItemResponse.Rejected);}});
  Run("Boolean false is retained as an answered choice",()=>{using(var c=ApiFieldEditor.Create(Json.Read("{\"fieldType\":\"Boolean\"}"),false,v=>{})){Check(((ComboBox)c).SelectedItem is bool && !(bool)((ComboBox)c).SelectedItem);}});
  Run("Number editor preserves decimal JSON values and existing input control",()=>{object answer=null;using(var c=ApiFieldEditor.Create(Json.Read("{\"fieldType\":\"Decimal\"}"),null,v=>answer=v)){((TextInput)c).Value="12.5";Check(answer is decimal && (decimal)answer==12.5m);}});
  Run("MultiSelect retains checked choices",()=>{object answer=null;using(var c=ApiFieldEditor.Create(Json.Read("{\"fieldType\":\"MultiSelect\",\"options\":[\"One\",\"Two\"]}"),new object[]{"One"},v=>answer=v)){((CheckedListBox)c).SetItemChecked(1,true);Check(((object[])answer).Length==2);}});
  Run("Encrypted drafts preserve values and base revision",()=>{var f=Ready();LocalDrafts.Save("920000001",17,new Dictionary<string,object>{{"response:3","R"}});var draft=LocalDrafts.Read("920000001");Check(Json.Id(draft,"revision")==17&&Json.Text(Json.Get(draft,"values"),"response:3")=="R");});
  Run("Session switch rejects an in-flight old-session response",()=>{var f=Ready();var completion=new TaskCompletionSource<bool>();f.Delay=completion.Task;var request=f.Api.SendAsync("GET","api/projects");f.Session.Clear();completion.SetResult(true);Throws<InvalidOperationException>(()=>request.GetAwaiter().GetResult());});
  Run("Saved response remains visible in the execution snapshot",()=>{var f=Ready();var form=new QcForm{Id="9",Revision=12,RemoteDetails=Json.Map(Json.Read("{\"sections\":[{\"fields\":[{\"qcResponseID\":1,\"value\":\"A\"}]}]}"))};FormData.SaveValueAsync(form,"response:1","R").GetAwaiter().GetResult();Check(Json.Text(Json.Rows(Json.Rows(form.RemoteDetails,"sections")[0],"fields")[0],"value")=="R");});
  Run("Business-rule conflicts do not masquerade as revision conflicts",()=>Check(!ApiClient.Error(HttpStatusCode.Conflict,"{\"detail\":\"Complete required signatures first.\"}").IsRevisionConflict));
  Run("Actual stale-revision response is recognised",()=>Check(ApiClient.Error(HttpStatusCode.Conflict,"{\"detail\":\"The central record changed. Preserve local work and reload the current revision.\"}").IsRevisionConflict));
  Run("Audit action names stay readable",()=>Check(ReportingData.ActionText("QCFormResponseUpdated")=="QC Form Response Updated"));
  Run("ITP writes carry server stamp and exact action contract",()=>{var f=Ready();object input=null;f.Override=async r=>{input=Json.Read(await r.Content.ReadAsStringAsync());return Fixture.Response(new{id=5,stamp=8});};ItpData.ActAsync("4",5,7,"release",2,"Criteria met",null).GetAwaiter().GetResult();Check(f.LastUri.EndsWith("api/projects/4/itps/5/release")&&Json.Id(input,"expectedStamp")==7&&Json.Id(input,"activityId")==2&&Json.Get(input,"userID")==null);});
  Run("ITP draft edits preserve definitions without client hold rules",()=>{var f=Ready();object input=null;f.Override=async r=>{input=Json.Read(await r.Content.ReadAsStringAsync());return Fixture.Response(new{id=5,stamp=8});};ItpData.SaveAsync("4",5,7,new{number="ITP1",title="Plan",activities=new object[0]}).GetAwaiter().GetResult();Check(Json.Text(Json.Get(input,"definition"),"number")=="ITP1"&&Json.Id(input,"expectedStamp")==7);});
  Run("Cable update carries both revisions and preserves hidden size/cores",()=>
  {
   var f=Ready();var record=Json.Map(Json.Read("{\"register\":{\"revision\":4,\"registerNumber\":\"CR1\"},\"rows\":[{\"cableRecordID\":11,\"revision\":2,\"cableNumber\":\"C1\",\"cableSize\":\"16mm\",\"cores\":\"3\",\"length\":10,\"pulled\":true}]}"));object written=null;string route=null;
   f.Override=async r=>{if(r.Method==HttpMethod.Put){written=Json.Read(await r.Content.ReadAsStringAsync());route=r.RequestUri.PathAndQuery;return Fixture.Response(new{});}return Fixture.Response(record);};
   var register=CableData.LoadAsync("4","9").GetAwaiter().GetResult();register.Cables[0].From="Changed";CableData.SaveAsync(register).GetAwaiter().GetResult();Check(route.EndsWith("/cables/11?registerRevision=4")&&Json.Id(written,"expectedRevision")==2&&Json.Text(written,"cableSize")=="16mm"&&Json.Text(written,"cores")=="3");
  });
  Run("Cable conflict retains edited row and original revision",()=>
  {
   var f=Ready();f.Override=r=>Task.FromResult(Fixture.Response(Json.Read("{\"register\":{\"revision\":4,\"registerNumber\":\"CR1\"},\"rows\":[{\"cableRecordID\":11,\"revision\":2,\"cableNumber\":\"C1\"}]}")));
   var register=CableData.LoadAsync("4","9").GetAwaiter().GetResult();register.Cables[0].From="Unsent";f.ApiStatus=HttpStatusCode.Conflict;Throws<ApiException>(()=>CableData.SaveAsync(register).GetAwaiter().GetResult());Check(register.Revision==4&&register.Cables[0].From=="Unsent");
  });
  Run("NCR verification posts revision and outcome without acting user ID",()=>
  {
   var f=Ready();object posted=null;string route=null;
   f.Override=async r=>{if(r.Method==HttpMethod.Post){posted=Json.Read(await r.Content.ReadAsStringAsync());route=r.RequestUri.AbsolutePath;var ack=Fixture.Response(new{});ack.Headers.ETag=new EntityTagHeaderValue("\"13\"");return ack;}if(r.RequestUri.AbsolutePath.EndsWith("/ncrs"))return Fixture.Response(new object[0]);return Fixture.Response(new{qcFormID=9,projectID=4,revision=12,status="Submitted"});};
   NcrData.VerifyAsync(new Ncr{Id="3",FormId="9",RemoteRecord=Json.Map(Json.Read("{\"revision\":7}"))},true,"Verified independently").GetAwaiter().GetResult();Check(route=="/api/qc-forms/9/ncrs/3/verify"&&Json.Id(posted,"expectedRevision")==7&&Json.Bool(posted,"passed")&&Json.Get(posted,"userID")==null);
  });
  Run("Audit query pages server events and retains actor names",()=>
  {
   var f=Ready();Repository.Db.Forms.Add(new QcForm{Id="9",QcRecordNo="QC9"});int pages=0;
   f.Override=r=>{if(r.RequestUri.AbsolutePath.EndsWith("audit-history"))return Task.FromResult(Fixture.Response(new object[0]));pages++;return Task.FromResult(Fixture.Response(Enumerable.Range(pages==1?1:101,pages==1?100:1).Select(i=>new{auditEventID=i,actorName="Alice Reviewer",action="QCFormResponseUpdated",occurredAt="2026-10-16T10:00:00Z"}).ToArray()));};
   ReportingData.LoadAuditAsync().GetAwaiter().GetResult();Check(pages==2&&Repository.Db.Audit.Count==101&&Repository.Db.Audit.All(a=>a.User=="Alice Reviewer"));
  });
  Run("Expired AAL2 session refreshes without changing identity",()=>{var f=Ready();f.Expire();Check(f.Session.ProtectedTokenAsync().GetAwaiter().GetResult()!=null&&f.RefreshCalls==1&&f.Session.Verified);});
  Run("Refresh cannot downgrade MFA and retain protected access",()=>{var f=Ready();f.WeakRefresh=true;f.Expire();Throws<InvalidOperationException>(()=>f.Session.ProtectedTokenAsync().GetAwaiter().GetResult());Check(!f.Session.Verified);});
  Run("Refresh cannot switch authenticated subject",()=>{var f=Ready();f.OtherRefreshSubject=true;f.Expire();Throws<InvalidOperationException>(()=>f.Session.ProtectedTokenAsync().GetAwaiter().GetResult());});
  Run("Template revision preserves existing field evidence and rules",()=>
  {
   var f=Ready();var definition=Json.Read("{\"checklistTemplateID\":2,\"checklistTemplateVersionID\":3,\"requiredSignOffTypes\":[\"Inspector\"],\"projectFields\":[],\"sections\":[{\"checklistSectionID\":1,\"isRequired\":true,\"fields\":[{\"fieldKey\":\"original_result\",\"fieldLabel\":\"Original\",\"fieldType\":\"Dropdown\",\"isRequired\":true,\"requiresPhoto\":true,\"options\":[\"A\",\"R\",\"N/A\"],\"validationRules\":{\"inspectionItem\":{\"key\":\"original_result\",\"role\":\"Result\"},\"custom\":\"retained\"}}]}]}");object posted=null;
   f.Override=async r=>{if(r.Method==HttpMethod.Post){posted=Json.Read(await r.Content.ReadAsStringAsync());return Fixture.Response(new{checklistTemplateID=2,checklistTemplateVersionID=4});}return Fixture.Response(definition);};
   var draft=TemplateData.Map(definition);draft.Sections[0].Items[0].Text="Changed label";TemplateData.SaveAsync(draft).GetAwaiter().GetResult();var field=Json.Rows(Json.Rows(posted,"sections")[0],"fields")[0];Check(Json.Text(field,"key")=="original_result"&&Json.Bool(field,"requiresPhoto")&&Json.Text(Json.Get(field,"validationRules"),"custom")=="retained");
  });
  Run("NCR correction chains uploaded evidence and form/NCR revisions",()=>
  {
   var f=Ready();object posted=null;string correctedMatch=null;
   f.Override=async r=>
   {
    var path=r.RequestUri.AbsolutePath;
    if(path.EndsWith("/evidence")){var response=Fixture.Response(new{evidenceID=91});response.Headers.ETag=new EntityTagHeaderValue("\"13\"");return response;}
    if(path.EndsWith("/correct")){posted=Json.Read(await r.Content.ReadAsStringAsync());correctedMatch=r.Headers.IfMatch.Single().Tag;var response=Fixture.Response(new{});response.Headers.ETag=new EntityTagHeaderValue("\"14\"");return response;}
    if(path.EndsWith("/ncrs"))return Fixture.Response(new object[0]);
    return Fixture.Response(new{qcFormID=9,projectID=4,revision=12,status="InProgress"});
   };
   NcrData.CorrectAsync(new Ncr{Id="3",FormId="9",RemoteRecord=Json.Map(Json.Read("{\"revision\":7,\"qcFormFieldID\":42}"))},"Major","Installation",true,"Repair","Corrected",new byte[]{1,2,3},"synthetic.png","image/png").GetAwaiter().GetResult();Check(Json.Id(posted,"correctiveEvidenceID")==91&&Json.Id(posted,"expectedRevision")==7&&correctedMatch=="\"13\"");
  });
  Console.WriteLine("Desktop migration checks: "+passed+" passed; "+failed+" failed.");return failed==0?0:1;
 }
 private static object TemplateFixture(){return Json.Read("{\"checklistTemplateID\":2,\"checklistTemplateVersionID\":3,\"templateName\":\"Template\",\"requiredSignOffTypes\":[\"Inspector\"],\"projectFields\":[],\"sections\":[{\"fields\":[{\"fieldKey\":\"reading\",\"fieldType\":\"Number\",\"fieldLabel\":\"Reading\"}]}]}");}
 private static Fixture Ready(){var f=new Fixture();f.Login();f.Verify();return f;}
 private static void Check(bool value){if(!value)throw new Exception("Assertion failed.");}
 private static void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
 private static void Run(string name,Action test){try{SynchronizationContext.SetSynchronizationContext(null);test();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.GetType().Name+" — "+ex.Message);}}
 private sealed class Handler:HttpMessageHandler
 {
  private readonly Func<HttpRequestMessage,Task<HttpResponseMessage>> send;
  public Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> action){send=action;}
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){return send(request);}
 }
 private sealed class Fixture
 {
  public readonly string Subject=Guid.NewGuid().ToString(),UserId=Guid.NewGuid().ToString(),Factor=Guid.NewGuid().ToString();
  public DesktopSession Session;public ApiClient Api;public bool FactorVerified,Pending,RejectCode,WeakUpgrade,WeakRefresh,OtherRefreshSubject;public int EnrollCalls,DeleteCalls,PageCalls,RefreshCalls;
  public string LastUri,LastMatch,LastKey;public AuthenticationHeaderValue LastAuth;public object LastClientWrite,LastProjectWrite;public HttpStatusCode ApiStatus=HttpStatusCode.OK;public Task Delay;public Func<HttpRequestMessage,Task<HttpResponseMessage>> Override;
  public Fixture(bool verified=true)
  {
   FactorVerified=verified;Repository.Load();
   Session=new DesktopSession(new Handler(AuthRequest));DesktopSession.Current=Session;
   Api=new ApiClient(new Handler(ApiRequest),ApiConfiguration.BaseUrl);ApiClient.Shared=Api;
  }
  public void Login(){Session.SignInAsync("qa@example.invalid","synthetic-only").GetAwaiter().GetResult();}
  public void Verify(){Session.VerifyAsync("123456").GetAwaiter().GetResult();}
  public void Expire(){typeof(DesktopSession).GetField("expires",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(Session,DateTime.UtcNow.AddMinutes(-1));}
  private object Tokens(string aal,string identity=null)
  {
   identity=identity??Subject;var claims=new{ sub=identity,aal,exp=(long)(DateTime.UtcNow.AddHours(1)-new DateTime(1970,1,1)).TotalSeconds};
   var encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Write(claims))).TrimEnd('=').Replace('+','-').Replace('/','_');
   return new{access_token="synthetic."+encoded+".signature",refresh_token="synthetic-refresh",user=new{id=identity}};
  }
  private async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
  {
   var path=request.RequestUri.PathAndQuery;
   if(path.Contains("grant_type=password"))return Response(Tokens("aal1"));
   if(path.Contains("grant_type=refresh_token")){RefreshCalls++;return Response(Tokens(WeakRefresh?"aal1":"aal2",OtherRefreshSubject?Guid.NewGuid().ToString():Subject));}
   if(path.EndsWith("/user"))return Response(new{id=Subject,factors=FactorVerified?new object[]{new{id=Factor,factor_type="totp",status="verified"}}:Pending?new object[]{new{id=Factor,factor_type="totp",status="unverified",friendly_name="Mzukulu desktop setup synthetic"}}:new object[0]});
   if(path.EndsWith("/factors")&&request.Method==HttpMethod.Post){EnrollCalls++;Pending=true;return Response(new{id=Factor,totp=new{secret="SYNTHETIC_SETUP_ONLY",uri="otpauth://totp/synthetic"}});}
   if(request.Method==HttpMethod.Delete){DeleteCalls++;return Response(new{});}
   if(path.EndsWith("/challenge"))return Response(new{id=Guid.NewGuid().ToString()});
   if(path.EndsWith("/verify")){if(RejectCode)return Response(new{},HttpStatusCode.BadRequest);FactorVerified=true;return Response(Tokens(WeakUpgrade?"aal1":"aal2"));}
   await Task.CompletedTask;return Response(new{});
  }
  private async Task<HttpResponseMessage> ApiRequest(HttpRequestMessage request)
  {
   LastUri=request.RequestUri.ToString();LastAuth=request.Headers.Authorization;LastMatch=request.Headers.IfMatch.FirstOrDefault()?.Tag;IEnumerable<string> keys;LastKey=request.Headers.TryGetValues("Idempotency-Key",out keys)?keys.Single():null;
   if(Delay!=null)await Delay;
   if(ApiStatus!=HttpStatusCode.OK)return Response(new{message="SENSITIVE_INTERNAL"},ApiStatus);
   if(Override!=null)return await Override(request);
   var path=request.RequestUri.PathAndQuery;
   if(path=="/api/auth/me")return Response(new{userID=UserId,externalAuthID=Subject,firstName="Synthetic",lastName="Tester",email="qa@example.invalid",role="Admin",isActive=true});
   if(path=="/api/auth/permissions")return Response(new{permissions=new[]{"ReadClients","ReadProjects","ReadInspection","ReadTemplates"}});
   if(path=="/api/clients"&&request.Method==HttpMethod.Get)return Response(new[]{new{clientID=7,clientName="Client",contactName="Name",contactEmail="contact@example.invalid",contactPhone="123",addressLine1="Retain address",clientCode="C1",isActive=true}});
   if(path.StartsWith("/api/clients/")&&request.Method==HttpMethod.Put){LastClientWrite=Json.Read(await request.Content.ReadAsStringAsync());var result=Json.Clone(LastClientWrite);result["clientID"]=7;return Response(result);}
   if(path=="/api/projects"&&request.Method==HttpMethod.Post){LastProjectWrite=Json.Read(await request.Content.ReadAsStringAsync());var result=Json.Clone(LastProjectWrite);result["projectID"]=4;return Response(result);}
   if(path=="/api/projects")return Response(new object[0]);
   if(path.StartsWith("/api/qc-forms?")){PageCalls++;var count=PageCalls==1?100:1;return Response(Enumerable.Range(0,count).Select(i=>new{qcFormID=PageCalls*100+i,projectID=4,status="Draft"}).ToArray());}
   var response=Response(new{});response.Headers.ETag=new EntityTagHeaderValue("\"13\"");return response;
  }
  internal static HttpResponseMessage Response(object body,HttpStatusCode status=HttpStatusCode.OK){return new HttpResponseMessage(status){Content=new StringContent(Json.Write(body),Encoding.UTF8,"application/json")};}
 }
}

