using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Diagnostics;
public static class GameSharing {
 static string Root{get{return Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;}}
 static string Home{get{return NativePaths.Home;}}
 static string Folder{get{return Path.Combine(Root,@"Windows Profile\AppData\Local\PioneerGame\Saved");}}
 static string Stage{get{return Path.Combine(Home,@"Data\Sharing");}}
 static string Backups{get{return Path.Combine(Root,@"Evidence\Settings Backups");}}
 static readonly string[] Names={@"Config\WindowsClient\GameUserSettings.ini",@"SaveGames\EmbarkOptionSaveGame.sav",@"SaveGames\EmbarkSaveGameClientKeyBindings.sav"};
 public static string Describe(){var output=new StringBuilder();foreach(string name in Names){string path=Path.Combine(Folder,name);output.AppendLine(Path.GetFileName(name)+": "+(File.Exists(path)?new FileInfo(path).Length+" bytes · "+File.GetLastWriteTime(path).ToString("g"):"not created yet"));}string saves=Path.Combine(Folder,"SaveGames");output.AppendLine("Account-specific option caches: "+(Directory.Exists(saves)?Directory.GetFiles(saves,"EmbarkOptionSaveGame_*.sav").Length:0)+" (kept separate)");return output.ToString();}
 static void Safe(string path){string current=Path.GetFullPath(path);while(!String.IsNullOrEmpty(current)){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Settings path contains a link. Copy cancelled.");current=Path.GetDirectoryName(current);}}
 public static string Apply(bool restore){
  if(Process.GetProcessesByName("PioneerGame").Any())throw new IOException("Close ARC Raiders in both clients before copying or restoring preferences.");
  Safe(Folder);Safe(Stage);Safe(Backups);Directory.CreateDirectory(Backups);string batch=Path.Combine(Backups,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(batch);
  var updates=new Dictionary<string,byte[]>();string manifest=Path.Combine(Backups,"last.json");
  if(restore){if(!File.Exists(manifest))throw new IOException("No ARC preferences backup is available.");var files=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(manifest));foreach(var item in files){if(!Names.Contains(item.Key)||!Path.GetFullPath(item.Value).StartsWith(Backups+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid settings backup.");Safe(item.Value);updates[item.Key]=File.ReadAllBytes(item.Value);}}
  else{
   string target=Path.Combine(Folder,Names[0]);if(File.Exists(Path.Combine(Stage,"arc-graphics.ini"))){if(!File.Exists(target))throw new IOException("Start ARC Raiders once on Second Steam before copying preferences.");string merged=ArcPreferences.SelectIni(File.ReadAllText(target),false)+Environment.NewLine+File.ReadAllText(Path.Combine(Stage,"arc-graphics.ini"));updates[Names[0]]=new UTF8Encoding(false).GetBytes(merged);}
   target=Path.Combine(Folder,Names[1]);var selected=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Stage,"arc-options.json")));if(selected.Count>0){if(!File.Exists(target))throw new IOException("ARC local options have not been created on Second Steam yet.");updates[Names[1]]=ArcPreferences.Parse(File.ReadAllBytes(target)).Merge(selected);}
   string bindings=Path.Combine(Stage,"arc-bindings.sav");if(File.Exists(bindings)){byte[] bytes=File.ReadAllBytes(bindings);if(bytes.Length>262144||bytes.Length<128||Encoding.ASCII.GetString(bytes,0,4)!="GVAS"||Encoding.ASCII.GetString(bytes).IndexOf("EmbarkSaveGameClientKeyBindingsV2",StringComparison.Ordinal)<0)throw new IOException("Unsupported ARC keybindings format.");updates[Names[2]]=bytes;}
  }
  var originals=new Dictionary<string,string>();try{foreach(var item in updates){string path=Path.Combine(Folder,item.Key);if(!File.Exists(path))throw new IOException("Target preferences are missing: "+Path.GetFileName(path));string backup=Path.Combine(batch,Path.GetFileName(path));File.Copy(path,backup);originals.Add(item.Key,backup);}
   foreach(var item in updates){string path=Path.Combine(Folder,item.Key),temp=path+".dualsteam-new";File.WriteAllBytes(temp,item.Value);File.Replace(temp,path,null);if(!File.ReadAllBytes(path).SequenceEqual(item.Value))throw new IOException("Settings copy verification failed.");}
  }catch{foreach(var item in originals)File.Copy(item.Value,Path.Combine(Folder,item.Key),true);throw;}
  File.WriteAllText(manifest,new JavaScriptSerializer().Serialize(originals));return (restore?"Restored":"Copied")+" "+updates.Count+" ARC preference files. Account caches, progress, consent and shader data were preserved.";
 }
}
