using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
public sealed class LibraryFolder {
 public string Path; public int Installed;
}
public static class LibraryCatalog {
 public static string[] ReadPaths(string text){
  return Regex.Matches(text,"\"path\"\\s+\"((?:\\\\.|[^\"])*)\"").Cast<Match>().Select(m=>m.Groups[1].Value.Replace("\\\\","\\").Replace("\\\"","\"")).ToArray();
 }
 public static List<LibraryFolder> Discover(Settings settings){
  string main=System.IO.Path.GetDirectoryName(settings.MainSteam);
  var paths=new List<string>{main};
  string index=System.IO.Path.Combine(main,@"steamapps\libraryfolders.vdf");
  if(File.Exists(index)){if(new FileInfo(index).Length>1024*1024)throw new Exception("Steam library list is unexpectedly large.");paths.AddRange(ReadPaths(File.ReadAllText(index)));}
  return paths.Where(p=>System.IO.Path.IsPathRooted(p) && p.Length>=3 && p[1]==':' && (Preferences.Current.IncludeSystemDriveLibraries||Char.ToUpperInvariant(p[0])!='C') && Directory.Exists(System.IO.Path.Combine(p,"steamapps")))
   .Select(p=>System.IO.Path.GetFullPath(p).TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase)
   .Select(p=>new LibraryFolder{Path=p,Installed=Directory.GetFiles(System.IO.Path.Combine(p,"steamapps"),"appmanifest_*.acf").Length}).ToList();
 }
 static string Escape(string value){return value.Replace("\\","\\\\").Replace("\"","\\\"");}
 public static int Sync(Settings settings){
  var libraries=Discover(settings);
  string client=System.IO.Path.GetDirectoryName(settings.SecondSteam);
  var paths=new[]{client}.Concat(libraries.Select(l=>l.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var text=new StringBuilder("\"libraryfolders\"\n{\n");int index=0;
  foreach(string path in paths){
   text.Append("\t\"").Append(index++).Append("\"\n\t{\n\t\t\"path\"\t\"").Append(Escape(path)).Append("\"\n\t\t\"label\"\t\"\"\n\t\t\"apps\"\n\t\t{\n");
   string apps=System.IO.Path.Combine(path,"steamapps");
   if(Directory.Exists(apps))foreach(string manifest in Directory.GetFiles(apps,"appmanifest_*.acf")){
    var match=Regex.Match(System.IO.Path.GetFileName(manifest),"^appmanifest_(\\d+)\\.acf$");
    if(match.Success)text.Append("\t\t\t\"").Append(match.Groups[1].Value).Append("\"\t\"0\"\n");
   }
   text.Append("\t\t}\n\t}\n");
  }
  text.Append("}\n");
  string destination=System.IO.Path.Combine(client,@"steamapps\libraryfolders.vdf");
  Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
  string content=text.ToString();if(File.Exists(destination)&&File.ReadAllText(destination)==content)return libraries.Sum(l=>l.Installed);
  string backup=System.IO.Path.Combine(Engine.Home,@"Backups\LibraryCatalog");Directory.CreateDirectory(backup);
  if(File.Exists(destination))File.Copy(destination,System.IO.Path.Combine(backup,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".vdf"));
  File.WriteAllText(destination+".new",content,new UTF8Encoding(false));
  if(File.Exists(destination))File.Replace(destination+".new",destination,null);else File.Move(destination+".new",destination);
  return libraries.Sum(l=>l.Installed);
 }
}
