using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace DualSteam {
 public static class UpdatePackage {
  public static readonly string[] Files={"Dual Steam.exe","Dual Steam.exe.config","Dual Steam Updater.exe","Tools/NativeAccountControl.exe","README.md","LICENSE","Assets/dual-steam.ico","Assets/dual-steam.png","Assets/dual-steam.svg","Assets/second-steam.ico","Assets/second-steam.png","Assets/second-steam.svg","Assets/steam.png","Assets/steam.svg","Assets/app.manifest","Assets/SOURCE.md","Source/StartNative.ps1","Source/DesktopAccess.cs","Source/Setup-Elevated.ps1","Source/Create-Shortcuts.ps1"};
  static readonly string[] Required=Files;
  public static bool RuntimeFilesPresent(string home){return Required.All(file=>File.Exists(Path.Combine(home,file.Replace('/' ,Path.DirectorySeparatorChar))));}
  public static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
  public static void VerifyHash(string file,string expected){if(expected==null||!System.Text.RegularExpressions.Regex.IsMatch(expected,"^[a-fA-F0-9]{64}$")||!Hash(file).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("The update checksum did not match. Nothing was installed.");}
  public static string[] Validate(string file){
   if(new FileInfo(file).Length>50*1024*1024)throw new IOException("The update package is too large.");
   var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long size=0;
   using(var zip=ZipFile.OpenRead(file)){
    if(zip.Entries.Count>100)throw new IOException("Too many update files.");
    foreach(var entry in zip.Entries){string name=entry.FullName.Replace('\\','/');if(name.EndsWith("/")&&new[]{"Assets/","Tools/","Source/"}.Contains(name))continue;
     if(name.StartsWith("/")||name.Contains(":")||name.Split('/').Any(p=>p==".."||p==".")||!Files.Contains(name,StringComparer.OrdinalIgnoreCase)||!found.Add(name))throw new IOException("The update contains an unexpected or unsafe file.");
     if(((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new IOException("Linked update files are not supported.");
     size+=entry.Length;if(entry.Length>32*1024*1024||size>100*1024*1024)throw new IOException("The extracted update is too large.");
    }
   }
   if(Required.Any(p=>!found.Contains(p)))throw new IOException("The update package is incomplete.");return found.ToArray();
  }
  public static void Extract(string zipPath,string stage){Validate(zipPath);Directory.CreateDirectory(stage);using(var zip=ZipFile.OpenRead(zipPath))foreach(var entry in zip.Entries){string name=entry.FullName.Replace('\\','/');if(name.EndsWith("/")&&new[]{"Assets/","Tools/","Source/"}.Contains(name))continue;string canonical=Files.Single(f=>f.Equals(name,StringComparison.OrdinalIgnoreCase));string target=Path.Combine(stage,canonical.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write))input.CopyTo(output);}}
  static void NoLinks(string target,string relative){string current=target;foreach(string part in relative.Replace('\\','/').Split('/')){current=Path.Combine(current,part);if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("An update target is a folder link. No update was installed.");}}
  public static string Apply(string stage,string target,Action<string> beforeCopy){
   target=Path.GetFullPath(target);stage=Path.GetFullPath(stage);if(target.TrimEnd('\\').Equals(stage.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Update staging must be separate.");
   if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Update the actual app folder, not a folder link.");
   var candidates=Directory.GetFiles(stage,"*",SearchOption.AllDirectories).Select(p=>new{Source=p,Relative=p.Substring(stage.TrimEnd('\\').Length+1).Replace('\\','/')}).ToArray();
   foreach(var file in candidates){if(!Files.Contains(file.Relative,StringComparer.OrdinalIgnoreCase))throw new IOException("Unexpected staged update file.");NoLinks(target,file.Relative);}
   string backup=Path.Combine(target,"Backups","App-update-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
   NoLinks(target,"Backups");Directory.CreateDirectory(backup);var changed=new List<string>();var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   try{
    foreach(var file in candidates){string destination=Path.Combine(target,file.Relative.Replace('/',Path.DirectorySeparatorChar));string saved=Path.Combine(backup,file.Relative.Replace('/',Path.DirectorySeparatorChar));if(File.Exists(destination)){Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(destination,saved,false);existed.Add(file.Relative);}if(beforeCopy!=null)beforeCopy(file.Relative);Directory.CreateDirectory(Path.GetDirectoryName(destination));changed.Add(file.Relative);string temp=destination+".update-temp";File.Copy(file.Source,temp,true);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
    return backup;
   }catch{
    foreach(string relative in Enumerable.Reverse(changed)){string destination=Path.Combine(target,relative.Replace('/',Path.DirectorySeparatorChar));if(existed.Contains(relative))File.Copy(Path.Combine(backup,relative.Replace('/',Path.DirectorySeparatorChar)),destination,true);else if(File.Exists(destination))File.Delete(destination);if(File.Exists(destination+".update-temp"))File.Delete(destination+".update-temp");}throw;
   }
  }
 }
}
