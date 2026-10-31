using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;using System.Security.Principal;
public sealed class RemovalPlan {public string Home,Root,Profile,MainSteam,User,SID,IssuerSID,OriginalProfileBackup;public string[] Libraries;}
public static class RemovalCore {
 [System.Runtime.InteropServices.DllImport("kernel32",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [System.Runtime.InteropServices.DllImport("advapi32",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [System.Runtime.InteropServices.DllImport("kernel32")]static extern bool CloseHandle(IntPtr handle);
 public static bool ProcessUsesAccount(int pid,string sid){IntPtr process=OpenProcess(0x1000,false,pid),token=IntPtr.Zero;try{if(process==IntPtr.Zero||!OpenProcessToken(process,8,out token))return false;using(var identity=new WindowsIdentity(token))return identity.User.Value==sid;}finally{if(token!=IntPtr.Zero)CloseHandle(token);if(process!=IntPtr.Zero)CloseHandle(process);}}
 static string Full(string value){if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))throw new IOException("Removal requires absolute, recorded paths.");return Path.GetFullPath(value).TrimEnd('\\');}
 static bool Inside(string child,string parent){return child.Equals(parent,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent+"\\",StringComparison.OrdinalIgnoreCase);}
 public static void Validate(RemovalPlan p,string issuer){
  if(p==null||p.IssuerSID!=issuer||p.SID==issuer||!Regex.IsMatch(p.User??"",@"^(DualSteamLocal|DualSteam_[a-f0-9]{8})$"))throw new IOException("This is not the managed setup owned by this Windows account.");new SecurityIdentifier(p.SID);new SecurityIdentifier(issuer);int rid;if(!Regex.IsMatch(p.SID,@"^S-1-5-21-[0-9]+-[0-9]+-[0-9]+-[0-9]+$")||!Int32.TryParse(p.SID.Substring(p.SID.LastIndexOf('-')+1),out rid)||rid<1000)throw new IOException("The secondary account is not a standard local Windows identity.");
  string root=Full(p.Root),home=Full(p.Home),profile=Full(p.Profile),main=Full(Path.GetDirectoryName(Full(p.MainSteam)));
  if(root.Length<=3||root.StartsWith(@"\\")||Inside(home,root)||Inside(main,root)||Inside(root,main)||!profile.Equals(Path.Combine(root,"Windows Profile"),StringComparison.OrdinalIgnoreCase))throw new IOException("Removal paths overlap the manager, Main Steam, or an unexpected Windows profile.");
  string current=Full(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),windows=Full(Environment.GetFolderPath(Environment.SpecialFolder.Windows));if(Inside(current,root)||Inside(root,windows)||Inside(windows,root))throw new IOException("Removal would affect the active user or Windows.");
  SettingsTransfer.NoLinks(p.MainSteam);foreach(string library in p.Libraries??new string[0]){SettingsTransfer.NoLinks(library);if(Inside(Full(library),root)||Inside(root,Path.Combine(Full(library),"steamapps")))throw new IOException("Removal overlaps shared Main Steam library content. Move or preserve that library before removing setup.");}
  SettingsTransfer.NoLinks(root);SettingsTransfer.NoLinks(home);
  if(!String.IsNullOrEmpty(p.OriginalProfileBackup)){string seed=Full(p.OriginalProfileBackup);string users=Directory.GetParent(current).FullName;if(!Path.GetDirectoryName(seed).Equals(users,StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(Path.GetFileName(seed),"^"+Regex.Escape(p.User)+@"(?:\.[A-Za-z0-9_.-]+)?$",RegexOptions.IgnoreCase)||Inside(current,seed)||Inside(seed,current))throw new IOException("The recorded Windows bootstrap backup is outside its managed location.");SettingsTransfer.NoLinks(seed);}
 }
 public static void DeleteTree(string target){
  target=Full(target);if(target.Length<=3)throw new IOException("Refusing to remove a drive root.");SettingsTransfer.NoLinks(target);if(Directory.Exists(target))DeleteDirectory(target,target);
 }
 public static void UnlinkProfileLinks(string profile){profile=Full(profile);if(profile.Length<=3)throw new IOException("Refusing a drive root.");SettingsTransfer.NoLinks(profile);if(Directory.Exists(profile))UnlinkChildren(profile,profile);}
 static void ClearReadOnly(string path,FileAttributes attributes){if((attributes&FileAttributes.ReparsePoint)==0&&(attributes&FileAttributes.ReadOnly)!=0)File.SetAttributes(path,attributes&~FileAttributes.ReadOnly);}
 static void UnlinkChildren(string folder,string root){if(!Inside(Full(folder),root))throw new IOException("Profile traversal left its verified folder.");ClearReadOnly(folder,File.GetAttributes(folder));foreach(string path in Directory.GetFileSystemEntries(folder)){var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReparsePoint)!=0){if((attributes&FileAttributes.Directory)!=0)Directory.Delete(path,false);else File.Delete(path);}else if((attributes&FileAttributes.Directory)!=0)UnlinkChildren(path,root);}}
 static void DeleteDirectory(string directory,string root){
  if(!Inside(Full(directory),root))throw new IOException("Removal left its verified folder.");
  ClearReadOnly(directory,File.GetAttributes(directory));
  foreach(string path in Directory.GetFileSystemEntries(directory)){
   if(!Inside(Full(path),root))throw new IOException("Unexpected removal path.");var attributes=File.GetAttributes(path);
   if((attributes&FileAttributes.Directory)!=0){if((attributes&FileAttributes.ReparsePoint)!=0)Directory.Delete(path,false);else DeleteDirectory(path,root);}
   else{if((attributes&FileAttributes.ReparsePoint)==0&&(attributes&FileAttributes.ReadOnly)!=0)File.SetAttributes(path,attributes&~FileAttributes.ReadOnly);File.Delete(path);}
  }
  Directory.Delete(directory,false);
 }
}
