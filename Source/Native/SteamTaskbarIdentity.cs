using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.IO;using System.Linq;using System.Runtime.InteropServices;
namespace DualSteamNative {
 public sealed class SteamTaskbarIdentity:IDisposable {
  sealed class Proc {public int Id;public long StartedUtcTicks;}
  public const string SecondSteamId="Wrakeeb.SecondSteam";
  public const string ManagerId="Wrakeeb.DualSteam.Manager";
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  public static void SetManagerIdentity(){Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(ManagerId));}
  public static void SetManagerWindow(IntPtr window,string home){var store=WindowStore(window);try{Write(store,2,"\""+Path.Combine(home,"Dual Steam.exe")+"\"");Write(store,3,Path.Combine(home,@"Assets\dual-steam.ico")+",0");Write(store,4,"Dual Steam");Write(store,5,ManagerId);Marshal.ThrowExceptionForHR(store.Commit());}finally{Marshal.FinalReleaseComObject(store);}}
  [StructLayout(LayoutKind.Sequential)]struct Key {public Guid Format;public uint Id;public Key(uint id){Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");Id=id;}}
  [StructLayout(LayoutKind.Sequential)]struct Value {public ushort Type,R1,R2,R3;public IntPtr Text,Extra;}
  [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Store {
   [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out Key key);[PreserveSig]int GetValue(ref Key key,out Value value);[PreserveSig]int SetValue(ref Key key,ref Value value);[PreserveSig]int Commit();
  }
  [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out Store store);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHGetPropertyStoreFromParsingName(string path,IntPtr context,uint flags,ref Guid iid,out Store store);
  [DllImport("ole32.dll")]static extern int PropVariantClear(ref Value value);
  delegate bool Callback(IntPtr window,IntPtr state);
  [DllImport("user32.dll")]static extern bool EnumWindows(Callback callback,IntPtr state);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool SetWindowText(IntPtr window,string text);
  [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out UIntPtr result);
  sealed class Original {public int Pid;public long Started;public readonly Dictionary<uint,string> Values=new Dictionary<uint,string>();public IntPtr Small,Large;public string Title;}
  readonly Dictionary<IntPtr,Original> windows=new Dictionary<IntPtr,Original>();readonly Icon small,large;readonly string home;
  static Icon LoadIcon(string path,int size){using(var stream=new MemoryStream(File.ReadAllBytes(path)))using(var icon=new Icon(stream,size,size))return (Icon)icon.Clone();}
  public SteamTaskbarIdentity(string folder){home=folder;small=LoadIcon(Path.Combine(home,"Assets","second-steam.ico"),16);large=LoadIcon(Path.Combine(home,"Assets","second-steam.ico"),32);}
  static Store WindowStore(IntPtr window){Guid iid=typeof(Store).GUID;Store store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window,ref iid,out store));return store;}
  static string Read(Store store,uint id){var key=new Key(id);Value value;Marshal.ThrowExceptionForHR(store.GetValue(ref key,out value));try{return value.Type==31?Marshal.PtrToStringUni(value.Text):null;}finally{PropVariantClear(ref value);}}
  static void Write(Store store,uint id,string text){var key=new Key(id);var value=new Value();if(text!=null){value.Type=31;value.Text=Marshal.StringToCoTaskMemUni(text);}try{Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));}finally{PropVariantClear(ref value);}}
  public static string ReadId(IntPtr window){var store=WindowStore(window);try{return Read(store,5);}finally{Marshal.FinalReleaseComObject(store);}}
  public static void SetShortcut(string path){SetShortcut(path,SecondSteamId);}
  public static void SetShortcut(string path,string identity){if(identity!=SecondSteamId&&identity!=ManagerId)throw new ArgumentException("Unknown shortcut identity");Guid iid=typeof(Store).GUID;Store store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreFromParsingName(path,IntPtr.Zero,2,ref iid,out store));try{Write(store,5,identity);Marshal.ThrowExceptionForHR(store.Commit());}finally{Marshal.FinalReleaseComObject(store);}}
  static IntPtr IconMessage(IntPtr window,int size,IntPtr icon){UIntPtr result;SendMessageTimeout(window,0x80,new IntPtr(size),icon,2,100,out result);return new IntPtr(unchecked((long)result.ToUInt64()));}
  public void Update(Process[] processes,bool enabled){
   if(!enabled){Restore();return;}foreach(var stale in windows.Keys.Where(k=>!IsWindow(k)).ToArray())windows.Remove(stale);var ids=processes.ToDictionary(p=>p.Id,p=>new Proc{Id=p.Id,StartedUtcTicks=p.StartTime.ToUniversalTime().Ticks});
   EnumWindows(delegate(IntPtr window,IntPtr state){uint pid;GetWindowThreadProcessId(window,out pid);Proc item;if(!IsWindowVisible(window)||!ids.TryGetValue((int)pid,out item))return true;
    try{using(var process=Process.GetProcessById(item.Id)){long started=process.StartTime.ToUniversalTime().Ticks;if(item.StartedUtcTicks<=0||Math.Abs(started-item.StartedUtcTicks)>10)return true;
     var store=WindowStore(window);try{Original original;if(!windows.TryGetValue(window,out original)){var title=new System.Text.StringBuilder(256);GetWindowText(window,title,256);original=new Original{Pid=item.Id,Started=started,Title=title.ToString()};foreach(uint id in new uint[]{2,3,4,5})original.Values[id]=Read(store,id);windows[window]=original;original.Small=IconMessage(window,0,small.Handle);original.Large=IconMessage(window,1,large.Handle);}
      Write(store,2,"\""+Path.Combine(home,"Dual Steam.exe")+"\" --second");Write(store,3,Path.Combine(home,"Assets","second-steam.ico")+",0");Write(store,4,"Second Steam");Write(store,5,SecondSteamId);IconMessage(window,0,small.Handle);IconMessage(window,1,large.Handle);var kind=new System.Text.StringBuilder(128);var caption=new System.Text.StringBuilder(256);GetClassName(window,kind,128);GetWindowText(window,caption,256);if(kind.ToString()=="SDL_app"&&caption.ToString()=="Steam")SetWindowText(window,"Second Steam");
     }finally{Marshal.FinalReleaseComObject(store);}
    }}catch(ArgumentException){}catch(InvalidOperationException){}catch(COMException){}catch(System.ComponentModel.Win32Exception){}return true;
   },IntPtr.Zero);
  }
  void Restore(){foreach(var pair in windows){try{uint pid;GetWindowThreadProcessId(pair.Key,out pid);if(pid!=pair.Value.Pid)continue;using(var process=Process.GetProcessById((int)pid)){if(process.StartTime.ToUniversalTime().Ticks!=pair.Value.Started)continue;}var store=WindowStore(pair.Key);try{foreach(var value in pair.Value.Values)Write(store,value.Key,value.Value);}finally{Marshal.FinalReleaseComObject(store);}IconMessage(pair.Key,0,pair.Value.Small);IconMessage(pair.Key,1,pair.Value.Large);if(pair.Value.Title=="Steam")SetWindowText(pair.Key,"Steam");}catch(ArgumentException){}catch(COMException){}catch(InvalidOperationException){}}windows.Clear();}
  public void Dispose(){Restore();small.Dispose();large.Dispose();}
 }
}
