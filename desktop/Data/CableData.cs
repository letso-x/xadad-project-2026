using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class CableData
 {
  private static string Route(string project){return "api/projects/"+project+"/cable-registers";}
  public static async Task LoadListAsync(string project)
  {
   var database=Repository.Db;
   var result=Json.Rows(await ApiClient.Shared.SendAsync("GET",Route(project)).ConfigureAwait(false));
   var registers=new List<CableRegister>();foreach(var row in result)registers.Add(await LoadAsync(project,Json.Text(row,"cableRegisterID")).ConfigureAwait(false));
   database.Registers=database.Registers.Where(r=>r.ProjectId!=project).Concat(registers).ToList();
  }
  public static async Task<CableRegister> LoadAsync(string project,string id)
  {
   var database=Repository.Db;
   var details=await ApiClient.Shared.SendAsync("GET",Route(project)+"/"+id).ConfigureAwait(false);var row=Json.Get(details,"register");
   var register=new CableRegister{Id=id,ProjectId=project,QcRecordNo=Json.Text(row,"registerNumber"),Revision=Json.Id(row,"revision"),RemoteRecord=Json.Map(row),Notes=Json.Text(row,"notes"),Status=Json.Get(row,"verifiedAt")!=null?RecordStatus.CompleteAccepted:RecordStatus.InProgress,CreatedAt=Json.Date(row,"createdAt")??DateTime.MinValue,UpdatedAt=Json.Date(row,"updatedAt")??DateTime.MinValue,CreatedBy=Json.Text(row,"maintainedByName"),MaintainedBy=new SignoffEntry{Name=Json.Text(row,"maintainedByName")},VerifiedBy=new SignoffEntry{Name=Json.Text(row,"verifiedByName"),Date=Json.Date(row,"verifiedAt")}};
   var fields=new[]{"Project","Project N°","Client","Contract / Order N°","Area / Location","Drawing N° & Rev.","Cable Schedule Ref. & Rev.","Register Revision"};var keys=new[]{"projectName","projectNumber","clientName","contractOrderNumber","areaLocation","drawingReference","scheduleReference","registerRevision"};
   for(int i=0;i<fields.Length;i++)register.FieldValues[fields[i]]=Json.Text(row,keys[i]);
   foreach(var cable in Json.Rows(details,"rows"))
   {
    var target=new CableRow{Id=Json.Text(cable,"cableRecordID"),Revision=Json.Id(cable,"revision"),RemoteRecord=cable,No=register.Cables.Count+1,CableNo=Json.Text(cable,"cableNumber"),From=Json.Text(cable,"fromLocation"),To=Json.Text(cable,"toLocation"),CableType=Json.Text(cable,"cableType"),DrumNo=Json.Text(cable,"drumNumber"),Length=Json.Text(cable,"length"),Pulled=Json.Bool(cable,"pulled"),Glanded=Json.Bool(cable,"glanded"),Terminated=Json.Bool(cable,"terminated"),IrCert=Json.Text(cable,"irCertificateNumber"),ContCert=Json.Text(cable,"continuityCertificateNumber"),Complete=Json.Bool(cable,"complete")};
    target.SavedInput=Json.Write(CableBody(target));register.Cables.Add(target);
   }
   register.SavedInput=Json.Write(HeaderBody(register));
   database.Registers=database.Registers.Where(r=>r.Id!=id).Concat(new[]{register}).ToList();return register;
  }
  public static async Task<CableRegister> CreateAsync(string project,string number)
  {
   var result=await ApiClient.Shared.SendAsync("POST",Route(project),new{registerNumber=number,expectedRevision=0}).ConfigureAwait(false);
   return await LoadAsync(project,Json.Text(result,"cableRegisterID")).ConfigureAwait(false);
  }
  private static object HeaderBody(CableRegister r)
  {
   return new{registerNumber=r.QcRecordNo,description=Json.Get(r.RemoteRecord,"description"),registerRevision=r.FieldValues["Register Revision"],drawingReference=r.FieldValues["Drawing N° & Rev."],scheduleReference=r.FieldValues["Cable Schedule Ref. & Rev."],areaLocation=r.FieldValues["Area / Location"],notes=r.Notes};
  }
  private static object Flag(CableRow c,string key,bool value){return Json.Bool(c.RemoteRecord,key)==value?Json.Get(c.RemoteRecord,key):value;}
  private static object CableBody(CableRow c)
  {
   decimal length;object parsed=null;if(!string.IsNullOrWhiteSpace(c.Length)){if(!decimal.TryParse(c.Length,NumberStyles.Number,CultureInfo.InvariantCulture,out length))throw new ArgumentException("Cable length must be numeric.");parsed=length;}
   return new{cableNumber=c.CableNo,fromLocation=c.From,toLocation=c.To,cableType=c.CableType,cableSize=Json.Get(c.RemoteRecord,"cableSize"),cores=Json.Get(c.RemoteRecord,"cores"),drumNumber=c.DrumNo,length=parsed,pulled=Flag(c,"pulled",c.Pulled),glanded=Flag(c,"glanded",c.Glanded),terminated=Flag(c,"terminated",c.Terminated),irCertificateNumber=c.IrCert,continuityCertificateNumber=c.ContCert,complete=Flag(c,"complete",c.Complete)};
  }
  public static async Task SaveAsync(CableRegister register)
  {
   var body=HeaderBody(register);
   if(Json.Write(body)!=register.SavedInput)
   {
    var input=Json.Clone(body);input["expectedRevision"]=register.Revision;
    await ApiClient.Shared.SendAsync("PUT",Route(register.ProjectId)+"/"+register.Id,input).ConfigureAwait(false);register.Revision++;register.SavedInput=Json.Write(body);register.RemoteRecord=Json.Clone(body);
   }
   foreach(var cable in register.Cables)
   {
    var value=CableBody(cable);if(Json.Write(value)==cable.SavedInput)continue;
    var input=Json.Clone(value);input["expectedRevision"]=cable.Revision;
    await ApiClient.Shared.SendAsync("PUT",Route(register.ProjectId)+"/"+register.Id+"/cables/"+cable.Id+"?registerRevision="+register.Revision,input).ConfigureAwait(false);
    register.Revision++;cable.Revision++;cable.SavedInput=Json.Write(value);cable.RemoteRecord=Json.Clone(value);
   }
   await LoadAsync(register.ProjectId,register.Id).ConfigureAwait(false);
  }
  public static async Task AddAsync(CableRegister register,string number)
  {
   await SaveAsync(register).ConfigureAwait(false);
   await ApiClient.Shared.SendAsync("POST",Route(register.ProjectId)+"/"+register.Id+"/cables?registerRevision="+register.Revision,new{cableNumber=number,expectedRevision=0}).ConfigureAwait(false);
   register.Revision++;
   await LoadAsync(register.ProjectId,register.Id).ConfigureAwait(false);
  }
  public static async Task VerifyAsync(CableRegister register)
  {
   await SaveAsync(register).ConfigureAwait(false);
   await ApiClient.Shared.SendAsync("POST",Route(register.ProjectId)+"/"+register.Id+"/verify?revision="+register.Revision).ConfigureAwait(false);
   await LoadAsync(register.ProjectId,register.Id).ConfigureAwait(false);
  }
 }
}
