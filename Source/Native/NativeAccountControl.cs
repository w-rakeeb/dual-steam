using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.IO.Pipes;
using System.Security.AccessControl;
using Microsoft.Win32;
public static class NativeAccountControl {
 static readonly string Root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;
 static readonly string Client=Root+@"\Client\";
 static string Ipc {get{return "DualSteam_"+NativePaths.Identity.SID.Replace("-","");}}
 static readonly object IdentityGate=new object();static DualSteamNative.SteamTaskbarIdentity identity;
 static readonly object OperationGate=new object();static DateTime missingManager=DateTime.MinValue;
 static void WatchManager(){try{bool running=RuntimeAuthorization.Valid(Home);if(running){missingManager=DateTime.MinValue;return;}if(missingManager==DateTime.MinValue){missingManager=DateTime.UtcNow;return;}if((DateTime.UtcNow-missingManager).TotalSeconds<10)return;lock(OperationGate){if(Execute(new[]{"stop"})==0)Environment.Exit(0);}}catch{}}
 static string Home{get{return NativePaths.Home;}}
 static IntPtr SteamWindow(Process[] clients,bool primaryOnly=false){var ids=clients.Select(p=>p.Id).ToArray();IntPtr best=IntPtr.Zero;long area=0;EnumWindows(delegate(IntPtr h,IntPtr state){uint pid;GetWindowThreadProcessId(h,out pid);if(!ids.Contains((int)pid))return true;var title=new StringBuilder(256);var kind=new StringBuilder(128);GetWindowText(h,title,256);GetClassName(h,kind,128);Rect rect;if(kind.ToString()=="SDL_app"&&(title.ToString()=="Steam"||title.ToString()=="Second Steam")){best=h;return false;}if(!primaryOnly&&IsWindowVisible(h)&&title.Length>0&&GetWindowRect(h,out rect)){long size=(long)(rect.Right-rect.Left)*(rect.Bottom-rect.Top);if(size>area){best=h;area=size;}}return true;},IntPtr.Zero);return best;}
 static string WindowTaskbarId(IntPtr h){try{return h==IntPtr.Zero?"":DualSteamNative.SteamTaskbarIdentity.ReadId(h);}catch{return "unavailable";}}
 static void UpdateIdentity(){lock(IdentityGate){try{bool enabled=!File.Exists(Path.Combine(Home,@"Data\separate-taskbar.off"));identity.Update(Clients(),enabled);}catch{}}}
 [DllImport("user32")]static extern bool EnumWindows(Callback cb,IntPtr state);
 delegate bool Callback(IntPtr hwnd,IntPtr state);
 [DllImport("user32")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32")]static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32")]static extern bool ShowWindowAsync(IntPtr hwnd,int mode);
 [DllImport("user32")]static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern bool SetWindowText(IntPtr hwnd,string text);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int count);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr hwnd,StringBuilder text,int count);
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
 [DllImport("user32")]static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 [DllImport("user32")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32")]static extern bool GetCursorPos(out Point point);
 [DllImport("user32")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("kernel32",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("advapi32",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [DllImport("kernel32")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint size);
 static string Image(Process process){IntPtr handle=OpenProcess(0x1000,false,process.Id);try{var path=new StringBuilder(2048);uint size=(uint)path.Capacity;return handle!=IntPtr.Zero&&QueryFullProcessImageName(handle,0,path,ref size)?path.ToString():"";}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}}
 static bool Own(Process process){IntPtr handle=OpenProcess(0x1000,false,process.Id),token=IntPtr.Zero;try{if(handle==IntPtr.Zero||!OpenProcessToken(handle,8,out token))return false;using(var identity=new WindowsIdentity(token))return identity.User==WindowsIdentity.GetCurrent().User;}finally{if(token!=IntPtr.Zero)CloseHandle(token);if(handle!=IntPtr.Zero)CloseHandle(handle);}}
 static Process[] Clients(){return new[]{"steam","steamwebhelper","gameoverlayui64","steamerrorreporter64"}.SelectMany(name=>Process.GetProcessesByName(name)).Where(p=>{try{return Own(p)&&Image(p).StartsWith(Client,StringComparison.OrdinalIgnoreCase);}catch{return false;}}).ToArray();}
 static bool ForeignClient(){return Process.GetProcessesByName("steam").Any(p=>Image(p).Equals(Client+"steam.exe",StringComparison.OrdinalIgnoreCase)&&!Own(p));}
 static Process[] Games(int app=0){
  var tracked=new Dictionary<int,Tuple<int,DateTime>>();string log=Client+@"logs\gameprocess_log.txt";
  if(!File.Exists(log))return new Process[0];
  string content;using(var file=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(file))content=reader.ReadToEnd();
  foreach(string line in content.Split('\n')){
   var add=Regex.Match(line,@"^\[([^\]]+)\] AppID (\d+) adding PID (\d+)");
   if(add.Success)tracked[Int32.Parse(add.Groups[3].Value)]=Tuple.Create(Int32.Parse(add.Groups[2].Value),DateTime.Parse(add.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture));
   var remove=Regex.Match(line,@"AppID \d+ no longer tracking PID (\d+)");if(remove.Success)tracked.Remove(Int32.Parse(remove.Groups[1].Value));
  }
  return tracked.Where(item=>app==0||item.Value.Item1==app).Select(item=>{try{var process=Process.GetProcessById(item.Key);return Own(process)&&Math.Abs((process.StartTime-item.Value.Item2).TotalSeconds)<15?process:null;}catch{return null;}}).Where(p=>p!=null).ToArray();
 }
 static void Write(object value){File.WriteAllText(Root+@"\Evidence\native-control.json",new JavaScriptSerializer().Serialize(value));}
 public static int Main(string[] args){try{if(args.Length!=2||args[0]!="broker")return 2;NativePaths.Initialize(args[1]);return Broker(NativePaths.Identity.IssuerSID);}catch(Exception ex){try{Directory.CreateDirectory(Root+@"\Evidence");File.WriteAllText(Root+@"\Evidence\controller-error.txt",ex.Message);}catch{}return 1;}}
 static int Broker(string issuer){
  if(WindowsIdentity.GetCurrent().User.Value!=NativePaths.Identity.SID)return 1;
  var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(issuer),PipeAccessRights.ReadWrite,AccessControlType.Allow));
  security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-18"),PipeAccessRights.FullControl,AccessControlType.Allow));
  Environment.SetEnvironmentVariable("steam_master_ipc_name_override",null);
  identity=new DualSteamNative.SteamTaskbarIdentity(Home);
  using(var identityTimer=new Timer(delegate{UpdateIdentity();WatchManager();},null,0,2000))
  using(var mutex=new System.Threading.Mutex(false,"Local\\"+NativePaths.Identity.Pipe+"Broker")){
   bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)return 1;
    while(true){using(var pipe=new NamedPipeServerStream(NativePaths.Identity.Pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None,4096,4096,security)){
     pipe.WaitForConnection();var reader=new StreamReader(pipe);var writer=new StreamWriter(pipe){AutoFlush=true};
     try{string line=reader.ReadLine();if(line==null||line.Length>128)throw new Exception("Invalid control command.");
      string[] command=line.Split(' ');if(!new[]{"open","status","focus","stop","game","close-game","ack-input","copy-settings","restore-settings","detach","verify-session"}.Contains(command[0]))throw new Exception("Unsupported command.");
      string result;lock(OperationGate){Execute(command);result=File.ReadAllText(Root+@"\Evidence\native-control.json");}writer.WriteLine(result);
      if((command[0]=="stop"||command[0]=="detach")&&result.Contains("\"Success\":true"))return 0;
     }catch(Exception ex){try{writer.WriteLine(new JavaScriptSerializer().Serialize(new{Success=false,Error=ex.Message}));}catch{}}
    }}
   }finally{lock(IdentityGate)identity.Dispose();if(held)mutex.ReleaseMutex();}
  }
 }
 public static int Execute(string[] args){try{
  if(WindowsIdentity.GetCurrent().User.Value!=NativePaths.Identity.SID||!String.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Root+@"\Windows Profile",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected Windows identity or profile.");
  string action=args.Length>0?args[0]:"status";
  if(action!="status"&&action!="stop"&&action!="detach")RuntimeAuthorization.Require(Home);
  if(action=="verify-session"){Write(new{Success=true,Info="Authorized manager session accepted by the second Windows account."});return 0;}
  if(action=="copy-settings"||action=="restore-settings"){
   if(Games().Any())throw new IOException("Close games in Second Steam before copying or restoring settings.");
   string history=Path.Combine(Root,@"Evidence\Settings Imports");string info;
   if(action=="copy-settings"){Guid id;if(args.Length!=2||!Guid.TryParseExact(args[1],"N",out id))throw new IOException("Invalid import identifier.");info=SettingsTransfer.Apply(Path.Combine(Home,@"Data\Settings Imports",args[1]),Path.Combine(Root,"Windows Profile"),history);}
   else info=SettingsTransfer.Restore(Path.Combine(Root,"Windows Profile"),history);
   Write(new{Success=true,Info=info});return 0;
  }
  if(action=="detach"){Write(new{Success=true});return 0;}
  var clients=Clients();var roots=clients.Where(p=>p.ProcessName.Equals("steam",StringComparison.OrdinalIgnoreCase)).ToArray();
  if(action=="open"){
   if(ForeignClient())throw new IOException("The second-client folder is being used outside its managed Windows account. Exit that Steam window normally, then use the Second Steam shortcut. No other Steam process was closed.");
   if(roots.Length>1)throw new Exception("Duplicate native clients found; close extras before continuing.");
   Environment.SetEnvironmentVariable("steam_master_ipc_name_override",null);
   if(roots.Length==0){
    Process.Start(new ProcessStartInfo(Client+"steam.exe","-master_ipc_name_override "+Ipc){UseShellExecute=false,WorkingDirectory=Client});
    action="focus";
   }
   action="focus";
  }
  if(action=="close-game"){
   int app;if(args.Length!=2||!Int32.TryParse(args[1],out app)||app<=0)throw new Exception("Invalid game ID.");
   foreach(var game in Games(app)){try{game.CloseMainWindow();if(!game.WaitForExit(5000)){game.Kill();game.WaitForExit(5000);}}catch{}}
   if(Games(app).Any())throw new Exception("The game could not be closed; close its window manually.");Write(new{Success=true,GameClosed=app});return 0;
  }
  if(action=="ack-input"){
   var ids=clients.Select(p=>p.Id).ToArray();bool clicked=false;
   EnumWindows(delegate(IntPtr hwnd,IntPtr state){uint pid;GetWindowThreadProcessId(hwnd,out pid);var title=new StringBuilder(256);GetWindowText(hwnd,title,title.Capacity);Rect r;
    if(ids.Contains((int)pid)&&title.ToString()=="Steam Dialog"&&GetWindowRect(hwnd,out r)&&r.Right-r.Left==700&&r.Bottom-r.Top==480){
     SetForegroundWindow(hwnd);Thread.Sleep(200);
     if(GetForegroundWindow()==hwnd){Point old;GetCursorPos(out old);SetCursorPos((r.Left+r.Right)/2,r.Bottom-28);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos(old.X,old.Y);clicked=true;}
     return false;
    }return true;},IntPtr.Zero);
   Write(new{Success=true,InfoDialogClicked=clicked});return 0;
  }
  if(action=="stop"){
   foreach(var game in Games()){try{game.CloseMainWindow();if(!game.WaitForExit(5000))game.Kill();}catch{}}
   if(Games().Any())throw new Exception("Close the second account's game before quitting Steam.");
   foreach(var process in clients.OrderBy(p=>p.ProcessName.Equals("steam",StringComparison.OrdinalIgnoreCase)?1:0)){try{process.Kill();process.WaitForExit(5000);}catch{}}
   if(Clients().Any())throw new Exception("Some native client processes could not be stopped.");
   Write(new{Success=true,Stopped=true});return 0;
  }
  if(action=="focus"){
   IntPtr best=IntPtr.Zero;for(int attempt=0;attempt<40;attempt++){clients=Clients();best=SteamWindow(clients,true);if(best!=IntPtr.Zero)break;Thread.Sleep(400);}
   if(best==IntPtr.Zero)throw new Exception("Steam is running but its interface is not ready. Wait for its update/sign-in, then use Focus again. No extra client was started.");
   ShowWindowAsync(best,9);SetForegroundWindow(best);UpdateIdentity();
  }
  if(action=="game"){
   throw new Exception("Use Play inside Second Steam. External game launch routing has not passed verification.");
  }
  var route=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess","pid",0)??0;
  clients=Clients();roots=clients.Where(p=>p.ProcessName.Equals("steam",StringComparison.OrdinalIgnoreCase)).ToArray();var metrics=clients.Select(p=>{try{return new{Id=p.Id,Memory=p.WorkingSet64,Cpu=p.TotalProcessorTime.TotalMilliseconds,Started=p.StartTime.ToUniversalTime().Ticks};}catch{return null;}}).Where(p=>p!=null).ToArray();var window=SteamWindow(clients,true);
  Write(new{Success=true,RootIds=roots.Select(p=>p.Id).ToArray(),ClientIds=clients.Select(p=>p.Id).ToArray(),GameIds=Games().Select(p=>p.Id).ToArray(),WindowReady=window!=IntPtr.Zero,WindowVisible=window!=IntPtr.Zero&&IsWindowVisible(window),Memory=metrics.Sum(p=>p.Memory),Cpu=metrics.Sum(p=>p.Cpu),Started=roots.Length>0?roots[0].StartTime.ToUniversalTime().Ticks:0,Count=clients.Length,TaskbarId=WindowTaskbarId(window),SdkPID=route});return 0;
 }catch(Exception ex){Write(new{Success=false,Error=ex.Message});return 1;}}
}
