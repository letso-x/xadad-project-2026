using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MzuApplication.Data
{
    // Public configuration only. Server credentials never belong in this application.
    internal static class ApiConfiguration
    {
        public const string BaseUrl = "https://xadad-project-2026.onrender.com/";
        public const string AuthUrl = "https://kfrknawmyewdambgaekl.supabase.co/auth/v1/";
        public const string PublicKey = "sb_publishable_dCTZ_BXeLKC2hI3B4ZpE-w_p7hzJllY";
    }

    internal sealed class ApiException : Exception
    {
        public int StatusCode { get; private set; }
        public bool IsRevisionConflict { get { return StatusCode==412 || Message.StartsWith("The central record changed.",StringComparison.Ordinal); } }
        public ApiException(int status, string message) : base(message) { StatusCode = status; }
    }

    // Boundary JSON adapter: preserves fields unknown to legacy desktop models.
    internal static class Json
    {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 100 }; }
        public static string Write(object value) { return Serializer().Serialize(value); }
        public static object Read(string text) { return string.IsNullOrWhiteSpace(text) ? new Dictionary<string, object>() : Serializer().DeserializeObject(text); }
        public static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        public static object Get(object value, string key)
        {
            foreach (var pair in Map(value)) if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return null;
        }
        public static string Text(object value, string key) { return Convert.ToString(Get(value, key), CultureInfo.InvariantCulture) ?? ""; }
        public static long Id(object value, string key) { long id; return long.TryParse(Text(value, key), out id) ? id : 0; }
        public static bool Bool(object value, string key) { bool result; return bool.TryParse(Text(value, key), out result) && result; }
        public static DateTime? Date(object value, string key)
        {
            object raw = Get(value, key); if (raw is DateTime) return (DateTime)raw;
            DateTime result; return DateTime.TryParse(Text(value,key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result) ? result : (DateTime?)null;
        }
        public static List<Dictionary<string, object>> Rows(object value)
        {
            var rows = new List<Dictionary<string, object>>();
            var array = value as IEnumerable;
            if (array != null && !(value is string) && !(value is IDictionary)) foreach (var item in array) rows.Add(Map(item));
            return rows;
        }
        public static List<Dictionary<string, object>> Rows(object value, string key) { return Rows(Get(value, key)); }
        public static object[] Array(object value) { var items=value as IEnumerable;return items==null||value is string||value is IDictionary?new object[0]:System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<object>(items)); }
        public static Dictionary<string, object> Clone(object value) { return Map(Read(Write(value))); }
    }

    internal sealed class ApiClient
    {
        private readonly HttpClient http;
        internal static ApiClient Shared { get; set; } = new ApiClient(new HttpClientHandler { AllowAutoRedirect = false }, ApiConfiguration.BaseUrl);
        internal ApiClient(HttpMessageHandler handler, string origin)
        {
            http = new HttpClient(handler) { BaseAddress = new Uri(origin), Timeout = TimeSpan.FromSeconds(90) };
        }
        public async Task<object> SendAsync(string method, string route, object body = null, string revision = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            int generation=DesktopSession.Current.Generation;
            if (route.StartsWith("/") || Uri.IsWellFormedUriString(route, UriKind.Absolute)) throw new ArgumentException("Use a relative API route.");
            var token = await DesktopSession.Current.ProtectedTokenAsync().ConfigureAwait(false);
            return await SendTokenAsync(method, route, token, body, revision, cancellationToken,generation).ConfigureAwait(false);
        }
        internal async Task<object> SendTokenAsync(string method, string route, string token, object body = null, string revision = null, CancellationToken cancellationToken = default(CancellationToken),int? sessionGeneration=null)
        {
            if (route.StartsWith("/") || Uri.IsWellFormedUriString(route, UriKind.Absolute)) throw new ArgumentException("Use a relative API route.");
            int generation=sessionGeneration??DesktopSession.Current.Generation;
            if(generation!=DesktopSession.Current.Generation)throw new InvalidOperationException("The session changed. Sign in again.");
            using (var request = new HttpRequestMessage(new HttpMethod(method), route))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (revision != null) request.Headers.TryAddWithoutValidation("If-Match", "\"" + revision + "\"");
                if (method != "GET") request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
                if (body != null) request.Content = new StringContent(Json.Write(body), Encoding.UTF8, "application/json");
                using (var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if(generation!=DesktopSession.Current.Generation)throw new InvalidOperationException("The session changed. Reload after signing in.");
                    if (!response.IsSuccessStatusCode) throw Error(response.StatusCode, text);
                    var result=Json.Read(text);
                    if(response.Headers.ETag!=null && result is Dictionary<string,object>) Json.Map(result)["__revision"]=response.Headers.ETag.Tag.Trim('"');
                    return result;
                }
            }
        }
        public async Task<object> UploadAsync(string formId, byte[] bytes, string fileName, string contentType, string evidenceType, string fieldId, string revision)
        {
            int generation=DesktopSession.Current.Generation;
            var token = await DesktopSession.Current.ProtectedTokenAsync().ConfigureAwait(false);
            if(generation!=DesktopSession.Current.Generation)throw new InvalidOperationException("The session changed. Sign in again.");
            using (var request = new HttpRequestMessage(HttpMethod.Post, "api/qc-forms/" + formId + "/evidence"))
            using (var content = new MultipartFormDataContent())
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.TryAddWithoutValidation("If-Match", "\"" + revision + "\"");
                content.Add(new StringContent(evidenceType), "EvidenceType");
                if (!string.IsNullOrEmpty(fieldId)) content.Add(new StringContent(fieldId), "QCFormFieldID");
                var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(file, "File", fileName); request.Content = content;
                using (var response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if(generation!=DesktopSession.Current.Generation)throw new InvalidOperationException("The session changed. Reload after signing in.");
                    if (!response.IsSuccessStatusCode) throw Error(response.StatusCode, text);
                    var result=Json.Read(text);
                    if(response.Headers.ETag!=null) Json.Map(result)["__revision"]=response.Headers.ETag.Tag.Trim('"');
                    return result;
                }
            }
        }
        internal static ApiException Error(HttpStatusCode status, string text)
        {
            // Never surface arbitrary response bodies (proxy pages, tokens or stack traces).
            string message = status == HttpStatusCode.Unauthorized ? "Your session expired. Sign in again."
                : status == HttpStatusCode.Forbidden ? "Your account does not have permission for this operation."
                : status == HttpStatusCode.Conflict || (int)status == 412 ? "This record changed on the server. Reload it before trying again."
                : (int)status >= 500 ? "The service could not complete this request. Your changes have not been confirmed."
                : "The server rejected this request. Check the required values.";
            if ((int)status == 400 || (int)status == 422 || (int)status == 409 || (int)status == 412)
            {
                try { var obj = Json.Read(text); var detail = Json.Text(obj,"detail");if(detail.Length==0)detail=Json.Text(obj,"message"); if (detail.Length > 0 && detail.Length < 600) message = detail; } catch (ArgumentException) { }
            }
            return new ApiException((int)status, message);
        }
    }
}
