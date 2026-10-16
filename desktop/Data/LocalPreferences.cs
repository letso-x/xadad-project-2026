using System;
using System.IO;
namespace MzuApplication.Data
{
 internal static class LocalPreferences
 {
  private static string PathName {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MzukuluQC","desktop-preferences.json");}}
  public static int IdleMinutes {get;private set;}=15;
  public static void Load(){try{if(!File.Exists(PathName))return;var value=Json.Id(Json.Read(File.ReadAllText(PathName)),"idleMinutes");if(value>=1&&value<=120)IdleMinutes=(int)value;}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}}
  public static void Save(int minutes){if(minutes<1||minutes>120)throw new ArgumentException("Choose 1–120 minutes.");Directory.CreateDirectory(Path.GetDirectoryName(PathName));File.WriteAllText(PathName,Json.Write(new{idleMinutes=minutes}));IdleMinutes=minutes;Repository.Db.Settings.IdleTimeoutMinutes=minutes;}
 }
}
