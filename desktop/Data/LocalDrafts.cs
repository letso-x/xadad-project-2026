using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace MzuApplication.Data
{
 // Genuine local cache of unacknowledged input only; never a shared-data fallback.
 internal static class LocalDrafts
 {
  internal static string StorageRoot {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MzukuluQC","api-drafts");
  private static string PathFor(string form)
  {
   var user=Guid.Parse(Auth.Current.Id).ToString("N");var id=long.Parse(form).ToString();
   var root=Path.Combine(StorageRoot,user);Directory.CreateDirectory(root);return Path.Combine(root,id+".bin");
  }
  public static Dictionary<string,object> Read(string form){var path=PathFor(form);if(!File.Exists(path))return null;return Json.Map(Json.Read(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser))));}
  public static void Save(string form,long revision,Dictionary<string,object> values)
  {
   var path=PathFor(form);var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(new {revision,values})),null,DataProtectionScope.CurrentUser);
   var temporary=path+".tmp";File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
  }
 }
}
