using System;using System.IO;using System.Linq;using System.Text;using System.Text.RegularExpressions;using System.Collections.Generic;using System.Diagnostics;using System.Web.Script.Serialization;
public sealed class SettingsScan {
 public string GameFolder,Name,AppId,SourceFolder;public List<SettingsFile> Files=new List<SettingsFile>();public List<string> Notes=new List<string>();public int Inspected,Skipped;
}
public static class SettingsScanner {
 public static string UserProfile{get{return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);}}
 static string Key(string name){return Regex.Replace(name.ToLowerInvariant(),"[^a-z0-9]","");}
 static bool Match(string name,List<string> aliases){string key=Key(name);return key.Length>=4&&aliases.Any(a=>a.Length>=4&&(key==a||key.StartsWith(a)||a.StartsWith(key)));}
 static IEnumerable<string> Children(string folder){try{SettingsTransfer.NoLinks(folder);return Directory.Exists(folder)?Directory.GetDirectories(folder):new string[0];}catch(UnauthorizedAccessException){return new string[0];}catch(IOException){return new string[0];}}
 public static SettingsScan Scan(string game,string custom=null){
  game=SettingsTransfer.Canonical(game);if(!Directory.Exists(game)||game==Path.GetPathRoot(game).TrimEnd('\\'))throw new IOException("Select a specific game folder, not a drive.");SettingsTransfer.NoLinks(game);
  var scan=new SettingsScan{GameFolder=game,Name=Path.GetFileName(game),SourceFolder=custom};var aliases=new List<string>{Key(scan.Name)};
  var parent=Directory.GetParent(game);if(parent!=null&&parent.Name.Equals("common",StringComparison.OrdinalIgnoreCase)&&parent.Parent!=null&&parent.Parent.Name.Equals("steamapps",StringComparison.OrdinalIgnoreCase)){
   foreach(string manifest in Directory.GetFiles(parent.Parent.FullName,"appmanifest_*.acf").Take(1000))try{string text=File.ReadAllText(manifest);var dir=Regex.Match(text,"\"installdir\"\\s*\"([^\"]+)\"");if(dir.Success&&dir.Groups[1].Value.Equals(Path.GetFileName(game),StringComparison.OrdinalIgnoreCase)){Match name=Regex.Match(text,"\"name\"\\s*\"([^\"]+)\""),id=Regex.Match(text,"\"appid\"\\s*\"([0-9]+)\"");if(name.Success)scan.Name=name.Groups[1].Value;if(id.Success)scan.AppId=id.Groups[1].Value;aliases.Add(Key(scan.Name));break;}}catch(IOException){}
  }
  foreach(string dir in Children(game).Take(40))if(!new[]{"Engine","Content","EasyAntiCheat","BattlEye","_CommonRedist","steamapps"}.Contains(Path.GetFileName(dir),StringComparer.OrdinalIgnoreCase))if(Directory.Exists(Path.Combine(dir,"Binaries"))||Directory.Exists(Path.Combine(dir,"Saved"))||Directory.GetFiles(dir,"*.uproject").Length>0)aliases.Add(Key(Path.GetFileName(dir)));
  var roots=new List<string>();if(!String.IsNullOrWhiteSpace(custom)){custom=SettingsTransfer.Canonical(custom);if(!Directory.Exists(custom)||custom==Path.GetPathRoot(custom).TrimEnd('\\'))throw new IOException("Select a specific settings folder.");SettingsTransfer.NoLinks(custom);roots.Add(custom);}
  else{
   int folders=0;foreach(string relative in new[]{@"AppData\Local",@"AppData\Roaming",@"AppData\LocalLow","Documents","Saved Games"}){
    string root=Path.Combine(UserProfile,relative);foreach(string first in Children(root).Take(500)){if(++folders>2500)break;if(Match(Path.GetFileName(first),aliases))roots.Add(first);else foreach(string second in Children(first).Take(80)){if(++folders>2500)break;if(Match(Path.GetFileName(second),aliases))roots.Add(second);}}
   }
   scan.Notes.Add("Matched profile folders using the installation name, Steam manifest and detected project folders. A name match is a candidate, not proof of the game's ownership.");
  }
  foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))Walk(root,root,scan,0);
  scan.Files=scan.Files.GroupBy(f=>f.Source,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).Take(SettingsTransfer.MaxFiles).ToList();
  scan.Notes.Add("Only named text settings are offered. Binary saves, progress, account data, caches and shared installation files are excluded. Some games keep options in cloud services, registry entries or unsupported formats.");
  if(scan.Files.Count==0)scan.Notes.Add("No transferable text settings were identified. Use Choose settings folder if the game stores its preferences under another name.");
  if(scan.Inspected>=12000||scan.Files.Count>=SettingsTransfer.MaxFiles)scan.Notes.Add("Scan limit reached (12,000 files or 100 candidates). Choose a narrower settings folder to inspect the remaining files.");return scan;
 }
 static void Walk(string folder,string root,SettingsScan scan,int depth){
  if(depth>6||scan.Inspected>=12000||scan.Files.Count>=SettingsTransfer.MaxFiles)return;
  try{SettingsTransfer.NoLinks(folder);foreach(string path in Directory.GetFiles(folder)){
   if(++scan.Inspected>12000||scan.Files.Count>=SettingsTransfer.MaxFiles)break;string rel=path.Substring(root.Length+1);if(!SettingsTransfer.Candidate(Path.GetFileName(root)+"\\"+rel)){scan.Skipped++;continue;}
   try{SettingsTransfer.NoLinks(path);var info=new FileInfo(path);if(info.Length==0||info.Length>SettingsTransfer.MaxFile){scan.Skipped++;continue;}byte[] bytes=File.ReadAllBytes(path);SettingsTransfer.ValidateText(bytes);
    string mapped=path.StartsWith(SettingsTransfer.Canonical(UserProfile)+"\\",StringComparison.OrdinalIgnoreCase)?path.Substring(SettingsTransfer.Canonical(UserProfile).Length+1):"";
    if(mapped.Length>0)try{SettingsTransfer.Destination(UserProfile,mapped);}catch(IOException){mapped="";}
    scan.Files.Add(new SettingsFile{Source=path,Relative=mapped,Hash=SettingsTransfer.Hash(bytes),Size=bytes.Length});
   }catch(IOException){scan.Skipped++;}catch(UnauthorizedAccessException){scan.Skipped++;}
  }
  foreach(string dir in Children(folder)){string name=Path.GetFileName(dir);if(Regex.IsMatch(name,@"^(?:SaveGames?|Saves|SaveData|.*Cache.*|.*Shader.*|Logs?|Crash.*|Accounts?|Auth|Tokens?)$",RegexOptions.IgnoreCase))continue;Walk(dir,root,scan,depth+1);}
  }catch(IOException){scan.Skipped++;}catch(UnauthorizedAccessException){scan.Skipped++;}
 }
 public static string Copy(SettingsScan scan,IEnumerable<SettingsFile> selection,string destinationBase){
  if(scan==null)throw new IOException("Scan a game folder first.");var files=selection.ToList();if(files.Count<1||files.Count>SettingsTransfer.MaxFiles)throw new IOException("Select at least one settings file (up to 100).");
  EnsureClosed(scan.GameFolder);string stageRoot=Path.Combine(Engine.Home,@"Data\Settings Imports");SettingsTransfer.NoLinks(stageRoot);string id=Guid.NewGuid().ToString("N"),stage=Path.Combine(stageRoot,id);Directory.CreateDirectory(Path.Combine(stage,"payload"));var plan=new SettingsPlan{Id=id};int total=0;
  foreach(var file in files){if(!scan.Files.Contains(file))throw new IOException("File is not part of this scan.");SettingsTransfer.NoLinks(file.Source);var info=new FileInfo(file.Source);if(info.Length!=file.Size||info.Length>SettingsTransfer.MaxFile)throw new IOException("A source changed; scan again before copying.");byte[] bytes=File.ReadAllBytes(file.Source);SettingsTransfer.ValidateText(bytes);if(SettingsTransfer.Hash(bytes)!=file.Hash)throw new IOException("A source changed; scan again before copying.");total+=bytes.Length;if(total>SettingsTransfer.MaxTotal)throw new IOException("Selected settings exceed 32 MB.");string relative=file.Relative;
   if(!String.IsNullOrWhiteSpace(destinationBase)){string sourceRoot=String.IsNullOrWhiteSpace(scan.SourceFolder)?null:SettingsTransfer.Canonical(scan.SourceFolder);if(sourceRoot==null)throw new IOException("Choose a settings folder before using a custom destination.");relative=destinationBase.Trim().TrimEnd('\\','/')+"\\"+file.Source.Substring(sourceRoot.Length+1);}
   SettingsTransfer.Destination(Engine.ProfileRoot,relative);plan.Files.Add(new SettingsFile{Relative=relative,Hash=file.Hash,Size=file.Size});File.WriteAllBytes(Path.Combine(stage,"payload",(plan.Files.Count-1)+".txt"),bytes);
  }
  File.WriteAllText(Path.Combine(stage,"request.json"),new JavaScriptSerializer().Serialize(plan),new UTF8Encoding(false));Engine.EnsureBroker();EnsureClosed(scan.GameFolder);string result=Engine.Control("copy-settings "+id).Info;File.WriteAllText(Path.Combine(stageRoot,"last-game.txt"),scan.GameFolder,new UTF8Encoding(false));return result;
 }
 public static string Restore(){if(Engine.Current==null)throw new IOException("Set up Second Steam first.");string file=Path.Combine(Engine.Home,@"Data\Settings Imports\last-game.txt");if(!File.Exists(file))throw new IOException("No settings import is available to restore.");EnsureClosed(File.ReadAllText(file));Engine.EnsureBroker();return Engine.Control("restore-settings").Info;}
 public static void EnsureClosed(string game){foreach(var process in Process.GetProcesses())try{string image=Engine.Image(process);if(image.StartsWith(SettingsTransfer.Canonical(game)+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Close this game in both Steam clients before copying settings.");}catch(InvalidOperationException){}if(Engine.Current!=null&&(Engine.Status().GameIds??new int[0]).Length>0)throw new IOException("Close games in Second Steam before copying or restoring settings.");}
}
