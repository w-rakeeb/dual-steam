using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace DualSteam {
 public static class UpdaterProgram {
  [STAThread]public static int Main(string[] args){
   string result=null;UpdateRequest request=null;
   try{
    if(args.Length!=2||args[0]!="--request")return 2;
    string requestFile=Path.GetFullPath(args[1]);result=Path.Combine(Path.GetDirectoryName(requestFile),"result.txt");
    request=new JavaScriptSerializer().Deserialize<UpdateRequest>(File.ReadAllText(requestFile));
    if(request==null||String.IsNullOrWhiteSpace(request.Target))throw new IOException("Invalid update request.");
    string target=Path.GetFullPath(request.Target);string executable=Path.Combine(target,"Dual Steam.exe");
    if(!File.Exists(executable))throw new IOException("The target app could not be found.");
    UpdatePackage.VerifyHash(request.Package,request.Sha256);
    string stage=Path.Combine(Path.GetDirectoryName(requestFile),"Extracted-"+Guid.NewGuid().ToString("N"));UpdatePackage.Extract(request.Package,stage);
    var incoming=AssemblyName.GetAssemblyName(Path.Combine(stage,"Dual Steam.exe")).Version;
    var expected=UpdateService.ParseVersion(request.Version);
    if(incoming.Major!=expected.Major||incoming.Minor!=expected.Minor||incoming.Build!=expected.Build||expected.Revision>=0&&incoming.Revision!=expected.Revision)throw new IOException("The downloaded app version does not match its release.");
    var installed=AssemblyName.GetAssemblyName(executable).Version;if(incoming<installed)throw new IOException("An update cannot install an older version.");
    if(request.ManagerPid>0)try{using(var manager=Process.GetProcessById(request.ManagerPid)){if(manager.StartTime.ToUniversalTime().Ticks!=request.ManagerStartTicks)throw new IOException("The manager process changed. Start the update again.");if(!manager.WaitForExit(30000))throw new IOException("The manager is still open. No update was installed.");}}catch(ArgumentException){}
    string backup=UpdatePackage.Apply(stage,target,null);
    try{File.WriteAllText(result,"SUCCESS: updated to "+request.Version+". Backup: "+backup);Directory.CreateDirectory(Path.Combine(target,"Logs"));File.WriteAllText(Path.Combine(target,"Logs","last-update.txt"),"Updated to "+request.Version+" at "+DateTime.Now.ToString("s"));}catch{}
    if(request.Restart)try{Process.Start(new ProcessStartInfo(executable){UseShellExecute=true});}catch{try{File.WriteAllText(result,"SUCCESS: update installed. Reopen Dual Steam manually.");}catch{}}return 0;
   }catch(Exception ex){try{if(result!=null)File.WriteAllText(result,"FAILED: "+ex.Message);if(request!=null&&Directory.Exists(request.Target)){Directory.CreateDirectory(Path.Combine(request.Target,"Logs"));File.WriteAllText(Path.Combine(request.Target,"Logs","last-update.txt"),"Update failed: "+ex.Message+". Check App-update backups if recovery is needed.");if(request.Restart&&File.Exists(Path.Combine(request.Target,"Dual Steam.exe")))Process.Start(new ProcessStartInfo(Path.Combine(request.Target,"Dual Steam.exe")){UseShellExecute=true});}}catch{}return 1;}
  }
 }
}
