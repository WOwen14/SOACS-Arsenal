using System;using System.Diagnostics;using SOACS.Arsenal.Models;
namespace SOACS.Arsenal.Services
{
 public class FirmwareValidator
 {
  public bool Validate(PatchItem p,MachineQualification q,out string reason){if(q==null||!q.FirmwareReady){reason="Machine qualification did not pass: "+(q?.Details??"not assessed");return false;}try{var v=FileVersionInfo.GetVersionInfo(p.FullPath);string evidence=((v.CompanyName??"")+" "+(v.ProductName??"")+" "+p.FileName).ToLowerInvariant();string maker=(q.Manufacturer??"").ToLowerInvariant();bool vendor=(maker.Contains("dell")&&evidence.Contains("dell"))||(maker.Contains("hp")&&(evidence.Contains("hp")||evidence.Contains("hewlett")))||(maker.Contains("lenovo")&&evidence.Contains("lenovo"))||(maker.Contains("getac")&&evidence.Contains("getac"))||(maker.Contains("panasonic")&&evidence.Contains("panasonic"));if(!vendor){reason="Package vendor could not be matched to this computer manufacturer ("+q.Manufacturer+").";return false;}reason="Vendor matched. Model applicability must be confirmed by the operator before execution.";return true;}catch(Exception ex){reason="Firmware metadata could not be validated: "+ex.Message;return false;}}
 }
}
