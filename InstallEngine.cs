using System;using System.Diagnostics;using System.IO;using System.Threading.Tasks;using SOACS.Arsenal.Models;
namespace SOACS.Arsenal.Services
{
 public class InstallEngine
 {
  public Task<int> InstallAsync(PatchItem p)=>Task.Run(()=>{string exe,args;Build(p,out exe,out args);var psi=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(p.FullPath)};using(var x=Process.Start(psi)){x.WaitForExit();return x.ExitCode;}});
  void Build(PatchItem p,out string exe,out string args){string q="\""+p.FullPath+"\"",n=p.FileName.ToLowerInvariant();
   if(p.Category=="Store App (All Users)"||p.Category=="Store Dependency"){exe="dism.exe";args="/Online /Add-ProvisionedAppxPackage /PackagePath:"+q+" /SkipLicense /Quiet";return;}
   if(p.Category=="BIOS / Firmware"){exe=p.FullPath;if(n.Contains("dell"))args="/s /r /p=";else if(n.Contains("lenovo"))args="/SILENT";else if(n.Contains("hp"))args="-s";else args="/s";return;}
   if(p.Extension==".msu"){exe="wusa.exe";args=q+" /quiet /norestart";return;}if(p.Extension==".cab"){exe="dism.exe";args="/online /add-package /packagepath:"+q+" /quiet /norestart";return;}if(p.Extension==".msi"){exe="msiexec.exe";args="/i "+q+" /qn /norestart";return;}if(p.Extension==".msp"){exe="msiexec.exe";args="/p "+q+" /qn /norestart";return;}exe=p.FullPath;if(!string.IsNullOrWhiteSpace(p.SilentArguments)){args=p.SilentArguments;return;}if(n.Contains("adobe")||n.Contains("acro"))args="/sAll /rs /rps /msi /norestart /quiet";else if(n.Contains("vc_redist")||n.Contains("vcredist"))args="/install /quiet /norestart";else if(n.Contains("chrome")||n.Contains("edge"))args="/silent /install";else args="/quiet /norestart";
  }
 }
}
