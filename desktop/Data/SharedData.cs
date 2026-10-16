using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    internal static class SharedData
    {
        internal static Dictionary<string,Dictionary<string,object>> ClientRecords {get;set;} = new Dictionary<string,Dictionary<string,object>>();
        public static bool Loading {get;private set;}=true;
        public static string LoadStatus {get;private set;}="Loading shared data…";
        internal static void ResetViewScope(){Loading=true;LoadStatus="Loading shared data…";}
        public static async Task LoadClientsAsync()
        {
            var database=Repository.Db;var records=ClientRecords;
            var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/clients").ConfigureAwait(false));
            var clients=new List<Client>();
            foreach(var row in rows)
            {
                var id=Json.Text(row,"clientID");records[id]=row;
                clients.Add(MapClient(row));
            }
            database.Clients=clients;
        }
        private static Client MapClient(object row) {return new Client {Id=Json.Text(row,"clientID"),Name=Json.Text(row,"clientName"),Contact=string.Join(" · ",new[]{Json.Text(row,"contactName"),Json.Text(row,"contactEmail"),Json.Text(row,"contactPhone")}.Where(s=>s.Length>0)),Active=Json.Bool(row,"isActive")};}
        public static async Task SaveClientAsync(Client existing,string name,string contactName,string email,string phone,bool active)
        {
            var database=Repository.Db;var records=ClientRecords;
            var body=existing!=null && ClientRecords.ContainsKey(existing.Id)?Json.Clone(ClientRecords[existing.Id]):new Dictionary<string,object>();
            body["clientName"]=name;body["isActive"]=active;
            body["contactName"]=contactName;body["contactEmail"]=string.IsNullOrWhiteSpace(email)?null:email;body["contactPhone"]=phone;
            var saved=await ApiClient.Shared.SendAsync(existing==null?"POST":"PUT","api/clients"+(existing==null?"":"/"+existing.Id),body).ConfigureAwait(false);
            var client=MapClient(saved);if(string.IsNullOrEmpty(client.Id))throw new InvalidOperationException("The create/update response did not include the client identity. Refresh before retrying.");
            records[client.Id]=Json.Map(saved);database.Clients=database.Clients.Where(c=>c.Id!=client.Id).Concat(new[]{client}).ToList();
        }
        public static async Task LoadProjectsAsync()
        {
            var database=Repository.Db;
            var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/projects").ConfigureAwait(false));
            database.Projects=rows.Select(MapProject).ToList();
        }
        private static Project MapProject(object row){return new Project {Id=Json.Text(row,"projectID"),ClientId=Json.Text(row,"clientID"),Name=Json.Text(row,"projectName"),Number=Json.Text(row,"projectNumber"),ContractNo=Json.Text(row,"contractOrderNumber"),Site=Json.Text(row,"siteLocation"),Description=Json.Text(row,"description"),Status=Json.Text(row,"status"),StartDate=Json.Date(row,"startDate"),EndDate=Json.Date(row,"endDate")};}
        public static async Task CreateProjectAsync(Project project)
        {
            var database=Repository.Db;
            var result=await ApiClient.Shared.SendAsync("POST","api/projects",new {ClientID=long.Parse(project.ClientId,CultureInfo.InvariantCulture),ProjectName=project.Name,ProjectNumber=project.Number,ContractOrderNumber=project.ContractNo,SiteLocation=project.Site,Description=project.Description,StartDate=project.StartDate.HasValue?project.StartDate.Value.ToString("yyyy-MM-dd"):null,EndDate=project.EndDate.HasValue?project.EndDate.Value.ToString("yyyy-MM-dd"):null}).ConfigureAwait(false);
            var saved=MapProject(result);if(string.IsNullOrEmpty(saved.Id))throw new InvalidOperationException("The create response did not include the project identity. Refresh before retrying.");
            database.Projects=database.Projects.Where(p=>p.Id!=saved.Id).Concat(new[]{saved}).ToList();
        }
        public static async Task RefreshAsync()
        {
            Loading=true;LoadStatus="Loading shared data…";
            var failures=new List<string>();var generation=DesktopSession.Current.Generation;
            try
            {
                if(DesktopSession.Current.Can("ReadClients"))await TryLoadAsync("Clients",LoadClientsAsync,failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadProjects"))await TryLoadAsync("Projects",LoadProjectsAsync,failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadTemplates"))await TryLoadAsync("Templates",TemplateData.LoadAsync,failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadInspection"))foreach(var project in Repository.Db.Projects)await TryLoadAsync("QC records for "+project.Name,()=>FormData.LoadListAsync(project.Id),failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadNcr"))await TryLoadAsync("NCR",NcrData.LoadAsync,failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadInspection"))foreach(var project in Repository.Db.Projects)await TryLoadAsync("Cable registers for "+project.Name,()=>CableData.LoadListAsync(project.Id),failures,generation).ConfigureAwait(false);
                if(DesktopSession.Current.Can("ReadReports"))await TryLoadAsync("Dashboard",ReportingData.LoadPortfolioAsync,failures,generation).ConfigureAwait(false);
                LoadStatus=failures.Count==0?"Loaded from the shared service.":string.Join("\n",failures);
            }
            finally{Loading=false;}
        }
        private static async Task TryLoadAsync(string module,Func<Task> load,List<string> failures,int generation)
        {
            if(generation!=DesktopSession.Current.Generation)throw new InvalidOperationException("The session changed while data was loading.");
            try{await load().ConfigureAwait(false);}catch(Exception ex){if(generation!=DesktopSession.Current.Generation)throw;failures.Add(module+": "+(ex is System.Net.Http.HttpRequestException?"Service connection failed.":ex.Message));}
        }
    }
}
