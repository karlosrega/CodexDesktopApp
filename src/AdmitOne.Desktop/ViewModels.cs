using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using AdmitOne;

namespace AdmitOne.Desktop
{
    public abstract class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Changed([CallerMemberName]string name=null) { PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name)); }
        private bool busy; private string notice="";
        public bool Busy { get { return busy; } set { busy=value; Changed(); CommandManager.InvalidateRequerySuggested(); } }
        public string Notice { get { return notice; } set { notice=value; Changed(); } }
        public async Task<bool> Run(Func<Task> action)
        {
            if(Busy) return false;
            Busy=true; Notice="";
            try { await action(); return true; }
            catch(Exception e) { Notice=ErrorLog.Describe(e); return false; }
            finally { Busy=false; }
        }
    }
    public sealed class ActionCommand : ICommand
    {
        private readonly Action action; private readonly Func<bool> enabled;
        public ActionCommand(Action action,Func<bool> enabled=null) { this.action=action; this.enabled=enabled; }
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested+=value; } remove { CommandManager.RequerySuggested-=value; } }
        public bool CanExecute(object parameter) { return enabled==null || enabled(); }
        public void Execute(object parameter) { action(); }
    }
    public sealed class LoginViewModel : ViewModel
    {
        private readonly ApplicationService service;
        public string Username { get; set; }="";
        public LoginViewModel(ApplicationService service) { this.service=service; }
        public async Task<Session> Login(string password)
        {
            Session session=null; await Run(async()=>session=await Task.Run(()=>service.Login(Username,password))); return session;
        }
    }
    public sealed class AdministrationViewModel : ViewModel
    {
        private readonly ApplicationService service;
        public Session Session { get; private set; }
        private bool usersMode=true; private IList rows=new object[0]; private object selected;
        private string search="";
        public string Search { get { return search; } set { search=value; Changed(); } }
        public IList Rows { get { return rows; } set { rows=value; Changed(); Changed(nameof(Count)); } }
        public object Selected { get { return selected; } set { selected=value; Changed(); CommandManager.InvalidateRequerySuggested(); } }
        public string Count { get { return Rows.Count+" registros"; } }
        public bool UsersMode { get { return usersMode; } }
        public string Title { get { return UsersMode ? "Administración de usuarios" : "Administración de perfiles"; } }
        public string Subtitle { get { return UsersMode ? "Gestiona las cuentas y el acceso a tu operación." : "Define los permisos de cada equipo."; } }
        public string Identity { get { return Session.FullName+" · "+Session.Username; } }
        public bool CanUsers { get { return Session.Can(Permission.UsersView); } }
        public bool CanProfiles { get { return Session.Can(Permission.ProfilesView); } }
        public bool CanCreate { get { return Session.Can(UsersMode ? Permission.UsersCreate : Permission.ProfilesCreate); } }
        public bool CanEdit { get { return Session.Can(UsersMode ? Permission.UsersEdit : Permission.ProfilesEdit); } }
        public bool CanDeactivate { get { return Session.Can(UsersMode ? Permission.UsersDeactivate : Permission.ProfilesDeactivate); } }
        public bool CanReset { get { return UsersMode && Session.Can(Permission.UsersEdit); } }
        public AdministrationViewModel(ApplicationService service,Session session) { this.service=service; Session=session; usersMode=CanUsers || !CanProfiles; }
        public void Switch(bool users)
        {
            usersMode=users; Search=""; Selected=null; Rows=new object[0]; NotifyPermissions(); Changed(nameof(Title)); Changed(nameof(Subtitle));
        }
        private void NotifyPermissions() { foreach(string name in new[]{"CanUsers","CanProfiles","CanCreate","CanEdit","CanDeactivate","CanReset","Identity"}) Changed(name); }
        public async Task Refresh()
        {
            await Run(async()=>
            {
                Session=await Task.Run(()=>service.ReadSession(Session.Token)); NotifyPermissions();
                if(Session.MustChangePassword) throw new BusinessException("Tu contraseña fue restablecida. Cierra sesión para cambiarla.",50001);
                if((UsersMode && !CanUsers) || (!UsersMode && !CanProfiles)) { Rows=new object[0]; throw new BusinessException("Ya no tienes acceso a este módulo.",50001); }
                string query=Search; bool mode=UsersMode;
                Rows=await Task.Run<IList>(()=>mode ? (IList)service.Users(Session,query) : service.Profiles(Session,query)); Selected=null;
            });
        }
    }
}
