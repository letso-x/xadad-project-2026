namespace MzukuluQMSMobile.WinUI;
internal static class StartupTrace
{
 [System.Diagnostics.Conditional("DEBUG")]
 public static void Mark(string stage)
 {
  try{System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"mzukulu-windows-startup.log"),DateTimeOffset.UtcNow.ToString("O")+" "+stage+Environment.NewLine);}catch{/* Startup must not depend on diagnostics. */}
 }
}
