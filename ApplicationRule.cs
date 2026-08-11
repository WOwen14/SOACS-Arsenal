using System;
namespace SOACS.Arsenal.Models
{
 public class ApplicationRule
 {
  public string Id{get;set;}
  public string Name{get;set;}
  public string PublisherContains{get;set;}
  public string ProductContains{get;set;}
  public string FileNameContains{get;set;}
  public string Category{get;set;}
  public string SilentArguments{get;set;}
  public string VerifyDisplayNameContains{get;set;}
  public int Priority{get;set;}
  public bool RequiresApproval{get;set;}
  public string RebootBehavior{get;set;}
  public bool Enabled{get;set;}
 }
}
