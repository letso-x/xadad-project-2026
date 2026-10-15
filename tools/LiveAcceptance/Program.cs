using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using MzukuluQMS.Mobile.Core;
var config=new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
var email=Environment.GetEnvironmentVariable("QMS_TEST_EMAIL")??throw new InvalidOperationException("QMS_TEST_EMAIL required");
var password=Environment.GetEnvironmentVariable("QMS_TEST_PASSWORD")??throw new InvalidOperationException("QMS_TEST_PASSWORD required");
var apiKey=Environment.GetEnvironmentVariable("QMS_TEST_PUBLIC_KEY")??throw new InvalidOperationException("QMS_TEST_PUBLIC_KEY required");
var apiBase=Environment.GetEnvironmentVariable("QMS_TEST_API")??"https://localhost:7045/";
var log=new List<object>();var evidenceDir="docs/codex/evidence";Directory.CreateDirectory(evidenceDir);
var run=Guid.NewGuid().ToString("N")[..10];var state=new Dictionary<string,object>{{"run",run}};
using var api=new HttpClient{BaseAddress=new Uri(apiBase),Timeout=TimeSpan.FromSeconds(60)};
await using var db=new NpgsqlConnection(config.GetConnectionString("QmsDatabase"));
try {
await db.OpenAsync();var originals=await Historical(db);await File.WriteAllTextAsync($"{evidenceDir}/historical-rows-{run}.json",JsonSerializer.Serialize(originals));
await Expect("anonymous identity denied",HttpMethod.Get,"api/auth/me",null,401);
using var auth=new HttpClient();auth.DefaultRequestHeaders.Add("apikey",apiKey);
var metadata=new Uri(config["SupabaseAuth:MetadataAddress"]!);var authUrl=metadata.GetLeftPart(UriPartial.Authority)+"/auth/v1/token?grant_type=password";
using var login=await auth.PostAsync(authUrl,new StringContent(JsonSerializer.Serialize(new{email,password}),Encoding.UTF8,"application/json"));
if(!login.IsSuccessStatusCode)throw new InvalidOperationException("Supabase login status "+(int)login.StatusCode);
var loginJson=JsonNode.Parse(await login.Content.ReadAsStringAsync())!;api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",loginJson["access_token"]!.GetValue<string>());
var me=await Expect("real linked identity",HttpMethod.Get,"api/auth/me",null,200);var user=Guid.Parse(me!["userID"]!.GetValue<string>());var role=me["role"]!.GetValue<string>();state["role"]=role;
if(args.Contains("--verify-download")) {
state["mode"]="private evidence download";
var saved=JsonNode.Parse(await File.ReadAllTextAsync("docs/codex/evidence/live-acceptance-107c7862a2.json"))!["state"]!;var ownForm=L(saved,"formID");var evidence=L(saved,"photoEvidenceID");state["formID"]=ownForm;state["evidenceID"]=evidence;
using(var response=await api.GetAsync($"api/qc-forms/{ownForm}/evidence/{evidence}/content")){Check("authorised evidence content HTTP200",(int)response.StatusCode==200);var ink=new SignatureInk();ink.Begin(.1f,.6f);ink.Move(.3f,.2f);ink.Move(.5f,.7f);ink.Move(.8f,.3f);Check("private storage bytes round trip",(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(ink.ToPng()));Check("private evidence no-store",response.Headers.CacheControl?.NoStore==true);Check("safe attachment disposition",response.Content.Headers.ContentDisposition?.DispositionType=="attachment");Check("image MIME retained",response.Content.Headers.ContentType?.MediaType=="image/png");Check("nosniff header",response.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff");}
using(var anonymous=new HttpClient{BaseAddress=api.BaseAddress})using(var denied=await anonymous.GetAsync($"api/qc-forms/{ownForm}/evidence/{evidence}/content"))Check("anonymous content denied",(int)denied.StatusCode==401);
await Expect("cross-form evidence denied",HttpMethod.Get,$"api/qc-forms/11/evidence/{evidence}/content",null,404);await Expect("other project evidence denied",HttpMethod.Get,$"api/qc-forms/8/evidence/{evidence}/content",null,403);
var location=await db.QuerySingleAsync<StorageLocation>("SELECT \"StorageBucket\" AS \"Bucket\",\"StoragePath\" AS \"Path\" FROM public.\"Evidence\" WHERE \"EvidenceID\"=@evidence",new{evidence});using(var publicClient=new HttpClient())using(var denied=await publicClient.GetAsync(config["SupabaseStorage:BaseUrl"]!.TrimEnd('/')+"/storage/v1/object/public/"+Uri.EscapeDataString(location.Bucket)+"/"+string.Join("/",location.Path.Split('/').Select(Uri.EscapeDataString))))Check("public bucket URL cannot read private evidence",!denied.IsSuccessStatusCode);
var afterDownload=await Historical(db);foreach(var table in originals)foreach(var row in table.Value)if(afterDownload[table.Key].GetValueOrDefault(row.Key)!=row.Value)throw new InvalidOperationException("Historical row changed during evidence read");Record("all public rows unchanged by downloads",true);state["downloadComplete"]=true;state["complete"]=true;return;
}
var permissions=new[]{"ReadProjects","ManageProjects","ReadClients","ReadTemplates","ManageTemplates","ReadInspection","PerformInspection","SubmitInspection","ReviewInspection","ApproveInspection","SignInspection","SignInspection:Inspector","ReadAudit","ReadNcr","CorrectNcr","VerifyNcr","ReadReports"};
var grants=await db.ExecuteScalarAsync<int>("SELECT count(*)::integer FROM qms_security.\"RolePermission\" WHERE \"RoleName\"=@role",new{role});
if(grants==0)await Expect("default-denied resource permission",HttpMethod.Get,"api/clients",null,403);
await using(var tx=await db.BeginTransactionAsync()){foreach(var permission in permissions)await db.ExecuteAsync("INSERT INTO qms_security.\"RolePermission\" (\"RoleName\",\"Permission\") VALUES(@role,@permission) ON CONFLICT DO NOTHING",new{role,permission},tx);await db.ExecuteAsync("INSERT INTO qms_security.\"AuditEvent\" (\"ActorUserID\",\"Action\",\"EntityType\",\"EntityID\",\"Details\") VALUES(@user,'SyntheticTestPermissionBootstrap','TestPolicy',0,jsonb_build_object('run',CAST(@run AS text),'role',CAST(@role AS text)))",new{user,run,role},tx);await tx.CommitAsync();}
Record("explicit disposable test-role permissions configured",true);
await Expect("authorised client list",HttpMethod.Get,"api/clients",null,200);
await Expect("authorised project list",HttpMethod.Get,"api/projects",null,200);
await Expect("active template library",HttpMethod.Get,"api/checklist-templates",null,200);
await Expect("scoped dashboard",HttpMethod.Get,"api/quality/dashboard",null,200);
var client=await Expect("create synthetic client",HttpMethod.Post,"api/clients",new{clientName="SYNTHETIC ACCEPTANCE "+run,clientCode="QA-"+run,isActive=true},201);var clientId=L(client!,"clientID");state["clientID"]=clientId;
Check("client create audited in existing stream",await db.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM public.\"AuditEvent\" WHERE \"EntityType\"='Client' AND \"EntityID\"=@id AND \"Action\"='ClientCreated')",new{id=clientId.ToString(System.Globalization.CultureInfo.InvariantCulture)}));
await Expect("update synthetic client",HttpMethod.Put,$"api/clients/{clientId}",new{clientName="SYNTHETIC ACCEPTANCE UPDATED "+run,clientCode="QA-"+run,isActive=true},200);
Check("client update audited with authenticated actor",await db.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM public.\"AuditEvent\" WHERE \"EntityType\"='Client' AND \"EntityID\"=@id AND \"Action\"='ClientUpdated' AND \"UserID\"=@user)",new{id=clientId.ToString(System.Globalization.CultureInfo.InvariantCulture),user}));
var project=await Expect("create synthetic project",HttpMethod.Post,"api/projects",new{clientID=clientId,projectName="SYNTHETIC correction lifecycle "+run,projectNumber="QA-"+run,siteLocation="Windows test",contractOrderNumber="TEST ONLY"},201);var projectId=L(project!,"projectID");state["projectID"]=projectId;
await Expect("historical project access denied",HttpMethod.Get,"api/qc-forms/8",null,403);
object Field(string key,string type,object? options=null)=>new{key,label=key,type,required=true,requiresPhoto=!args.Contains("--nonstorage")&&key=="equipment_number",requiresSignature=false,options};
var template=await Expect("publish two-field synthetic template",HttpMethod.Post,"api/checklist-templates",new{documentNumber="QA-"+run,documentName="SYNTHETIC Equipment Inspection",revision="1",issueDate="2026-10-14",active=true,scope="Test only",acceptanceCriteria="Both fields completed and fresh Inspector signoff",projectFields=Array.Empty<object>(),sections=new[]{new{title="Inspection Details",required=true,fields=new[]{Field("equipment_number","Text"),Field("inspection_result","Dropdown",new[]{"Pass","Fail","N/A"})}}},requiredSignOffTypes=new[]{"Inspector"}},200,201);
var templateId=L(template!,"checklistTemplateID");var versionId=L(template!,"checklistTemplateVersionID");state["templateID"]=templateId;state["versionID"]=versionId;
var createKey=Guid.NewGuid();var createBody=new{projectID=projectId,checklistTemplateID=templateId,checklistTemplateVersionID=versionId};
var concurrentCreates=await Task.WhenAll(Expect("concurrent snapshot create A",HttpMethod.Post,"api/qc-forms",createBody,201,key:createKey),Expect("concurrent snapshot create B",HttpMethod.Post,"api/qc-forms",createBody,201,key:createKey));
var form=concurrentCreates[0];Check("concurrent create returns one form",L(concurrentCreates[0]!,"qcFormID")==L(concurrentCreates[1]!,"qcFormID"));var id=L(form!,"qcFormID");state["formID"]=id;
var replay=await Expect("create idempotent replay",HttpMethod.Post,"api/qc-forms",createBody,201,key:createKey);Check("replay same form",L(replay!,"qcFormID")==id);
var current=await GetForm(id);Check("two-field snapshot",current["sections"]!.AsArray().Sum(s=>s!["fields"]!.AsArray().Count)==2);Check("immutable header captured",current["documentSnapshot"] is not null);
var fields=current["sections"]![0]!["fields"]!.AsArray();var equipment=L(fields[0]!,"qcResponseID");var outcome=L(fields[1]!,"qcResponseID");var equipmentFieldId=L(fields[0]!,"qcFormFieldID");
await Expect("approval denied in Draft",HttpMethod.Post,$"api/qc-forms/{id}/approve",null,409,revision:Rev(current));
await Expect("submit missing required fields denied",HttpMethod.Post,$"api/qc-forms/{id}/submit",null,409,revision:Rev(current));
var auditGuard="qa_audit_"+run;
await using(var tx=await db.BeginTransactionAsync()) {
await db.ExecuteAsync($"CREATE FUNCTION qms_security.{auditGuard}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"EntityID\"='{id}' AND NEW.\"EntityType\"='QCForm' THEN RAISE EXCEPTION 'Synthetic injected audit failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER {auditGuard} BEFORE INSERT ON public.\"AuditEvent\" FOR EACH ROW EXECUTE FUNCTION qms_security.{auditGuard}();",transaction:tx);await tx.CommitAsync();}
try {await Expect("audit failure rejects business write",HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}",new{value="MUST ROLL BACK"},500,revision:Rev(current));}
finally {await db.ExecuteAsync($"DROP TRIGGER IF EXISTS {auditGuard} ON public.\"AuditEvent\"; DROP FUNCTION IF EXISTS qms_security.{auditGuard}();");}
var rolledBack=await GetForm(id);Check("audit failure preserves revision",Rev(rolledBack)==Rev(current));Check("audit failure preserves response",rolledBack["sections"]![0]!["fields"]![0]!["value"] is null);
var raceRevision=Rev(current);
async Task<int> RaceWrite(string value){using var request=new HttpRequestMessage(HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}"){Content=new StringContent(JsonSerializer.Serialize(new{value}),Encoding.UTF8,"application/json")};request.Headers.TryAddWithoutValidation("If-Match","\""+raceRevision+"\"");using var response=await api.SendAsync(request);return (int)response.StatusCode;}
var raced=await Task.WhenAll(RaceWrite("RACE A"),RaceWrite("RACE B"));Check("competing writes accept one and reject one",raced.Order().SequenceEqual(new[]{204,409}));current=await GetForm(id);
await Expect("equipment response",HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}",new{value="QA-EQUIPMENT-1"},204,revision:Rev(current));current=await GetForm(id);
await Expect("stale revision denied",HttpMethod.Put,$"api/qc-forms/{id}/responses/{outcome}",new{value="Pass"},409,revision:0);
await Expect("outcome response",HttpMethod.Put,$"api/qc-forms/{id}/responses/{outcome}",new{value="Pass"},204,revision:Rev(current));current=await GetForm(id);
if(!args.Contains("--nonstorage")){await Expect("missing required photo denied",HttpMethod.Post,$"api/qc-forms/{id}/submit",null,409,revision:Rev(current));var photoEvidence=await Upload(id,Rev(current),"Photo",equipmentFieldId);state["photoEvidenceID"]=photoEvidence;current=await GetForm(id);}
await Expect("submit",HttpMethod.Post,$"api/qc-forms/{id}/submit",null,204,revision:Rev(current));current=await GetForm(id);
await Expect("submitted edits denied",HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}",new{value="ILLEGAL"},409,revision:Rev(current));
await Expect("approval without signatures denied",HttpMethod.Post,$"api/qc-forms/{id}/approve",null,409,revision:Rev(current));
if(args.Contains("--nonstorage")) {
await Expect("return without storage/signature dependency",HttpMethod.Post,$"api/qc-forms/{id}/return-for-correction",new{reason="Synthetic non-storage NCR verification"},204,revision:Rev(current));current=await GetForm(id);
await Expect("Fail outcome creates NCR",HttpMethod.Put,$"api/qc-forms/{id}/responses/{outcome}",new{value="Fail"},204,revision:Rev(current));current=await GetForm(id);
var ncrs=await Expect("read source NCR",HttpMethod.Get,$"api/qc-forms/{id}/ncrs",null,200);Check("one open source NCR",ncrs!.AsArray().Count==1);var ncr=ncrs[0]!;var ncrId=L(ncr,"ncrID");state["ncrID"]=ncrId;Check("existing public NCR bridged",ncr["publicNCRID"] is not null);
await Expect("repeated Fail deduplicated",HttpMethod.Put,$"api/qc-forms/{id}/responses/{outcome}",new{value="Fail"},204,revision:Rev(current));current=await GetForm(id);
var repeated=await Expect("read deduplicated NCR",HttpMethod.Get,$"api/qc-forms/{id}/ncrs",null,200);Check("one NCR after repeated Fail",repeated!.AsArray().Count==1);
await Expect("later Pass",HttpMethod.Put,$"api/qc-forms/{id}/responses/{outcome}",new{value="Pass"},204,revision:Rev(current));current=await GetForm(id);
var retained=await Expect("NCR retained after Pass",HttpMethod.Get,$"api/qc-forms/{id}/ncrs",null,200);Check("Pass does not erase NCR",retained!.AsArray().Count==1&&retained[0]!["closedAt"] is null);
await Expect("resubmit with open NCR",HttpMethod.Post,$"api/qc-forms/{id}/submit",null,204,revision:Rev(current));current=await GetForm(id);
var blocked=await Expect("unclassified NCR blocks approval",HttpMethod.Post,$"api/qc-forms/{id}/approve",null,409,revision:Rev(current));Check("NCR rejection reason",blocked!["detail"]!.GetValue<string>().Contains("NCR",StringComparison.Ordinal));
await Expect("incomplete NCR verification denied",HttpMethod.Post,$"api/qc-forms/{id}/ncrs/{ncrId}/verify",new{passed=true,comments="Synthetic attempt",expectedRevision=0},409,revision:Rev(current));
var ncrAudit=await Expect("NCR public audit",HttpMethod.Get,$"api/qc-forms/{id}/audit-history",null,200);Check("NCR audit populated",ncrAudit!.AsArray().Count>=8);
var afterNonstorage=await Historical(db);foreach(var table in originals)foreach(var row in table.Value)if(afterNonstorage[table.Key].GetValueOrDefault(row.Key)!=row.Value)throw new InvalidOperationException("Historical row changed: "+table.Key+"/"+row.Key);Record("all pre-existing public rows preserved",true);
state["nonstorageComplete"]=true;state["complete"]=false;state["remaining"]="Real storage/signature/corrected approval path not run in nonstorage mode";return;
}
var firstEvidence=await Upload(id,Rev(current));current=await GetForm(id);
var firstSignoff=await Expect("first authenticated signoff",HttpMethod.Post,$"api/qc-forms/{id}/sign-offs",new{signOffType="Inspector",signatureEvidenceID=firstEvidence,comments="Synthetic consent, initial submission",userID=Guid.NewGuid()},201,revision:Rev(current));current=await GetForm(id);
await Expect("ungranted signing capacity denied",HttpMethod.Post,$"api/qc-forms/{id}/sign-offs",new{signOffType="Client",signatureEvidenceID=firstEvidence},403,revision:Rev(current));
Check("signoff actor resolved from authenticated session",Guid.Parse(firstSignoff!["userID"]!.GetValue<string>())==user);
var firstHistory=await Expect("initial signature history",HttpMethod.Get,$"api/qc-forms/{id}/sign-offs",null,200);Check("initial signature persisted",firstHistory!.AsArray().Count==1);
await Expect("return reason required",HttpMethod.Post,$"api/qc-forms/{id}/return-for-correction",new{reason=""},400,revision:Rev(current));
await Expect("return for correction",HttpMethod.Post,$"api/qc-forms/{id}/return-for-correction",new{reason="Synthetic reviewer requests equipment correction"},204,revision:Rev(current));current=await GetForm(id);Check("ReturnedForCorrection state",current["status"]!.GetValue<string>()=="ReturnedForCorrection");
await Expect("corrected response",HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}",new{value="QA-EQUIPMENT-2"},204,revision:Rev(current));current=await GetForm(id);
await Expect("resubmit",HttpMethod.Post,$"api/qc-forms/{id}/submit",null,204,revision:Rev(current));current=await GetForm(id);
await Expect("old signature cannot approve corrected form",HttpMethod.Post,$"api/qc-forms/{id}/approve",null,409,revision:Rev(current));
await Expect("stale signature evidence rejected",HttpMethod.Post,$"api/qc-forms/{id}/sign-offs",new{signOffType="Inspector",signatureEvidenceID=firstEvidence},400,revision:Rev(current));
var secondEvidence=await Upload(id,Rev(current));current=await GetForm(id);
await Expect("fresh corrected signoff",HttpMethod.Post,$"api/qc-forms/{id}/sign-offs",new{signOffType="Inspector",signatureEvidenceID=secondEvidence,comments="Synthetic fresh consent after correction"},201,revision:Rev(current));current=await GetForm(id);
await Expect("approve current signatures",HttpMethod.Post,$"api/qc-forms/{id}/approve",null,204,revision:Rev(current));current=await GetForm(id);Check("Approved state",current["status"]!.GetValue<string>()=="Approved");
await Expect("approved edits denied",HttpMethod.Put,$"api/qc-forms/{id}/responses/{equipment}",new{value="ILLEGAL"},409,revision:Rev(current));
var finalHistory=await Expect("preserved signoff history",HttpMethod.Get,$"api/qc-forms/{id}/sign-offs",null,200);Check("both historical signatures retained",finalHistory!.AsArray().Count==2);
var audit=await Expect("existing public audit history",HttpMethod.Get,$"api/qc-forms/{id}/audit-history",null,200);Check("public audit bridge populated",audit!.AsArray().Count>=10);

var after=await Historical(db);foreach(var table in originals)foreach(var row in table.Value)if(after[table.Key].GetValueOrDefault(row.Key)!=row.Value)throw new InvalidOperationException("Historical row changed: "+table.Key+"/"+row.Key);Record("all pre-existing public rows preserved",true);
Record("signed correction lifecycle API complete",true);state["complete"]=true;
} catch(Exception e){if(e is PostgresException pg){state["sqlState"]=pg.SqlState;state["sqlPosition"]=pg.Position;state["sqlConstraint"]=pg.ConstraintName??"not supplied";}state["complete"]=false;state["failureType"]=e.GetType().Name;state["failure"]=e is InvalidOperationException?e.Message:"See local server diagnostics; secrets excluded";Console.WriteLine("FAILED: "+state["failure"]);Environment.ExitCode=2;}
finally {await File.WriteAllTextAsync($"{evidenceDir}/live-acceptance-{run}.json",JsonSerializer.Serialize(new{state,checks=log},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Evidence run: "+run);}
void Record(string check,bool pass){log.Add(new{check,pass});Console.WriteLine((pass?"PASS ":"FAIL ")+check);}
void Check(string check,bool value){Record(check,value);if(!value)throw new InvalidOperationException(check);}
long L(JsonNode node,string key)=>node[key]!.GetValue<long>();long Rev(JsonNode node)=>L(node,"revision");
async Task<JsonNode> GetForm(long id)=>(await Expect("read execution",HttpMethod.Get,$"api/qc-forms/{id}",null,200))!;
async Task<JsonNode?> Expect(string check,HttpMethod method,string url,object? body,int expected,int? alternative=null,long? revision=null,Guid? key=null){
using var request=new HttpRequestMessage(method,url);if(body is not null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");if(revision.HasValue)request.Headers.TryAddWithoutValidation("If-Match","\""+revision+"\"");if(key.HasValue)request.Headers.Add("Idempotency-Key",key.Value.ToString());
using var response=await api.SendAsync(request);var status=(int)response.StatusCode;Record(check+$" HTTP {status}",status==expected||status==alternative);if(status!=expected&&status!=alternative)throw new InvalidOperationException(check+$" expected {expected}, got {status}");var text=await response.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(text)?null:JsonNode.Parse(text);
}
async Task<long> Upload(long id,long revision,string type="Signature",long? fieldId=null){var ink=new SignatureInk();ink.Begin(.1f,.6f);ink.Move(.3f,.2f);ink.Move(.5f,.7f);ink.Move(.8f,.3f);using var content=new MultipartFormDataContent();content.Add(new StringContent(type),"EvidenceType");if(fieldId.HasValue)content.Add(new StringContent(fieldId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),"QCFormFieldID");var file=new ByteArrayContent(ink.ToPng());file.Headers.ContentType=new MediaTypeHeaderValue("image/png");content.Add(file,"File","synthetic-signature.png");using var request=new HttpRequestMessage(HttpMethod.Post,$"api/qc-forms/{id}/evidence"){Content=content};request.Headers.TryAddWithoutValidation("If-Match","\""+revision+"\"");using var response=await api.SendAsync(request);Record("private "+type+" upload HTTP "+(int)response.StatusCode,response.StatusCode==System.Net.HttpStatusCode.Created);if(response.StatusCode!=System.Net.HttpStatusCode.Created)throw new InvalidOperationException(type+" upload failed, HTTP "+(int)response.StatusCode);var json=JsonNode.Parse(await response.Content.ReadAsStringAsync())!;return L(json,"evidenceID");}
static async Task<Dictionary<string,Dictionary<string,string>>> Historical(NpgsqlConnection db){
var tables=(await db.QueryAsync<string>("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename")).ToArray();var result=new Dictionary<string,Dictionary<string,string>>();foreach(var table in tables){var key=await db.ExecuteScalarAsync<string>("SELECT a.attname FROM pg_index i JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=ANY(i.indkey) WHERE i.indrelid=CAST(@name AS regclass) AND i.indisprimary ORDER BY a.attnum LIMIT 1",new{name="public.\""+table+"\""});if(key is null)throw new InvalidOperationException("No primary key for preservation baseline");var sql=$"SELECT \"{key}\"::text AS \"Key\",md5((to_jsonb(t)-'Revision'-'HashVersion'-'CoreAuditEventID')::text) AS \"Digest\" FROM public.\"{table}\" t";var rows=await db.QueryAsync<RowDigest>(sql);result[table]=rows.ToDictionary(r=>r.Key,r=>r.Digest);}return result;
}
record RowDigest(string Key,string Digest);

record StorageLocation(string Bucket,string Path);
