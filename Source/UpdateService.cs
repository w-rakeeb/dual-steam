using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;

namespace DualSteam {
 public sealed class UpdateRateLimitException:IOException {public UpdateRateLimitException():base("GitHub's request limit was reached. Automatic checks will retry later; you can also download the release from GitHub.") {}}
 public sealed class ReleaseInfo {public string Tag,Page,Download,Sha256;public Version Version;public long Size;}
 public sealed class UpdateRequest {public string Target,Package,Sha256,Version;public int ManagerPid;public long ManagerStartTicks;public bool Restart=true;}
 public static class UpdateService {
  public const string Repository="w-rakeeb/dual-steam",CurrentVersion="3.3.3",AssetName="Dual-Steam-Windows.zip";
  public static Version ParseVersion(string text){Version version;if(String.IsNullOrEmpty(text)||!System.Text.RegularExpressions.Regex.IsMatch(text,"^v?[0-9]+\\.[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$")||!Version.TryParse(text.TrimStart('v'),out version))throw new IOException("The release version is not supported.");return version;}
  public static bool TrustedAsset(string text){Uri uri;return Uri.TryCreate(text,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)&&String.IsNullOrEmpty(uri.UserInfo)&&String.IsNullOrEmpty(uri.Query)&&String.IsNullOrEmpty(uri.Fragment);}
  static HttpWebResponse Open(string address){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   for(int redirects=0;redirects<6;redirects++){
    var uri=new Uri(address);if(uri.Scheme!="https"||!new[]{"api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com","github-releases.githubusercontent.com"}.Contains(uri.Host))throw new IOException("The update redirected to an unexpected server.");
    var request=(HttpWebRequest)WebRequest.Create(uri);request.UserAgent="DualSteam/"+CurrentVersion;request.Accept="application/vnd.github+json";request.Timeout=15000;request.ReadWriteTimeout=15000;request.AllowAutoRedirect=false;request.CachePolicy=new System.Net.Cache.HttpRequestCachePolicy(System.Net.Cache.HttpRequestCacheLevel.NoCacheNoStore);
    var response=(HttpWebResponse)request.GetResponse();int code=(int)response.StatusCode;if(code>=300&&code<400){string location=response.Headers["Location"];response.Dispose();address=new Uri(uri,location).AbsoluteUri;continue;}return response;
   }throw new IOException("Too many update redirects.");
  }
  public static ReleaseInfo GetLatest(){
   try{using(var response=Open("https://api.github.com/repos/"+Repository+"/releases/latest"))using(var reader=new StreamReader(response.GetResponseStream()))return ParseRelease(reader.ReadToEnd());}
   catch(WebException ex){var response=ex.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)throw new IOException("No public release has been published yet.");if(response!=null&&((int)response.StatusCode==403||(int)response.StatusCode==429))throw new UpdateRateLimitException();throw new IOException("GitHub could not be reached. Check your connection and try again.");}
  }
  public static ReleaseInfo ParseRelease(string json){
   var obj=new JavaScriptSerializer{MaxJsonLength=2000000}.Deserialize<Dictionary<string,object>>(json);if(obj==null||!obj.ContainsKey("tag_name")||Convert.ToBoolean(obj["draft"])||Convert.ToBoolean(obj["prerelease"]))throw new IOException("No stable update is available.");
   var version=ParseVersion(Convert.ToString(obj["tag_name"]));var assets=(System.Collections.IEnumerable)obj["assets"];
   foreach(Dictionary<string,object> asset in assets)if(Convert.ToString(asset["name"])==AssetName){string url=Convert.ToString(asset["browser_download_url"]);string digest=asset.ContainsKey("digest")?Convert.ToString(asset["digest"]):"";long size=Convert.ToInt64(asset["size"]);if(!TrustedAsset(url)||!System.Text.RegularExpressions.Regex.IsMatch(digest,"^sha256:[0-9a-fA-F]{64}$")||size<=0||size>50*1024*1024)throw new IOException("The release is missing a valid package checksum or download.");return new ReleaseInfo{Tag=Convert.ToString(obj["tag_name"]),Version=version,Page="https://github.com/"+Repository+"/releases/tag/"+Uri.EscapeDataString(Convert.ToString(obj["tag_name"])),Download=url,Sha256=digest.Substring(7),Size=size};}
   throw new IOException("This release does not include a Windows update package.");
  }
  public static string Download(ReleaseInfo release){
   if(!TrustedAsset(release.Download))throw new IOException("Unexpected release download address.");string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,AssetName);
   using(var response=Open(release.Download))using(var input=response.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write)){var buffer=new byte[65536];long total=0;int count;while((count=input.Read(buffer,0,buffer.Length))>0){total+=count;if(total>release.Size||total>50*1024*1024)throw new IOException("The download size does not match the release.");output.Write(buffer,0,count);}if(total!=release.Size)throw new IOException("The update download is incomplete.");}
   UpdatePackage.VerifyHash(file,release.Sha256);UpdatePackage.Validate(file);return file;
  }
 }
}
