using System;using System.IO;using System.Linq;using System.Diagnostics;using System.Security.Principal;using System.Web.Script.Serialization;using System.Collections.Generic;using System.Security.AccessControl;
public static class RemovalService {
 public static bool Pending{get{return File.Exists(Path.Combine(Engine.Home,@"Data\Removal\request.json"));}}
 public static bool Available{get{return Engine.Current!=null||File.Exists(Path.Combine(Engine.Home,@"Data\Native\account.json"));}}
 public static void RequireComplete(){if(Pending)throw new IOException("Removal has not finished. Open Settings → General → Finish removing setup. Keep the recovery records; after removal succeeds you can create Second Steam again.");}
 public static RemovalPlan Preview(){
  string file=Path.Combine(Engine.Home,@"Data\Native\account.json");if(!File.Exists(file))throw new IOException("No verified Second Steam account record was found. No Windows account was removed.");var metadata=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
  var account=Engine.Current==null?new JavaScriptSerializer().Deserialize<NativeAccount>(File.ReadAllText(file)):Engine.Account();
  if(account==null||account.IssuerSID!=WindowsIdentity.GetCurrent().User.Value||String.IsNullOrEmpty(account.Profile)||!Path.IsPathRooted(account.Profile)||!Path.GetFileName(account.Profile.TrimEnd('\\')).Equals("Windows Profile",StringComparison.OrdinalIgnoreCase))throw new IOException("The saved account does not belong to this Windows user or profile layout.");
  string root=Path.GetDirectoryName(account.Profile.TrimEnd('\\')),main=Engine.Current==null?Engine.DetectSteam():Engine.Current.MainSteam;if(String.IsNullOrEmpty(main))throw new IOException("Main Steam must be located before reviewing removal so shared libraries can be protected.");
  object managed;if(!metadata.TryGetValue("ManagedBy",out managed)||!Convert.ToString(managed).StartsWith("Dual Steam",StringComparison.Ordinal))throw new IOException("The account is not marked as managed by Dual Steam.");object seed;metadata.TryGetValue("OriginalProfileBackup",out seed);
  string index=Path.Combine(Path.GetDirectoryName(main),@"steamapps\libraryfolders.vdf");if(File.Exists(index)&&new FileInfo(index).Length>1024*1024)throw new IOException("Main Steam's library list is unexpectedly large.");
  string registered=Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\"+account.SID,"ProfileImagePath",null) as string;
  var p=new RemovalPlan{Home=Engine.Home,Root=root,Profile=account.Profile.TrimEnd('\\'),RegisteredProfile=String.IsNullOrEmpty(registered)?null:Environment.ExpandEnvironmentVariables(registered).TrimEnd('\\'),MainSteam=main,User=account.User,SID=account.SID,IssuerSID=account.IssuerSID,OriginalProfileBackup=Convert.ToString(seed),Libraries=File.Exists(index)?LibraryCatalog.ReadPaths(File.ReadAllText(index)):new string[0]};RemovalCore.Validate(p,WindowsIdentity.GetCurrent().User.Value);return p;
 }
 public static void Remove(RemovalPlan approved){
  var plan=Preview();if(plan.Root!=approved.Root||plan.SID!=approved.SID||plan.Profile!=approved.Profile||plan.OriginalProfileBackup!=approved.OriginalProfileBackup||!String.Equals(plan.RegisteredProfile,approved.RegisteredProfile,StringComparison.OrdinalIgnoreCase))throw new IOException("The setup changed after preview. Review removal again.");
  string previous=Path.Combine(Engine.Home,@"Data\Removal\result.json");if(File.Exists(previous)&&new FileInfo(previous).Length<1048576&&!Directory.Exists(plan.Root)&&(String.IsNullOrEmpty(plan.OriginalProfileBackup)||!Directory.Exists(plan.OriginalProfileBackup))){var prior=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(previous));if(prior.ContainsKey("Success")&&Convert.ToBoolean(prior["Success"])&&prior.ContainsKey("SID")&&Convert.ToString(prior["SID"])==plan.SID&&Convert.ToString(prior["RootRemoved"])==plan.Root){bool missing=false;try{new NTAccount(Environment.MachineName,plan.User).Translate(typeof(SecurityIdentifier));}catch(IdentityNotMappedException){missing=true;}if(missing){FinishLocal(plan);return;}}}
  Engine.PrepareUpdate();
  if(MedalCompatibility.Enabled)MedalCompatibility.Restore();
  if(!Pending&&Engine.Current!=null)foreach(var broker in Process.GetProcessesByName("NativeAccountControl"))using(broker){if(Engine.Image(broker).Equals(Engine.Helper,StringComparison.OrdinalIgnoreCase)&&!broker.WaitForExit(10000))throw new IOException("The background controller is still closing. Wait a moment and retry removal.");}
  string state=Path.Combine(Engine.Home,@"Data\Removal");Directory.CreateDirectory(state);SettingsTransfer.NoLinks(state);string request=Path.Combine(state,"request.json"),result=Path.Combine(state,"result.json");File.WriteAllText(request,new JavaScriptSerializer().Serialize(plan));if(File.Exists(result))File.Delete(result);
  var process=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File "+Engine.Quote(Path.Combine(Engine.Home,@"Source\Setup-Elevated.ps1"))+" -Remove -Request "+Engine.Quote(request)){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
  using(process){if(!process.WaitForExit(300000))throw new IOException("Removal is still running. Wait for its result in Data / Removal before retrying. Do not start a new setup yet.");if(process.ExitCode!=0){string message="Removal did not finish. The recovery records were kept.";if(File.Exists(result)){var details=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(result));if(details.ContainsKey("Error"))message+=" "+Convert.ToString(details["Error"]);}throw new IOException(message);}}
  FinishLocal(plan);
 }
 static void FinishLocal(RemovalPlan plan){
  DesktopAccess.Revoke(plan.SID);
  Engine.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File "+Engine.Quote(Path.Combine(Engine.Home,@"Source\Create-Shortcuts.ps1"))+" -RemoveSecond");
  RemovalCore.DeleteTree(Path.Combine(Engine.Home,"Data"));Engine.Current=null;Preferences.Current=new Preferences();Preferences.Save();
 }
}
