using System;
using System.Windows;
using System.Windows.Threading;
namespace SOACS.Arsenal
{
 public partial class App : Application
 {
  private SplashWindow _splash;
  private void Application_Startup(object sender, StartupEventArgs e)
  {
   _splash = new SplashWindow(); _splash.Show();
   var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
   timer.Tick += delegate { timer.Stop(); var main = new MainWindow(); MainWindow = main; main.Show(); _splash.Close(); };
   timer.Start();
  }
 }
}
