using System;using System.Linq;using System.Diagnostics;
public sealed class PerformanceSnapshot {
 public long Memory,Started;public double Cpu;public int Count,Root;public bool Ready;
 public static PerformanceSnapshot Main(){string folder=System.IO.Path.GetDirectoryName(Engine.Current==null?Engine.DetectSteam():Engine.Current.MainSteam)+"\\";var all=new[]{"steam","steamwebhelper","gameoverlayui64"}.SelectMany(n=>Process.GetProcessesByName(n)).Where(p=>Engine.Image(p).StartsWith(folder,StringComparison.OrdinalIgnoreCase)).ToArray();var result=new PerformanceSnapshot{Count=all.Length,Ready=all.Length>0};foreach(var p in all)try{result.Memory+=p.WorkingSet64;result.Cpu+=p.TotalProcessorTime.TotalMilliseconds;if(p.ProcessName=="steam"){result.Root=p.Id;result.Started=p.StartTime.ToUniversalTime().Ticks;}}catch{}return result;}
 public static PerformanceSnapshot Second(NativeStatus s){return new PerformanceSnapshot{Memory=s.Memory,Cpu=s.Cpu,Started=s.Started,Count=s.Count,Root=(s.RootIds??new int[0]).FirstOrDefault(),Ready=s.WindowReady};}
}
