using System;using System.IO;using System.IO.Pipes;using System.Threading.Tasks;using System.Diagnostics;using System.Runtime.InteropServices;using System.Security.AccessControl;using System.Security.Principal;
public static class ManagerSessionHost {
 static volatile bool authorized;static string pipeName;static Task server;
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(IntPtr pipe,out uint pid);
 public static void Revoke(){authorized=false;}
 public static string Authorize(){if(server==null){pipeName="DualSteamSession_"+Guid.NewGuid().ToString("N");server=Task.Run(()=>Serve());}authorized=true;return pipeName;}
 static bool Read(Stream pipe,byte[] bytes,int length){int at=0;var deadline=Stopwatch.StartNew();while(at<length){var read=pipe.ReadAsync(bytes,at,length-at);int remaining=1500-(int)deadline.ElapsedMilliseconds;if(remaining<=0||!read.Wait(remaining)||read.Result==0)return false;at+=read.Result;}return true;}
 static async Task Serve(){
  var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-11"),PipeAccessRights.ReadWrite,AccessControlType.Allow));
  while(true){try{using(var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,64,64,security)){await pipe.WaitForConnectionAsync();var nonce=new byte[16];if(!Read(pipe,nonce,nonce.Length))continue;var response=new byte[17];response[0]=authorized?(byte)1:(byte)0;Buffer.BlockCopy(nonce,0,response,1,16);var write=pipe.WriteAsync(response,0,response.Length);write.Wait(1500);}}catch{System.Threading.Thread.Sleep(100);}}
 }
 public static void Confirm(AuthorizedSession session){using(var pipe=new NamedPipeClientStream(".",session.Pipe,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification)){
  pipe.Connect(2000);uint pid;if(!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(),out pid)||pid!=session.ManagerPid)throw new IOException("The session endpoint is not owned by the running manager.");byte[] nonce=Guid.NewGuid().ToByteArray();var write=pipe.WriteAsync(nonce,0,nonce.Length);if(!write.Wait(1500))throw new IOException("The manager session did not respond.");var answer=new byte[17];if(!Read(pipe,answer,answer.Length)||answer[0]!=1)throw new IOException("This manager startup is not authorized.");for(int i=0;i<nonce.Length;i++)if(answer[i+1]!=nonce[i])throw new IOException("Invalid manager session response.");
 }}
}
