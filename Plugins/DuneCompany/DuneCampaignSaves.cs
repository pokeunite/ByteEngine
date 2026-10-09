namespace DuneCompany;
/// <summary>Campaign reset intentionally preserves the player's blueprint library and preferences.</summary>
internal static class DuneCampaignSaves
{
 internal static readonly string[] CampaignFiles=["vehicle.json","active-blueprint.txt","tow-progress-v1.json","winch-progress-v1.json","recovery-history.json","winch-history.json","recovery-payment.json","workshop-tutorial.txt", "workshop-tutorial.json"];
 internal static void Reset(string root)
 {
  Directory.CreateDirectory(root);
  var existing=CampaignFiles.Where(name=>File.Exists(Path.Combine(root,name))).ToArray();
  if(existing.Length==0)return;
  string backup=Path.Combine(root,"Backups","campaign-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
  // Complete the backup before changing any campaign file.
  foreach(var name in existing)File.Copy(Path.Combine(root,name),Path.Combine(backup,name));
  try{foreach(var name in existing){File.Delete(Path.Combine(root,name));File.Delete(Path.Combine(root,name+".tmp"));}}
  catch{foreach(var name in existing)File.Copy(Path.Combine(backup,name),Path.Combine(root,name),true);throw;}
 }
}
