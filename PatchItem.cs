using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
namespace SOACS.Arsenal.Models
{
 public class PatchItem : INotifyPropertyChanged
 {
  string _status="Queued",_result="Waiting",_category="Unknown",_hashStatus="Not Checked"; int _progress,_sequence;
  public string FileName{get;set;} public string FullPath{get;set;} public string Extension{get;set;} public long SizeBytes{get;set;}
  public string SizeText=>SizeBytes<1048576?(SizeBytes/1024.0).ToString("0.0")+" KB":(SizeBytes/1048576.0).ToString("0.0")+" MB";
  public string Category{get=>_category;set{_category=value;Changed();}} public bool CanAutomate{get;set;} public bool RequiresApproval{get;set;} public string CommandPreview{get;set;} public string SilentArguments{get;set;} public string DependencyGroup{get;set;} public int Priority{get;set;}
  public string Publisher{get;set;} public string ProductName{get;set;} public string FileVersion{get;set;} public string RuleId{get;set;} public string RuleName{get;set;}
  public string ExpectedHash{get;set;} public string CalculatedHash{get;set;} public bool HashProvided{get;set;} public bool HashValid{get;set;}
  public string HashStatus{get=>_hashStatus;set{_hashStatus=value;Changed();Changed("HashBrush");}}
  public int Sequence{get=>_sequence;set{_sequence=value;Changed();}}
  public string Status{get=>_status;set{_status=value;Changed();Changed("StatusBrush");}} public string Result{get=>_result;set{_result=value;Changed();}}
  public int Progress{get=>_progress;set{_progress=value;Changed();}}
  public Brush StatusBrush{get{if(Status=="Completed")return Brushes.LimeGreen;if(Status=="Failed"||Status=="Blocked")return Brushes.Red;if(Status=="Manual")return Brushes.Orange;if(Status=="Installing"||Status=="Approval Required")return Brushes.Gold;if(Status=="Reboot Required")return Brushes.Yellow;if(Status=="Skipped")return Brushes.Gray;return Brushes.LightGray;}}
  public Brush HashBrush{get{if(HashStatus=="Verified")return Brushes.LimeGreen;if(HashStatus=="Mismatch"||HashStatus=="Invalid Manifest")return Brushes.Red;if(HashStatus=="Not Provided")return Brushes.Gold;return Brushes.LightGray;}}
  public event PropertyChangedEventHandler PropertyChanged; void Changed([CallerMemberName]string n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
 }
}
