using System;using System.IO;
public static class AccountAppearanceTests {
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static bool Refuses(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(System.IO.IOException){return true;}}
 public static int Main(){try{
  Check(new AccountAppearanceOptions().Hidden,"new account appearance hides Windows sign-in by default");
  Check(AccountAppearance.ValidateName("  Second Steam  ")=="Second Steam","display name trimmed without changing login identity");
  Check(AccountAppearance.ValidateName("আমার Steam ♥")=="আমার Steam ♥","Unicode display names supported");
  Check(Refuses(()=>AccountAppearance.ValidateName("  ")),"empty display name rejected");
  Check(Refuses(()=>AccountAppearance.ValidateName(new string('a',81))),"oversized display name rejected");
  Check(Refuses(()=>AccountAppearance.ValidateName("Steam\nHidden")),"multiline display name rejected");
  string sid="S-1-5-21-1-2-3-1002",owner="S-1-5-21-1-2-3-1001";
  AccountAppearance.ValidateIdentity("DualSteam_1234abcd",sid,owner,sid);Check(true,"managed account identity accepted");
  AccountAppearance.ValidateIdentity("DualSteamLocal",sid,owner,sid);Check(true,"existing prototype account supported");
  Check(Refuses(()=>AccountAppearance.ValidateIdentity("Alice",sid,owner,sid)),"unrelated Windows account rejected");
  Check(Refuses(()=>AccountAppearance.ValidateIdentity("DualSteam_1234abcd",owner,owner,owner)),"setup owner cannot be hidden or renamed");
  Check(Refuses(()=>AccountAppearance.ValidateIdentity("DualSteam_1234abcd",sid,owner,owner)),"reused account name with changed SID rejected");
  string script=Path.Combine(Engine.Home,"Verification","AppearanceHost-"+Guid.NewGuid().ToString("N")+".ps1");Directory.CreateDirectory(Path.GetDirectoryName(script));try{
   File.WriteAllText(script,"$a=[Reflection.Assembly]::LoadFrom('"+typeof(AccountAppearance).Assembly.Location.Replace("'","''")+"')\n$a.GetType('AccountAppearance').GetProperty('Home',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null,$null)");
   string home=Engine.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -File "+Engine.Quote(script)).Trim();
   Check(home.Equals(Path.GetDirectoryName(typeof(AccountAppearance).Assembly.Location),StringComparison.OrdinalIgnoreCase),"elevated PowerShell resolves data relative to the app instead of its host executable");
  }finally{File.Delete(script);}
  Console.WriteLine(passed+" account appearance checks passed.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
