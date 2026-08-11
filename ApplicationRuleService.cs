using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Xml.Linq;using SOACS.Arsenal.Models;
namespace SOACS.Arsenal.Services
{
 public class ApplicationRuleService
 {
  readonly string _path; readonly List<ApplicationRule> _rules=new List<ApplicationRule>();
  public string RulesPath=>_path; public IReadOnlyList<ApplicationRule> Rules=>_rules;
  public ApplicationRuleService(){var dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Rules");Directory.CreateDirectory(dir);_path=Path.Combine(dir,"ApplicationRules.xml");EnsureDefaults();Load();}
  void EnsureDefaults(){if(File.Exists(_path))return;new XDocument(new XElement("ApplicationRules",
   Rule("JAVA8-X64","Oracle Java 8 x64","Oracle","Java","jre-","Java Runtime","/s REBOOT=Suppress AUTO_UPDATE=Disable SPONSORS=Disable",320,"Java"),
   Rule("JAVA8-X86","Oracle Java 8 x86","Oracle","Java","jre-","Java Runtime","/s REBOOT=Suppress AUTO_UPDATE=Disable SPONSORS=Disable",321,"Java"),
   Rule("VLC","VLC media player","VideoLAN","VLC","vlc-","VLC","/S",780,"VLC media player"),
   Rule("NMAP","Nmap","Nmap","Nmap","nmap-","Nmap","/S",790,"Nmap"),
   Rule("7ZIP","7-Zip","Igor Pavlov","7-Zip","7z","7-Zip","/S",760,"7-Zip"),
   Rule("NOTEPADPP","Notepad++","Notepad++","Notepad++","npp.","Notepad++","/S",770,"Notepad++"),
   Rule("FIREFOX","Mozilla Firefox","Mozilla","Firefox","firefox","Firefox","-ms",820,"Mozilla Firefox")
  )).Save(_path);}
  static XElement Rule(string id,string name,string publisher,string product,string file,string category,string args,int priority,string verify)=>new XElement("Rule",new XAttribute("Id",id),new XAttribute("Enabled","true"),new XElement("Name",name),new XElement("PublisherContains",publisher),new XElement("ProductContains",product),new XElement("FileNameContains",file),new XElement("Category",category),new XElement("SilentArguments",args),new XElement("VerifyDisplayNameContains",verify),new XElement("Priority",priority),new XElement("RequiresApproval","false"),new XElement("RebootBehavior","ExitCode"));
  public void Load(){_rules.Clear();var doc=XDocument.Load(_path);foreach(var x in doc.Root.Elements("Rule")){_rules.Add(new ApplicationRule{Id=(string)x.Attribute("Id")??Guid.NewGuid().ToString("N"),Enabled=((string)x.Attribute("Enabled")??"true").Equals("true",StringComparison.OrdinalIgnoreCase),Name=(string)x.Element("Name")??"Unnamed",PublisherContains=(string)x.Element("PublisherContains")??"",ProductContains=(string)x.Element("ProductContains")??"",FileNameContains=(string)x.Element("FileNameContains")??"",Category=(string)x.Element("Category")??"Application",SilentArguments=(string)x.Element("SilentArguments")??"",VerifyDisplayNameContains=(string)x.Element("VerifyDisplayNameContains")??"",Priority=(int?)x.Element("Priority")??800,RequiresApproval=(bool?)x.Element("RequiresApproval")??false,RebootBehavior=(string)x.Element("RebootBehavior")??"ExitCode"});}}
  public ApplicationRule Match(PatchItem p){return _rules.Where(r=>r.Enabled).Select(r=>new{Rule=r,Score=Score(r,p)}).Where(x=>x.Score>0).OrderByDescending(x=>x.Score).ThenBy(x=>x.Rule.Priority).Select(x=>x.Rule).FirstOrDefault();}
  static int Score(ApplicationRule r,PatchItem p){int score=0;bool has=false;if(!string.IsNullOrWhiteSpace(r.FileNameContains)){has=true;if(Has(p.FileName,r.FileNameContains))score+=60;else return 0;}if(!string.IsNullOrWhiteSpace(r.PublisherContains)){has=true;if(Has(p.Publisher,r.PublisherContains))score+=30;}if(!string.IsNullOrWhiteSpace(r.ProductContains)){has=true;if(Has(p.ProductName,r.ProductContains))score+=30;}return has?score:0;}
  static bool Has(string value,string token)=>!string.IsNullOrWhiteSpace(value)&&value.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0;
  public void SaveRule(ApplicationRule rule){var doc=XDocument.Load(_path);var existing=doc.Root.Elements("Rule").FirstOrDefault(x=>string.Equals((string)x.Attribute("Id"),rule.Id,StringComparison.OrdinalIgnoreCase));var node=new XElement("Rule",new XAttribute("Id",rule.Id),new XAttribute("Enabled",rule.Enabled.ToString().ToLowerInvariant()),new XElement("Name",rule.Name??""),new XElement("PublisherContains",rule.PublisherContains??""),new XElement("ProductContains",rule.ProductContains??""),new XElement("FileNameContains",rule.FileNameContains??""),new XElement("Category",rule.Category??"Application"),new XElement("SilentArguments",rule.SilentArguments??""),new XElement("VerifyDisplayNameContains",rule.VerifyDisplayNameContains??""),new XElement("Priority",rule.Priority),new XElement("RequiresApproval",rule.RequiresApproval),new XElement("RebootBehavior",rule.RebootBehavior??"ExitCode"));if(existing==null)doc.Root.Add(node);else existing.ReplaceWith(node);doc.Save(_path);Load();}
 }
}
