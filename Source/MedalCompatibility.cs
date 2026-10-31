using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Web.Script.Serialization;
public sealed class MedalFile {public string Original,Disabled,Backup,Hash;}
public sealed class MedalRule {public string Path,Sid,OriginalSecurity;public bool Added;}
public static class MedalCompatibility {
 static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Medal");
 static readonly string Folder=Path.Combine(Engine.Home,@"Data\MedalCompatibility");
 static string Manifest {get{return Path.Combine(Folder,"rules.json");}}
 static string Preference {get{return Path.Combine(Folder,"enabled.txt");}}
 static SecurityIdentifier User {get{return WindowsIdentity.GetCurrent().User;}}
 public static bool Enabled {get{return File.Exists(Preference)&&File.ReadAllText(Preference).Trim()=="enabled";}}
 static string[] Helpers(){
  if(!Directory.Exists(Root))return new string[0];
  return Directory.GetDirectories(Root,"recorder-*",SearchOption.TopDirectoryOnly).SelectMany(dir=>new[]{"MedalSteamUtils.exe","SteamUtils.exe"}.Select(name=>Path.Combine(dir,@"Plugins\Steam",name))).Where(path=>File.Exists(path)||File.Exists(path+".dualsteam-disabled")).ToArray();
 }
 static void Validate(string path){
  string full=Path.GetFullPath(path);string name=Path.GetFileName(full);
  if(!full.StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(Path.GetDirectoryName(full))!="Steam"||Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(full)))!="Plugins"||!(name=="MedalSteamUtils.exe"||name=="SteamUtils.exe"))throw new Exception("Unexpected Medal helper path");
  var dir=new DirectoryInfo(Path.GetDirectoryName(full));while(dir!=null&&dir.FullName.Length>=Root.Length){if((dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new Exception("Medal helper folder is redirected; review it before changing compatibility.");dir=dir.Parent;}
 }
 static bool Blocked(string path){if(!File.Exists(path))return false;return File.GetAccessControl(path).GetAccessRules(true,false,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(r=>r.IdentityReference==User&&r.AccessControlType==AccessControlType.Deny&&(r.FileSystemRights&FileSystemRights.ExecuteFile)!=0);}
 static Process[] Processes(){return Process.GetProcesses().Where(p=>{try{return Engine.Image(p).StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase);}catch{return false;}}).ToArray();}
 public static bool HasConflict(){return Processes().Length>0&&(Helpers().Any(p=>File.Exists(p)&&!Blocked(p))||Processes().Any(p=>p.ProcessName=="MedalSteamUtils"||p.ProcessName=="SteamUtils"));}
 public static string Status {get{var files=Helpers();return files.Length==0?"Medal Steam integration not detected.":Enabled&&files.All(Blocked)?"Compatibility enabled. Medal recording stays available; Steam lobby/friend helpers are paused.":"Standard Medal Steam integration. No automatic changes are made.";}}
 static List<MedalRule> Read(){return File.Exists(Manifest)?new JavaScriptSerializer().Deserialize<List<MedalRule>>(File.ReadAllText(Manifest)):new List<MedalRule>();}
 static void Save(List<MedalRule> records){Directory.CreateDirectory(Folder);File.WriteAllText(Manifest+".new",new JavaScriptSerializer().Serialize(records));if(File.Exists(Manifest))File.Replace(Manifest+".new",Manifest,null);else File.Move(Manifest+".new",Manifest);}
 static FileSystemAccessRule Rule(){return new FileSystemAccessRule(User,FileSystemRights.ExecuteFile,AccessControlType.Deny);}
 static void Unblock(string path){var acl=File.GetAccessControl(path);acl.RemoveAccessRule(Rule());File.SetAccessControl(path,acl);}
 static void RestoreRenamedFiles(){
  string old=Path.Combine(Folder,"files.json");if(!File.Exists(old))return;
  foreach(var file in new JavaScriptSerializer().Deserialize<List<MedalFile>>(File.ReadAllText(old))){
   Validate(file.Original);if(file.Disabled!=file.Original+".dualsteam-disabled")throw new Exception("Unexpected legacy backup path");
   if(!File.Exists(file.Disabled))continue;
   if(!File.Exists(file.Original))File.Move(file.Disabled,file.Original);
   else{using(var sha=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(file.Disabled)){if(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","")!=file.Hash)throw new Exception("Legacy disabled helper changed");}
    File.Move(file.Disabled,Path.Combine(Folder,Guid.NewGuid().ToString("N")+"-legacy-"+Path.GetFileName(file.Original)));
   }
  }
 }
 public static void Ensure(){if(Enabled&&Helpers().Any(p=>!Blocked(p)))Enable();}
 public static void Enable(){
  if(Engine.SecondSteam().Length>0)throw new Exception("Quit Second Steam before changing Medal compatibility.");
  if(Processes().Any(p=>p.ProcessName=="MedalSteamUtils"||p.ProcessName=="SteamUtils"))throw new Exception("Close games before enabling Medal compatibility.");
  RestoreRenamedFiles();var paths=Helpers();if(paths.Length==0)throw new Exception("Medal's Steam helpers were not found.");foreach(string path in paths)Validate(path);
  var records=Read();var changed=new List<string>();
  try{foreach(string path in paths){
   bool blocked=Blocked(path);var known=records.FirstOrDefault(r=>r.Path==path&&r.Sid==User.Value);
   if(known==null){known=new MedalRule{Path=path,Sid=User.Value,OriginalSecurity=File.GetAccessControl(path).GetSecurityDescriptorSddlForm(AccessControlSections.All),Added=!blocked};records.Add(known);}
   if(blocked)continue;known.Added=true;Save(records);var acl=File.GetAccessControl(path);acl.AddAccessRule(Rule());File.SetAccessControl(path,acl);changed.Add(path);
  }if(!paths.All(Blocked))throw new Exception("Medal helper block verification failed");Save(records);File.WriteAllText(Preference,"enabled");
  }catch{foreach(string path in changed)Unblock(path);throw;}
 }
 public static void Restore(){
  if(Engine.SecondSteam().Length>0)throw new Exception("Quit Second Steam before restoring Medal integration.");
  foreach(var record in Read()){Validate(record.Path);if(record.Sid!=User.Value)throw new Exception("Use the Windows account that enabled Medal compatibility.");if(record.Added&&File.Exists(record.Path))Unblock(record.Path);}
  RestoreRenamedFiles();Directory.CreateDirectory(Folder);File.WriteAllText(Preference,"disabled");
 }
}
