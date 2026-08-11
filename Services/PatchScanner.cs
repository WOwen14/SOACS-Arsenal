using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using SOACS.Arsenal.Models;
namespace SOACS.Arsenal.Services
{
 public class PatchScanner
 {
  static readonly string[] Supported={".msu",".cab",".msi",".msp",".exe",".appx",".appxbundle",".msix",".msixbundle"}; readonly ApplicationRuleService _rules;
  public PatchScanner(ApplicationRuleService rules){_rules=rules;}
  public List<PatchItem> Scan(string folder){var o=new List<PatchItem>();if(string.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return o;var hashes=new HashValidationService(folder);foreach(var path in Directory.GetFiles(folder,"*.*",SearchOption.AllDirectories)){var ext=Path.GetExtension(path).ToLowerInvariant();if(!Supported.Contains(ext))continue;var f=new FileInfo(path);var p=new PatchItem{FileName=f.Name,FullPath=f.FullName,Extension=ext,SizeBytes=f.Length};ReadMetadata(p);Classify(p);hashes.Validate(p);ApplyHashGate(p);o.Add(p);}return BuildPlan(o);}
  static void ReadMetadata(PatchItem p){try{var v=FileVersionInfo.GetVersionInfo(p.FullPath);p.Publisher=v.CompanyName??"";p.ProductName=v.ProductName??v.FileDescription??"";p.FileVersion=v.FileVersion??v.ProductVersion??"";}catch{p.Publisher="";p.ProductName="";p.FileVersion="";}}
  List<PatchItem> BuildPlan(List<PatchItem> items){var sorted=items.OrderBy(x=>x.Priority).ThenBy(x=>x.Category).ThenBy(x=>x.FileName).ToList();for(int i=0;i<sorted.Count;i++)sorted[i].Sequence=i+1;return sorted;}
  void Classify(PatchItem p){string n=p.FileName.ToLowerInvariant(),dir=(Path.GetDirectoryName(p.FullPath)??"").ToLowerInvariant();
   if(IsFirmware(n,dir)){Set(p,"BIOS / Firmware",true,"Vendor firmware installer; guarded approval required",900,"Firmware",true);return;}
   if(IsStore(p.Extension)){bool dependency=Contains(n,"vclibs","ui.xaml","native.runtime","native.framework")||dir.Contains("dependenc");Set(p,dependency?"Store Dependency":"Store App (All Users)",true,"DISM /Online /Add-ProvisionedAppxPackage",dependency?350:400,dependency?"StoreFramework":"StoreApp",false);return;}
   if(p.Extension==".msu"){Set(p,"Windows Update",true,"wusa.exe \"{file}\" /quiet /norestart",200,"Windows",false);return;}
   if(p.Extension==".cab"){Set(p,"Windows CAB",true,"dism.exe /online /add-package /packagepath:\"{file}\" /quiet /norestart",180,"Windows",false);return;}
   if(Contains(n,"servicingstack","ssu")){Set(p,"Servicing Stack",true,"Windows servicing prerequisite",100,"WindowsPrereq",false);return;}
   if(Contains(n,"vc_redist","vcredist")){p.SilentArguments="/install /quiet /norestart";Set(p,"VC++ Runtime",true,p.SilentArguments,250,"Runtime",false);return;}
   if(Contains(n,"ndp","dotnet","windowsdesktop-runtime")){p.SilentArguments="/quiet /norestart";Set(p,".NET",true,p.SilentArguments,275,"Runtime",false);return;}
   if(p.Extension==".msi"){Set(p,Contains(n,"chrome")?"Chrome":Contains(n,"edge")?"Microsoft Edge":Contains(n,"adobe","acro")?"Adobe":Contains(n,"sql")?"SQL":"MSI Package",true,"msiexec.exe /i \"{file}\" /qn /norestart",Contains(n,"sql")?600:Contains(n,"adobe")?700:750,"Application",false);return;}
   if(p.Extension==".msp"){Set(p,Contains(n,"adobe","acro")?"Adobe":"MSP Patch",true,"msiexec.exe /p \"{file}\" /qn /norestart",700,"Application",false);return;}
   var rule=_rules.Match(p);if(rule!=null){p.RuleId=rule.Id;p.RuleName=rule.Name;p.SilentArguments=rule.SilentArguments;Set(p,rule.Category,true,"\"{file}\" "+rule.SilentArguments,rule.Priority,"ApplicationRule",rule.RequiresApproval);p.Result="Matched application rule: "+rule.Name;return;}
   if(Contains(n,"sqlserver","sql202","sql201","ssms")){Set(p,"SQL",false,"Manual review: edition-specific SQL rule not configured",600,"SQL",false);return;}
   if(Contains(n,"chrome")){p.SilentArguments="/silent /install";Set(p,"Chrome",true,p.SilentArguments,800,"Browser",false);return;}
   if(Contains(n,"edge")){p.SilentArguments="/silent /install";Set(p,"Microsoft Edge",true,p.SilentArguments,800,"Browser",false);return;}
   if(Contains(n,"adobe","acrobat","acrordr")){p.SilentArguments="/sAll /rs /rps /msi /norestart /quiet";Set(p,"Adobe",true,p.SilentArguments,700,"Application",false);return;}
   Set(p,"Unknown EXE",false,"Teach Arsenal the silent install rule for this application",950,"Manual",false);
  }
  static void ApplyHashGate(PatchItem p){if(p.HashStatus=="Mismatch"||p.HashStatus=="Invalid Manifest"){p.CanAutomate=false;p.Status="Blocked";p.Result="INSTALL BLOCKED: "+p.HashStatus+". Expected "+Short(p.ExpectedHash)+" calculated "+Short(p.CalculatedHash);return;}if(p.HashStatus=="Verified")p.Result=(p.Result??"")+" | SHA-256 verified";else if(p.HashStatus=="Not Provided")p.Result=(p.Result??"")+" | No hash supplied";}
  static string Short(string v)=>string.IsNullOrWhiteSpace(v)?"not available":(v.Length>16?v.Substring(0,16)+"...":v);
  static bool IsStore(string e)=>e==".appx"||e==".appxbundle"||e==".msix"||e==".msixbundle";
  static bool IsFirmware(string n,string d)=>Contains(n,"bios","firmware","uefi","thunderbolt","tpm","dock_fw","dockfirmware")||d.Contains("firmware")||d.Contains("bios");
  static bool Contains(string v,params string[] t){foreach(var x in t)if(v.Contains(x))return true;return false;}
  static void Set(PatchItem p,string c,bool a,string cmd,int pri,string group,bool approval){p.Category=c;p.CanAutomate=a;p.CommandPreview=cmd;p.Priority=pri;p.DependencyGroup=group;p.RequiresApproval=approval;p.Status=a?(approval?"Approval Required":"Queued"):"Manual";p.Result=a?(approval?"Safety validation and operator approval required":"Ready for planned installation"):"Human intervention required";}
 }
}
