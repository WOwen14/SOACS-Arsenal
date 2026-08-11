using System;using System.IO;using System.Windows;using SOACS.Arsenal.Models;
namespace SOACS.Arsenal
{
 public partial class TeachRuleWindow:Window
 {
  public ApplicationRule Rule{get;private set;} public string PackageName{get;private set;}
  public TeachRuleWindow(PatchItem p){InitializeComponent();PackageName=p.FileName;var stem=Path.GetFileNameWithoutExtension(p.FileName);Rule=new ApplicationRule{Id="CUSTOM-"+Guid.NewGuid().ToString("N").Substring(0,10).ToUpperInvariant(),Name=string.IsNullOrWhiteSpace(p.ProductName)?stem:p.ProductName,PublisherContains=p.Publisher??"",ProductContains=p.ProductName??"",FileNameContains=BestToken(stem),Category="Application",SilentArguments="/S",VerifyDisplayNameContains=p.ProductName??stem,Priority=800,RequiresApproval=false,RebootBehavior="ExitCode",Enabled=true};DataContext=this;}
  static string BestToken(string s){if(string.IsNullOrWhiteSpace(s))return "";var dash=s.IndexOf('-');return dash>2?s.Substring(0,dash):s;}
  void Save_Click(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(Rule.Name)||string.IsNullOrWhiteSpace(Rule.FileNameContains)||string.IsNullOrWhiteSpace(Rule.SilentArguments)){MessageBox.Show("Rule name, filename match, and silent arguments are required.","Teach Arsenal",MessageBoxButton.OK,MessageBoxImage.Warning);return;}DialogResult=true;}
  void Cancel_Click(object s,RoutedEventArgs e){DialogResult=false;}
 }
}
