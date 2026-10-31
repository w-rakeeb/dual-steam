using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Security.Principal;
using Microsoft.Win32;
public sealed class Settings {public string MainSteam,SecondSteam,NativeRoot,ProfileRoot;public int Schema;}
public sealed class NativeAccount {public string User,SID,IssuerSID,Profile;}
public sealed class NativeStatus {public bool Success,WindowReady,WindowVisible;public string Error,Info,TaskbarId;public int[] RootIds,ClientIds,GameIds;public int SdkPID,Count;public long Memory,Started;public double Cpu;}
public static class Engine {
 public static readonly string Home=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
 public static Settings Current;public static string LastLaunchNotice="";
 static readonly object Gate=new object();
 public static string NativeRoot {get{return Current!=null?Current.NativeRoot:Directory.Exists(Path.Combine(Home,"Native Prototype"))?Path.Combine(Home,"Native Prototype"):Path.Combine(Home,@"Profiles\Second");}}
 public static string ProfileRoot {get{return Path.Combine(NativeRoot,"Windows Profile");}}
 public static string Helper {get{return Path.Combine(NativeRoot,@"Client\NativeAccountControl.exe");}}
 public static string PipeName{get{string file=Path.Combine(Home,@"Data\controller.json");if(File.Exists(file))return new JavaScriptSerializer().Deserialize<ControllerIdentity>(File.ReadAllText(file)).Pipe;return "WrakeebDualSteamNativeControl";}}
 [DllImport("kernel32",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref uint size);
 [DllImport("user32")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32")]static extern bool AllowSetForegroundWindow(int pid);
 public static void AllowForeground(int pid){AllowSetForegroundWindow(pid);}
 [DllImport("user32")]static extern bool ShowWindowAsync(IntPtr h,int mode);
 delegate bool WindowCallback(IntPtr h,IntPtr value);
 [DllImport("user32")]static extern bool EnumWindows(WindowCallback callback,IntPtr value);
 [DllImport("user32")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder text,int count);
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32")]static extern bool GetWindowRect(IntPtr h,out Rect rect);
 public static string Image(Process p){IntPtr h=OpenProcess(0x1000,false,p.Id);try{var path=new StringBuilder(2048);uint count=(uint)path.Capacity;return h!=IntPtr.Zero&&QueryFullProcessImageName(h,0,path,ref count)?path.ToString():"";}finally{if(h!=IntPtr.Zero)CloseHandle(h);}}
 public static string DetectSteam(){
  foreach(string key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"})foreach(string value in new[]{"SteamExe","SteamPath","InstallPath"}){
   string path=Registry.GetValue(key,value,null) as string;if(String.IsNullOrEmpty(path))continue;path=path.Replace('/','\\');if(!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))path=Path.Combine(path,"steam.exe");if(File.Exists(path)&&!path.StartsWith(NativeRoot+"\\",StringComparison.OrdinalIgnoreCase))return path;
  }
  foreach(string path in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steam.exe")})if(File.Exists(path))return path;return "";
 }
 public static string Quote(string value){if(value.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid path");return "\""+value.TrimEnd('\\')+"\"";}
 public static NativeAccount Account(){
  string file=Path.Combine(Home,@"Data\Native\account.json");if(!File.Exists(file))throw new Exception("The native Windows profile has not been provisioned on this PC. This build requires its existing local profile setup.");
  var account=new JavaScriptSerializer().Deserialize<NativeAccount>(File.ReadAllText(file));
  if(account==null||String.IsNullOrWhiteSpace(account.User)||account.Profile!=ProfileRoot||account.IssuerSID!=WindowsIdentity.GetCurrent().User.Value)throw new Exception("Use the Windows account that owns this native profile.");
  new SecurityIdentifier(account.SID);return account;
 }
 public static void Load(){
  string file=Path.Combine(Home,@"Data\settings.json");if(!File.Exists(file))return;
  var settings=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file));if(settings==null||!(settings.Schema==1||settings.Schema==2))throw new Exception("Invalid setup file");
  if(settings.Schema==1){Account();settings.Schema=2;settings.NativeRoot=NativeRoot;settings.ProfileRoot=ProfileRoot;settings.SecondSteam=Path.Combine(NativeRoot,@"Client\steam.exe");Save(settings);}
  if(String.IsNullOrWhiteSpace(settings.NativeRoot)||!Path.IsPathRooted(settings.NativeRoot)||settings.ProfileRoot!=Path.Combine(settings.NativeRoot,"Windows Profile")||settings.SecondSteam!=Path.Combine(settings.NativeRoot,@"Client\steam.exe"))throw new Exception("Native profile paths do not match this installation.");Current=settings;
 }
 static void Save(Settings value){string path=Path.Combine(Home,@"Data\settings.json");File.WriteAllText(path+".new",new JavaScriptSerializer().Serialize(value));if(File.Exists(path))File.Replace(path+".new",path,null);else File.Move(path+".new",path);}
 public static void Setup(string main){Account();if(!File.Exists(main)||!File.Exists(Helper))throw new Exception("Steam or the native helper is missing.");Current=new Settings{Schema=2,MainSteam=main,SecondSteam=Path.Combine(NativeRoot,@"Client\steam.exe"),NativeRoot=NativeRoot,ProfileRoot=ProfileRoot};Save(Current);}
 public static string Run(string exe,string args){var start=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.EnvironmentVariables.Remove("PSModulePath");using(var process=Process.Start(start)){
  var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(60000))throw new Exception("The operation timed out.");string text=output.Result+error.Result;if(process.ExitCode!=0)throw new Exception(String.IsNullOrWhiteSpace(text)?"Operation failed":text);return text.Trim();
 }}
 static bool BrokerRunning(){return Process.GetProcessesByName("NativeAccountControl").Any(p=>{try{return String.Equals(Image(p),Helper,StringComparison.OrdinalIgnoreCase);}catch{return false;}});}
 public static void Log(string text){try{string folder=Path.Combine(Home,@"Data\Logs");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"manager.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch{}}
 public static NativeStatus Status(){if(BrokerRunning())return Control("status");var roots=Current==null?new Process[0]:Process.GetProcessesByName("steam").Where(p=>Image(p).Equals(Current.SecondSteam,StringComparison.OrdinalIgnoreCase)).ToArray();var result=new NativeStatus{Success=true,RootIds=roots.Select(p=>p.Id).ToArray(),ClientIds=roots.Select(p=>p.Id).ToArray(),GameIds=new int[0],Count=roots.Length,Error=roots.Length>0?"Controller disconnected. Use Focus to reconnect.":null};if(roots.Length>0)try{result.Started=roots[0].StartTime.ToUniversalTime().Ticks;result.Memory=roots.Sum(p=>p.WorkingSet64);}catch{}return result;}
 public static void EnsureBroker(){lock(Gate){Account();if(BrokerRunning())return;string shipped=Path.Combine(Home,@"Tools\NativeAccountControl.exe");if(!File.Exists(shipped))throw new IOException("The complete app folder is required. Extract the full release ZIP.");Directory.CreateDirectory(Path.GetDirectoryName(Helper));File.Copy(shipped,Helper,true);Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File "+Quote(Path.Combine(Home,@"Source\StartNative.ps1")));Exception last=null;for(int i=0;i<12;i++){try{Control("status");return;}catch(Exception ex){last=ex;System.Threading.Thread.Sleep(250);}}throw new IOException("The background controller did not become ready. Reopen Dual Steam and try again.",last);}}
 public static NativeStatus Control(string command){lock(Gate){
  using(var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut)){
   pipe.Connect(2500);var writer=new StreamWriter(pipe){AutoFlush=true};writer.WriteLine(command);
   var task=new StreamReader(pipe).ReadLineAsync();if(!task.Wait(25000))throw new Exception("Second Steam did not respond. Close its game normally and try again.");string text=task.Result;if(text==null||text.Length>65536)throw new Exception("Invalid native response");var result=new JavaScriptSerializer().Deserialize<NativeStatus>(text);if(result==null||!result.Success)throw new Exception(result==null?"Invalid native response":result.Error);return result;
  }
 }}
 public static uint[] SecondPids(){lock(Gate){if(!BrokerRunning())return new uint[0];return (Control("status").ClientIds??new int[0]).Select(p=>(uint)p).ToArray();}}
 public static Process[] SecondSteam(){return (Status().RootIds??new int[0]).Select(id=>{try{return Process.GetProcessById((int)id);}catch{return null;}}).Where(p=>p!=null&&p.ProcessName=="steam").ToArray();}
 public static void OpenSecond(){lock(Gate){LastLaunchNotice="";
  if(Current==null)throw new Exception("Set up Second Steam first.");Account();
  bool stopped=!Process.GetProcessesByName("steam").Any(p=>String.Equals(Image(p),Current.SecondSteam,StringComparison.OrdinalIgnoreCase));
  if(stopped)LibraryCatalog.Sync(Current);EnsureBroker();
  if(stopped&&Preferences.Current.SyncMillennium)try{MillenniumSharing.Import();}catch(IOException ex){LastLaunchNotice="Second Steam opened. Automatic add-on import was skipped: "+ex.Message;Log(LastLaunchNotice);}foreach(var broker in Process.GetProcessesByName("NativeAccountControl"))if(Image(broker).Equals(Helper,StringComparison.OrdinalIgnoreCase))AllowForeground(broker.Id);Control("open");
 }}
 public static void QuitSecond(){lock(Gate){if(!BrokerRunning()&&Status().RootIds.Length>0)EnsureBroker();if(BrokerRunning())Control("stop");}}
 public static Process[] MainSteam(){string path=Current==null?DetectSteam():Current.MainSteam;return Process.GetProcessesByName("steam").Where(p=>{try{return String.Equals(Image(p),path,StringComparison.OrdinalIgnoreCase);}catch{return false;}}).ToArray();}
 public static bool FocusIds(uint[] ids){IntPtr best=IntPtr.Zero;long area=0;EnumWindows(delegate(IntPtr h,IntPtr state){uint pid;GetWindowThreadProcessId(h,out pid);if(!ids.Contains(pid))return true;var title=new StringBuilder(256);var kind=new StringBuilder(128);GetWindowText(h,title,256);GetClassName(h,kind,128);if(title.ToString()=="Steam"&&kind.ToString()=="SDL_app"){best=h;return false;}Rect rect;if(IsWindowVisible(h)&&GetWindowRect(h,out rect)){long size=(long)(rect.Right-rect.Left)*(rect.Bottom-rect.Top);if(size>area){area=size;best=h;}}return true;},IntPtr.Zero);if(best==IntPtr.Zero)return false;ShowWindowAsync(best,9);SetForegroundWindow(best);return true;}
 public static void OpenMain(){string path=Current==null?DetectSteam():Current.MainSteam;if(!File.Exists(path))throw new Exception("Locate main steam.exe first.");if(MainSteam().Length>0){var ids=new[]{"steam","steamwebhelper"}.SelectMany(n=>Process.GetProcessesByName(n)).Where(p=>Image(p).StartsWith(Path.GetDirectoryName(path)+"\\",StringComparison.OrdinalIgnoreCase)).Select(p=>(uint)p.Id).ToArray();FocusIds(ids);return;}var start=new ProcessStartInfo(path,"-master_ipc_name_override WrakeebDualSteamMain"){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)};start.EnvironmentVariables.Remove("steam_master_ipc_name_override");Process.Start(start);}
}
