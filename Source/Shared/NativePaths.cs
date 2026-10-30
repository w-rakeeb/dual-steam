using System;using System.IO;using System.Web.Script.Serialization;using System.Security.Principal;
public sealed class ControllerIdentity {public string SID,IssuerSID,Profile,Root,Home,Pipe,User;}
public static class NativePaths {
 public static string Root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;
 public static string Home=Directory.GetParent(Root).FullName;
 public static ControllerIdentity Identity;
 public static void Initialize(string home){Home=Path.GetFullPath(home).TrimEnd('\\');Identity=new JavaScriptSerializer().Deserialize<ControllerIdentity>(File.ReadAllText(Path.Combine(Home,@"Data\controller.json")));if(Identity==null||!String.Equals(Identity.Home,Home,StringComparison.OrdinalIgnoreCase)||!String.Equals(Identity.Root,Root,StringComparison.OrdinalIgnoreCase)||!String.Equals(Identity.Profile,Path.Combine(Root,"Windows Profile"),StringComparison.OrdinalIgnoreCase)||Identity.SID!=WindowsIdentity.GetCurrent().User.Value||Identity.Pipe==null||!System.Text.RegularExpressions.Regex.IsMatch(Identity.Pipe,@"^WrakeebDualSteamNativeControl(?:_[a-f0-9]{12})?$"))throw new IOException("The background controller identity or location does not match this setup.");new SecurityIdentifier(Identity.IssuerSID);}
}
