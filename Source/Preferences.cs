using System;using System.IO;using System.Web.Script.Serialization;
public sealed class Preferences {
 public bool TrayOnClose=true,SecondTray=true,SeparateTaskbar=true;
 public bool RecoverMillennium=true;
 public bool KeepSecondRunning=false,AutoUpdates=true,ImportMillennium=false,ImportThemes=false,ImportThemeSettings=false,ImportPlugins=false,SyncMillennium=false,IncludeSystemDriveLibraries=true;
 public static Preferences Current=new Preferences();
 static string FilePath{get{return Path.Combine(Engine.Home,@"Data\preferences.json");}}
 public static void Load(){if(File.Exists(FilePath)){Current=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(FilePath));if(Current==null)throw new IOException("Preferences are unreadable.");Current.KeepSecondRunning=false;}}
 public static void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath+".new",new JavaScriptSerializer().Serialize(Current));if(File.Exists(FilePath))File.Replace(FilePath+".new",FilePath,null);else File.Move(FilePath+".new",FilePath);string off=Path.Combine(Engine.Home,@"Data\separate-taskbar.off");if(Current.SeparateTaskbar){if(File.Exists(off))File.Delete(off);}else File.WriteAllText(off,"disabled");string required=Path.Combine(Engine.Home,@"Data\require-manager.on");if(Current.KeepSecondRunning){if(File.Exists(required))File.Delete(required);}else File.WriteAllText(required,"Second Steam and its games require the manager.");}
}
