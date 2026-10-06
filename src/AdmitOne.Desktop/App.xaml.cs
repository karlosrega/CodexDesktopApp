using System;
using System.Windows;
using AdmitOne;

namespace AdmitOne.Desktop
{
    public partial class App : Application
    {
        public ApplicationService Service { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException+=(sender,args)=> { MessageBox.Show(ErrorLog.Describe(args.Exception),"Aviso",MessageBoxButton.OK,MessageBoxImage.Error); args.Handled=true; };
            try { Service=new ApplicationService(new Database(LocalConfiguration.Load())); ShowLogin(); }
            catch(Exception ex) { MessageBox.Show(ErrorLog.Describe(ex),"Configuración",MessageBoxButton.OK,MessageBoxImage.Error); Shutdown(1); }
        }
        public void ShowLogin()
        {
            var next=new LoginWindow(Service); MainWindow=next; next.Show();
        }
        public void ShowAdministration(Session session,Window previous)
        {
            var next=new AdministrationWindow(Service,session); MainWindow=next; next.Show(); previous.Close();
        }
        public void EndSession(Window previous)
        {
            ShowLogin(); previous.Close();
        }
    }
}
