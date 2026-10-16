using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class Auth
 {
  public static UserAccount Current {get;private set;}
  internal static void SetCurrent(UserAccount account) { Current=account; }
  public static bool IsSignedIn {get{return DesktopSession.Current.Verified;}}
  public const string PasswordRule="Your organisation's Supabase password policy applies.";
  public static Task SignInAsync(string email,string password){return DesktopSession.Current.SignInAsync(email.Trim(),password);}
  public static void SignOut(){ _ = DesktopSession.Current.DetachAndSignOutAsync(); Current=null; SharedData.ClientRecords=new Dictionary<string,Dictionary<string,object>>(); Repository.SignOut(); }
  public static async Task SignOutAsync(){try{await DesktopSession.Current.SignOutAsync();}finally{SignOut();}}
 }
}
