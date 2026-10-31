using System;
public sealed class UpdateFeedback {
 public string Title="Ready to check for updates",Detail="Check GitHub for the latest stable release.",Kind="idle";public DateTime? LastChecked;
 public void Begin(){Kind="checking";Title="Checking for updates…";Detail="Contacting GitHub and checking the stable release.";}
 public void Complete(Version installed,Version latest){LastChecked=DateTime.Now;Kind=latest>installed?"available":latest<installed?"ahead":"current";Title=Kind=="available"?"An update is available":Kind=="ahead"?"Installed version is ahead of the public release":"You’re up to date";Detail="Installed: "+installed.ToString(3)+"  ·  Latest stable: "+latest.ToString(3)+(Kind=="available"?". Use Install update to continue.":".");}
 public void Fail(string error){Kind="error";LastChecked=DateTime.Now;Title="Couldn’t check for updates";Detail=error+" You can try again.";}
}
