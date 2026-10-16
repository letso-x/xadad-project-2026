using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    // Memory-only sessions: no passwords, setup keys or tokens in the JSON store.
    internal sealed class DesktopSession
    {
        internal static DesktopSession Current { get; set; } = new DesktopSession(new HttpClientHandler { AllowAutoRedirect = false });
        private readonly HttpClient auth;
        private int generation;
        internal int Generation { get { return Volatile.Read(ref generation); } }
        internal DesktopSession(HttpMessageHandler handler) { auth=new HttpClient(handler) {BaseAddress=new Uri(ApiConfiguration.AuthUrl),Timeout=TimeSpan.FromSeconds(90)}; }
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly object stateGate = new object();
        private string access, refresh, subject, assurance;
        private DateTime expires;
        private string pendingFactor;
        public string SetupKey { get; private set; }
        public string SetupUri { get; private set; }
        public string FactorId { get; private set; }
        public bool Verified { get; private set; }
        public HashSet<string> Permissions { get; private set; } = new HashSet<string>(StringComparer.Ordinal);
        public bool Can(string permission) { return Verified && Permissions.Contains(permission); }
        public async Task SignInAsync(string email, string password)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                Clear();
                var operationGeneration=Generation;
                ReadTokens(await SendAuthAsync("POST", "token?grant_type=password", new { email, password }, false).ConfigureAwait(false),operationGeneration);
                await FactorsAsync().ConfigureAwait(false);
                var me = await ApiClient.Shared.SendTokenAsync("GET", "api/auth/me", access,sessionGeneration:operationGeneration).ConfigureAwait(false);
                lock(stateGate)
                {
                    CheckGeneration(operationGeneration);
                    Auth.SetCurrent(new UserAccount { Id = Json.Text(me,"userID"), Username = Json.Text(me,"email"), Email = Json.Text(me,"email"), FullName = (Json.Text(me,"firstName")+" "+Json.Text(me,"lastName")).Trim(), Active = Json.Bool(me,"isActive"), Role = Json.Text(me,"role") == "Admin" ? UserRole.Admin : UserRole.SiteUser });
                }
            }
            catch { Clear(); Auth.SetCurrent(null); throw; }
            finally { gate.Release(); }
        }
        private async Task FactorsAsync()
        {
            var user = await SendAuthAsync("GET", "user").ConfigureAwait(false);
            if (Json.Text(user,"id") != subject) throw new InvalidOperationException("Your session changed. Sign in again.");
            var factors = Json.Rows(user,"factors");
            var verified = factors.FirstOrDefault(f=>Json.Text(f,"factor_type")=="totp" && Json.Text(f,"status")=="verified");
            FactorId = verified == null ? null : Json.Text(verified,"id");
            // Only this desktop flow's unfinished factors may be cleaned up.
            pending = factors.Where(f=>Json.Text(f,"factor_type")=="totp" && Json.Text(f,"status")=="unverified" && Json.Text(f,"friendly_name").StartsWith("Mzukulu desktop setup ",StringComparison.Ordinal)).Select(f=>Json.Text(f,"id")).ToList();
            if (FactorId == null && factors.Any(f=>Json.Text(f,"status")=="verified")) throw new InvalidOperationException("Your account uses another verification method. Contact your administrator.");
        }
        private List<string> pending = new List<string>();
        public async Task EnrollAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await FactorsAsync().ConfigureAwait(false);
                if (FactorId != null) return;
                if (pendingFactor != null && SetupKey != null && pending.Contains(pendingFactor)) { FactorId = pendingFactor; return; }
                if (pending.Count > 0 && assurance != "aal1") throw new InvalidOperationException("An unfinished setup needs administrator assistance. Your verified factors are unchanged.");
                foreach (var id in pending) await SendAuthAsync("DELETE", "factors/"+Guid.Parse(id)).ConfigureAwait(false);
                var result = await SendAuthAsync("POST", "factors", new { factor_type="totp", friendly_name="Mzukulu desktop setup "+Guid.NewGuid().ToString("N").Substring(0,8) }).ConfigureAwait(false);
                FactorId = pendingFactor = Guid.Parse(Json.Text(result,"id")).ToString();
                SetupKey = Json.Text(Json.Get(result,"totp"),"secret");
                SetupUri = Json.Text(Json.Get(result,"totp"),"uri");
                if (string.IsNullOrWhiteSpace(SetupKey)) throw new InvalidOperationException("Authenticator setup is unavailable. Please retry.");
            }
            finally { gate.Release(); }
        }
        public async Task VerifyAsync(string code)
        {
            if (code == null || code.Length != 6 || code.Any(c=>c<'0'||c>'9')) throw new ArgumentException("Enter the six-digit code from your authenticator app.");
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var operationGeneration=Generation;
                var factor = Guid.Parse(FactorId).ToString();
                var challenge = await SendAuthAsync("POST", "factors/"+factor+"/challenge", new {}).ConfigureAwait(false);
                var previousSubject = subject;
                ReadTokens(await SendAuthAsync("POST", "factors/"+factor+"/verify", new { challenge_id=Json.Text(challenge,"id"), code }).ConfigureAwait(false),operationGeneration);
                if (subject != previousSubject || assurance != "aal2") throw new InvalidOperationException("Two-step verification did not complete. Sign in again.");
                await FactorsAsync().ConfigureAwait(false);
                if (FactorId == null) throw new InvalidOperationException("A verified authenticator is required.");
                var permissions = await ApiClient.Shared.SendTokenAsync("GET", "api/auth/permissions", access,sessionGeneration:operationGeneration).ConfigureAwait(false);
                var values = Json.Get(permissions,"permissions") as object[] ?? new object[0];
                lock(stateGate)
                {
                    CheckGeneration(operationGeneration);
                    if(Auth.Current==null)throw new InvalidOperationException("Sign in again to continue.");
                    Permissions = new HashSet<string>(values.Select(Convert.ToString), StringComparer.Ordinal);
                    Verified = true; SetupKey = SetupUri = null;
                    Repository.SignIn(Auth.Current.Role, Auth.Current.FullName);
                }
            }
            finally { gate.Release(); }
        }
        public async Task<string> ProtectedTokenAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var operationGeneration=Generation;
                if (!Verified || assurance != "aal2") throw new InvalidOperationException("Sign in and complete two-step verification to continue.");
                if (expires <= DateTime.UtcNow.AddMinutes(1))
                {
                    var original = subject;
                    ReadTokens(await SendAuthAsync("POST","token?grant_type=refresh_token",new { refresh_token=refresh },false).ConfigureAwait(false),operationGeneration);
                    if (subject!=original || assurance!="aal2") { Clear(); throw new InvalidOperationException("Sign in again to continue."); }
                    await FactorsAsync().ConfigureAwait(false);
                    if (FactorId == null) { Clear(); throw new InvalidOperationException("Complete two-step verification again."); }
                }
                lock(stateGate){CheckGeneration(operationGeneration);return access;}
            }
            finally { gate.Release(); }
        }
        public async Task ChangePasswordAsync(string password) { await ProtectedTokenAsync().ConfigureAwait(false); await SendAuthAsync("PUT","user",new { password }).ConfigureAwait(false); }
        public async Task SignOutAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try { if (access!=null) await SendAuthAsync("POST","logout?scope=local",new {}).ConfigureAwait(false); }
            finally { Clear(); gate.Release(); }
        }
        internal Task DetachAndSignOutAsync()
        {
            var token=access;
            Clear();
            return RevokeAsync(token);
        }
        private async Task RevokeAsync(string token)
        {
            if(token==null)return;
            try
            {
                using(var request=new HttpRequestMessage(HttpMethod.Post,"logout?scope=local"))
                {
                    request.Headers.Add("apikey",ApiConfiguration.PublicKey);
                    request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
                    using(var response=await auth.SendAsync(request).ConfigureAwait(false)) { }
                }
            }
            catch(HttpRequestException) { }
            catch(TaskCanceledException) { }
        }
        public void Clear() { lock(stateGate){Interlocked.Increment(ref generation);access=refresh=subject=assurance=pendingFactor=SetupKey=SetupUri=FactorId=null; Verified=false; Permissions=new HashSet<string>(StringComparer.Ordinal);pending=new List<string>();Auth.SetCurrent(null);} }
        private void CheckGeneration(int expected){if(expected!=Generation)throw new InvalidOperationException("The session changed. Sign in again.");}
        private void ReadTokens(object result,int expectedGeneration)
        {
            var token = Json.Text(result,"access_token"); var encoded = token.Split('.')[1].Replace('-','+').Replace('_','/');
            var claims = Json.Read(Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'='))));
            var id = Guid.Parse(Json.Text(claims,"sub")).ToString();
            lock(stateGate)
            {
                CheckGeneration(expectedGeneration);
                if (!string.IsNullOrEmpty(subject) && subject!=id) throw new InvalidOperationException("Your session changed. Sign in again.");
                access=token; refresh=Json.Text(result,"refresh_token"); subject=id; assurance=Json.Text(claims,"aal");
                expires=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Json.Id(claims,"exp"));
                if (string.IsNullOrEmpty(refresh)) throw new InvalidOperationException("The sign-in session is incomplete.");
            }
        }
        private async Task<object> SendAuthAsync(string method,string route,object body=null,bool authenticated=true)
        {
            var generationAtRequest=Generation;
            using (var request = new HttpRequestMessage(new HttpMethod(method),route))
            {
                request.Headers.Add("apikey",ApiConfiguration.PublicKey);
                if (authenticated) { if (access==null) throw new InvalidOperationException("Sign in first."); request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access); }
                if (body!=null) request.Content=new StringContent(Json.Write(body),Encoding.UTF8,"application/json");
                using(var response=await auth.SendAsync(request).ConfigureAwait(false))
                {
                    var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if(generationAtRequest!=Generation)throw new InvalidOperationException("Sign-in was cancelled. Please sign in again.");
                    if(!response.IsSuccessStatusCode) throw new InvalidOperationException((int)response.StatusCode==429?"Too many attempts. Wait before trying again.":route.Contains("verify")?"The authenticator code could not be verified. Try a current code.":"Account verification failed. Check your credentials and connection.");
                    return Json.Read(text);
                }
            }
        }
    }
}
