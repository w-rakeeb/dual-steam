using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
public static class DesktopAccess {
 [DllImport("user32")]static extern IntPtr GetProcessWindowStation();
 [DllImport("user32")]static extern IntPtr GetThreadDesktop(uint id);
 [DllImport("kernel32")]static extern uint GetCurrentThreadId();
 [DllImport("user32",SetLastError=true)]static extern bool GetUserObjectSecurity(IntPtr handle,ref uint info,byte[] buffer,uint length,out uint needed);
 [DllImport("user32",SetLastError=true)]static extern bool SetUserObjectSecurity(IntPtr handle,ref uint info,byte[] buffer);
 static void GrantObject(IntPtr handle,SecurityIdentifier sid,int mask){
  uint info=4,needed;GetUserObjectSecurity(handle,ref info,null,0,out needed);
  if(needed==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  var buffer=new byte[needed];if(!GetUserObjectSecurity(handle,ref info,buffer,needed,out needed))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  var descriptor=new RawSecurityDescriptor(buffer,0);
  foreach(GenericAce entry in descriptor.DiscretionaryAcl){var ace=entry as CommonAce;if(ace!=null&&ace.SecurityIdentifier==sid&&(ace.AccessMask&mask)==mask)return;}
  descriptor.DiscretionaryAcl.InsertAce(descriptor.DiscretionaryAcl.Count,new CommonAce(AceFlags.None,AceQualifier.AccessAllowed,mask,sid,false,null));
  var updated=new byte[descriptor.BinaryLength];descriptor.GetBinaryForm(updated,0);
  if(!SetUserObjectSecurity(handle,ref info,updated))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
 }
 public static void Grant(string sid){var identity=new SecurityIdentifier(sid);GrantObject(GetProcessWindowStation(),identity,0x000F037F);GrantObject(GetThreadDesktop(GetCurrentThreadId()),identity,0x000F01FF);}
 static void RevokeObject(IntPtr handle,SecurityIdentifier sid){uint info=4,needed;GetUserObjectSecurity(handle,ref info,null,0,out needed);if(needed==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var buffer=new byte[needed];if(!GetUserObjectSecurity(handle,ref info,buffer,needed,out needed))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var descriptor=new RawSecurityDescriptor(buffer,0);for(int i=descriptor.DiscretionaryAcl.Count-1;i>=0;i--){var ace=descriptor.DiscretionaryAcl[i] as CommonAce;if(ace!=null&&ace.SecurityIdentifier==sid&&ace.AceQualifier==AceQualifier.AccessAllowed)descriptor.DiscretionaryAcl.RemoveAce(i);}var updated=new byte[descriptor.BinaryLength];descriptor.GetBinaryForm(updated,0);if(!SetUserObjectSecurity(handle,ref info,updated))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
 public static void Revoke(string sid){var identity=new SecurityIdentifier(sid);if(identity==WindowsIdentity.GetCurrent().User)throw new InvalidOperationException("Cannot revoke the active desktop user.");RevokeObject(GetProcessWindowStation(),identity);RevokeObject(GetThreadDesktop(GetCurrentThreadId()),identity);}
}
