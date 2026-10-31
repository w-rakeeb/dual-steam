using System;using System.IO;using System.Linq;using System.Diagnostics;using System.Security.Principal;using System.Web.Script.Serialization;using System.Collections.Generic;using System.Security.AccessControl;
public static class RemovalService {
 public static bool Pending{get{return File.Exists(Path.Combine(Engine.Home,@"Data\Removal\request.json"));}}
 public static void RequireComplete(){if(Pending)throw new IOException("Removal has not finished. Open Settings → General → Finish removing setup. Keep the recovery records; after removal succeeds you can create Second Steam again.");}
 public static RemovalPlan Preview(){
  if(Engine.Current==null)throw new IOException("No configured Second Steam setup was found.");var account=Engine.Account();string file=Path.Combine(Engine.Home,@"Data\Native\account.json");var metadata=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
  object managed;if(!metadata.TryGetValue("ManagedBy",out managed)||!Convert.ToString(managed).StartsWith("Dual Steam",StringComparison.Ordinal))throw new IOException("The account is not marked as managed by Dual Steam.");object seed;metadata.TryGetValue("OriginalProfileBackup",out seed);
  string index=Path.Combine(Path.GetDirectoryName(Engine.Current.MainSteam),@"steamapps\libraryfolders.vdf");if(File.Exists(index)&&new FileInfo(index).Length>1024*1024)throw new IOException("Main Steam's library list is unexpectedly large.");
  var p=new RemovalPlan{Home=Engine.Home,Root=Engine.NativeRoot,Profile=Engine.ProfileRoot,MainSteam=Engine.Current.MainSteam,User=account.User,SID=account.SID,IssuerSID=account.IssuerSID,OriginalProfileBackup=Convert.ToString(seed),Libraries=File.Exists(index)?LibraryCatalog.ReadPaths(File.ReadAllText(index)):new string[0]};RemovalCore.Validate(p,WindowsIdentity.GetCurrent().User.Value);return p;
 }
 public static void Remove(RemovalPlan approved){
  var plan=Preview();if(plan.Root!=approved.Root||plan.SID!=approved.SID||plan.Profile!=approved.Profile||plan.OriginalProfileBackup!=approved.OriginalProfileBackup)throw new IOException("The setup changed after preview. Review removal again.");
  string previous=Path.Combine(Engine.Home,@"Data\Removal\result.json");if(File.Exists(previous)&&new FileInfo(previous).Length<1048576&&!Directory.Exists(plan.Root)&&(String.IsNullOrEmpty(plan.OriginalProfileBackup)||!Directory.Exists(plan.OriginalProfileBackup))){var prior=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(previous));if(prior.ContainsKey("Success")&&Convert.ToBoolean(prior["Success"])&&prior.ContainsKey("SID")&&Convert.ToString(prior["SID"])==plan.SID&&Convert.ToString(prior["RootRemoved"])==plan.Root){bool missing=false;try{new NTAccount(Environment.MachineName,plan.User).Translate(typeof(SecurityIdentifier));}catch(IdentityNotMappedException){missing=true;}if(missing){FinishLocal(plan);return;}}}
  Engine.PrepareUpdate();
  if(MedalCompatibility.Enabled)MedalCompatibility.Restore();
  if(!Pending)foreach(var broker in Process.GetProcessesByName("NativeAccountControl"))using(broker){if(Engine.Image(broker).Equals(Engine.Helper,StringComparison.OrdinalIgnoreCase)&&!broker.WaitForExit(10000))throw new IOException("The background controller is still closing. Wait a moment and retry removal.");}
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
