using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AdmitOne;
using AdmitOne.Desktop;

namespace AdmitOne.Tests
{
    internal static class Program
    {
        private static int passed,failed;
        private static string adminPassword,connectionString;
        private static readonly string TestPassword="Validation-"+Guid.NewGuid().ToString("N");
        private static SqlConnection connection;
        private static SqlTransaction transaction;
        private static ApplicationService service;
        private static Session admin;
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding=new UTF8Encoding(false);
            Check("Contraseñas cortas rechazadas",()=>Expect(()=>Passwords.Create("short"),50002));
            Check("Salt individual y derivación estable",()=> { var a=Passwords.Create(TestPassword); var b=Passwords.Create(TestPassword); Assert(!a.Salt.SequenceEqual(b.Salt),"Salt repetido"); Assert(a.Hash.SequenceEqual(Passwords.Derive(TestPassword,a.Salt,a.Iterations)),"Derivación incorrecta"); Assert(!a.Hash.SequenceEqual(Passwords.Derive(TestPassword+"x",a.Salt,a.Iterations)),"Contraseña incorrecta aceptada"); });
            Check("Límites de contraseña y texto",()=> { Expect(()=>Passwords.Validate(new string('x',257)),50002); Expect(()=>ApplicationService.Trim(new string('x',65),64),50002); });
            if(!args.Contains("--database")) return Finish();
            try
            {
                connectionString=LocalConfiguration.Load(); Console.Write("Contraseña actual de Admin: "); adminPassword=ReadPassword();
                if(String.IsNullOrEmpty(adminPassword)) throw new Exception("Se requiere contraseña de Admin.");
                string before=Snapshot();
                if(args.Contains("--visual-only"))
                {
                    DbCase("Vistas renderizadas al 100 % y 150 %",()=>RenderViews(args));
                    Check("Datos de prueba visual revertidos",()=>Assert(before==Snapshot(),"La prueba visual alteró datos")); return Finish();
                }
                Check("Reinstalación conserva datos y contraseñas",()=>
                {
                    string installer=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..","..","src","AdmitOne.Installer","bin",new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Name,"AdmitOne.Installer.exe"));
                    var info=new ProcessStartInfo(installer) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
                    using(var process=Process.Start(info)) { string output=process.StandardOutput.ReadToEnd(); string error=process.StandardError.ReadToEnd(); process.WaitForExit(); Assert(process.ExitCode==0,"Reinstalación fallida: "+error+output); }
                    Assert(before==Snapshot(),"Reinstalación modificó datos");
                });
                DbCase("Cambio obligatorio antes de administrar",()=> { Assert(admin.MustChangePassword,"La cuenta inicial ya fue alterada"); Expect(()=>service.Users(admin),50001); },false);
                DbCase("Cambio de contraseña y acceso posterior",()=> { Assert(!admin.MustChangePassword,"Cambio no aplicado"); Assert(service.Users(admin).Any(u=>u.Username=="Admin"),"Listado vacío"); var again=service.Login("Admin",TestPassword); Assert(!again.MustChangePassword && again.Can(Permission.All),"Nuevo acceso incorrecto"); });
                DbCase("Credenciales incorrectas no crean sesión",()=> { Expect(()=>service.Login("Admin","wrong"),50010); Assert((int)Scalar("SELECT FailedAttempts FROM dbo.Users WHERE Username=N'Admin'")==1,"Intento no registrado"); });
                DbCase("Cinco fallos bloquean por 15 minutos",()=> { for(int i=0;i<5;i++) Expect(()=>service.Login("Admin","wrong"),50010); Assert((int)Scalar("SELECT FailedAttempts FROM dbo.Users WHERE Username=N'Admin'")==5,"Contador incorrecto"); Assert((int)Scalar("SELECT DATEDIFF(second,SYSUTCDATETIME(),LockedUntil) FROM dbo.Users WHERE Username=N'Admin'")>880,"Duración incorrecta"); Expect(()=>service.Login("Admin",TestPassword),50010); });
                DbCase("Expiración del bloqueo y reinicio del contador",()=> { for(int i=0;i<5;i++) Expect(()=>service.Login("Admin","wrong"),50010); Execute("UPDATE dbo.Users SET LockedUntil=DATEADD(second,-1,SYSUTCDATETIME()) WHERE Username=N'Admin'"); service.Login("Admin",TestPassword); Assert((int)Scalar("SELECT FailedAttempts FROM dbo.Users WHERE Username=N'Admin'")==0,"Contador no reiniciado"); });
                DbCase("Usuario inexistente recibe mensaje genérico",()=>Expect(()=>service.Login("does-not-exist","wrong"),50010));
                DbCase("Alta, edición y búsqueda de usuarios",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); u.FullName="Nombre cambiado"; service.SaveUser(admin,u); var found=service.Users(admin,"Nombre cambiado"); Assert(found.Count==1 && found[0].Id==u.Id,"Edición/búsqueda incorrecta"); Assert(found[0].MustChangePassword,"Falta cambio obligatorio"); });
                DbCase("Perfil y permisos persistidos",()=> { var p=CreateProfile(Permission.UsersView|Permission.ProfilesView); Assert(service.Profiles(admin,p.Name).Single().Permissions==p.Permissions,"Permisos incorrectos"); p.Description="Descripción actualizada"; service.SaveProfile(admin,p); Assert(service.Profiles(admin,p.Name).Single().Description==p.Description,"Perfil no actualizado"); });
                DbCase("Duplicados de usuarios rechazados",()=> { var p=CreateProfile(Permission.None); var u=CreateUser(p); var duplicate=u.Copy(); duplicate.Id=0; Expect(()=>service.SaveUser(admin,duplicate,TestPassword),50006); });
                DbCase("Duplicados de perfiles rechazados",()=> { var p=CreateProfile(Permission.None); var duplicate=p.Copy(); duplicate.Id=0; Expect(()=>service.SaveProfile(admin,duplicate),50006); });
                DbCase("Correo inválido rechazado",()=> { var p=CreateProfile(Permission.None); var u=CreateUser(p); u.Email="invalid"; Expect(()=>service.SaveUser(admin,u),50002); });
                DbCase("Conflicto de edición de usuario",()=> { var p=CreateProfile(Permission.None); var u=CreateUser(p); var stale=u.Copy(); u.FullName="Primera edición"; service.SaveUser(admin,u); stale.FullName="Edición antigua"; Expect(()=>service.SaveUser(admin,stale),50005); });
                DbCase("Conflicto de edición de perfil",()=> { var p=CreateProfile(Permission.None); var stale=p.Copy(); p.Description="Primera edición"; service.SaveProfile(admin,p); stale.Description="Edición antigua"; Expect(()=>service.SaveProfile(admin,stale),50005); });
                DbCase("Usuario inactivo no inicia sesión",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); service.SetUserActive(admin,u,false); Expect(()=>service.Login(u.Username,TestPassword),50010); });
                DbCase("Perfil inactivo no inicia sesión",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); service.SetProfileActive(admin,p,false); Expect(()=>service.Login(u.Username,TestPassword),50010); });
                DbCase("Activación/desactivación conserva datos",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); service.SetUserActive(admin,u,false); u=service.Users(admin,u.Username).Single(); service.SetUserActive(admin,u,true); service.SetProfileActive(admin,p,false); p=service.Profiles(admin,p.Name).Single(); service.SetProfileActive(admin,p,true); Assert(service.Users(admin,u.Username).Single().IsActive,"Usuario perdido"); Assert(service.Login(u.Username,TestPassword).MustChangePassword,"Acceso reactivado incorrecto"); });
                DbCase("Permisos aplicados en servicio y SQL",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); var low=service.ChangePassword(service.Login(u.Username,TestPassword),TestPassword+"new"); Assert(service.Users(low).Count>0,"Consulta permitida rechazada"); Expect(()=>service.Profiles(low),50001); Expect(()=>service.SaveProfile(low,new ProfileRecord { Name="Forbidden",IsActive=true }),50001); var forged=new Session { Token=low.Token,Permissions=Permission.All }; Expect(()=>service.Profiles(forged),50001); });
                DbCase("Cada permiso administrativo es obligatorio",()=> { var p=CreateProfile(Permission.UsersView|Permission.ProfilesView); var u=CreateUser(p); var low=service.ChangePassword(service.Login(u.Username,TestPassword),TestPassword+"new"); Expect(()=>service.SaveUser(low,new UserRecord()),50001); Expect(()=>service.SaveUser(low,u),50001); Expect(()=>service.SetUserActive(low,u,false),50001); Expect(()=>service.SaveProfile(low,new ProfileRecord()),50001); Expect(()=>service.SaveProfile(low,p),50001); Expect(()=>service.SetProfileActive(low,p,false),50001); Expect(()=>service.ResetPassword(low,u,TestPassword),50001); });
                foreach(var permission in new[]{Permission.UsersCreate,Permission.UsersEdit,Permission.UsersDeactivate,Permission.ProfilesCreate,Permission.ProfilesEdit,Permission.ProfilesDeactivate})
                {
                    var required=permission;
                    DbCase("SQL rechaza permisos falsificados: "+required,()=>
                    {
                        var p=CreateProfile(Permission.UsersView|Permission.ProfilesView); var u=CreateUser(p); var low=service.ChangePassword(service.Login(u.Username,TestPassword),TestPassword+"new"); low.Permissions=Permission.All; u=service.Users(admin,u.Username).Single();
                        switch(required)
                        {
                            case Permission.UsersCreate: Expect(()=>service.SaveUser(low,new UserRecord { Username="Forbidden",FullName="Forbidden",ProfileId=p.Id,IsActive=true },TestPassword),50001); break;
                            case Permission.UsersEdit: u.FullName="Cambio prohibido"; Expect(()=>service.SaveUser(low,u),50001); break;
                            case Permission.UsersDeactivate: Expect(()=>service.SetUserActive(low,u,false),50001); break;
                            case Permission.ProfilesCreate: Expect(()=>service.SaveProfile(low,new ProfileRecord { Name="Forbidden",IsActive=true }),50001); break;
                            case Permission.ProfilesEdit: p.Description="Cambio prohibido"; Expect(()=>service.SaveProfile(low,p),50001); break;
                            case Permission.ProfilesDeactivate: Expect(()=>service.SetProfileActive(low,p,false),50001); break;
                        }
                    });
                }
                DbCase("Permisos retirados invalidan sesiones existentes",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); var low=service.ChangePassword(service.Login(u.Username,TestPassword),TestPassword+"new"); p.Permissions=Permission.None; service.SaveProfile(admin,p); Expect(()=>service.Users(low),50001); });
                DbCase("Restablecer contraseña revoca sesiones",()=> { var p=CreateProfile(Permission.UsersView); var u=CreateUser(p); var low=service.ChangePassword(service.Login(u.Username,TestPassword),TestPassword+"new"); u=service.Users(admin,u.Username).Single(); service.ResetPassword(admin,u,TestPassword+"reset"); Assert(service.Login(u.Username,TestPassword+"reset").MustChangePassword,"Falta cambio tras reset"); Expect(()=>service.ReadSession(low.Token),50001); });
                DbCase("Cerrar sesión invalida el token",()=> { service.Logout(admin); Expect(()=>service.ReadSession(admin.Token),50001); });
                DbCase("Último administrador no se desactiva",()=> { var u=service.Users(admin,"Admin").Single(); Expect(()=>service.SetUserActive(admin,u,false),50003); });
                DbCase("Perfil del último administrador no se desactiva",()=> { var p=service.Profiles(admin).Single(); Expect(()=>service.SetProfileActive(admin,p,false),50003); });
                DbCase("Último administrador conserva permisos completos",()=> { var p=service.Profiles(admin).Single(); p.Permissions=Permission.UsersView; Expect(()=>service.SaveProfile(admin,p),50003); });
                DbCase("Último administrador no se cambia a perfil limitado",()=> { var p=CreateProfile(Permission.UsersView); var u=service.Users(admin,"Admin").Single(); u.ProfileId=p.Id; Expect(()=>service.SaveUser(admin,u),50003); });
                DbCase("Auditoría administrativa sin secretos",()=> { var p=CreateProfile(Permission.UsersView); CreateUser(p); Assert((int)Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action IN ('UserSaved','ProfileSaved','PasswordChanged')")>=3,"Auditoría incompleta"); Assert((int)Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE CHARINDEX(N'"+TestPassword+"',Detail)>0")==0,"Secreto en auditoría"); });
                DbCase("Vistas renderizadas al 100 % y 150 %",()=>RenderViews(args));
                Check("Datos de prueba revertidos por completo",()=>Assert(before==Snapshot(),"Los datos difieren después de las pruebas"));
                Check("Conexión y persistencia tras reiniciar servicio",()=> { var fresh=new ApplicationService(new Database(LocalConfiguration.Load())); var session=fresh.Login("Admin",adminPassword); Assert(session.MustChangePassword,"Se cambió la contraseña real"); fresh.Logout(session); });
                Check("Desconexión genera aviso y diagnóstico sin secretos",()=>
                {
                    var b=new SqlConnectionStringBuilder(connectionString) { DataSource="tcp:127.0.0.1,1",ConnectTimeout=1 };
                    try { new ApplicationService(new Database(b.ConnectionString)).Login("Admin",adminPassword); throw new Exception("Se esperaba una desconexión"); }
                    catch(SqlException ex) { string notice=ErrorLog.Describe(ex); Assert(notice.Contains("Referencia:"),"Falta referencia"); string log=File.ReadAllText(Path.Combine(LocalConfiguration.DirectoryPath,"logs",DateTime.UtcNow.ToString("yyyy-MM-dd")+".log")); Assert(!log.Contains(adminPassword) && !log.Contains(b.Password) && !log.Contains(connectionString),"Secreto en diagnóstico"); }
                });
            }
            catch(Exception e) { failed++; Console.WriteLine("FAIL preparación: "+ErrorLog.Describe(e)); }
            return Finish();
        }
        private static int Finish() { Console.WriteLine("RESULTADO: "+passed+" correctas, "+failed+" fallidas."); return failed==0 ? 0 : 1; }
        private static string ReadPassword()
        {
            if(Console.IsInputRedirected) return Console.ReadLine();
            var input=new StringBuilder(); for(;;) { var key=Console.ReadKey(true); if(key.Key==ConsoleKey.Enter) { Console.WriteLine(); return input.ToString(); } if(key.Key==ConsoleKey.Backspace) { if(input.Length>0) input.Length--; } else if(!Char.IsControl(key.KeyChar)) input.Append(key.KeyChar); }
        }
        private static void Check(string name,Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS "+name); }
            catch(Exception e) { while(e is TargetInvocationException && e.InnerException!=null) e=e.InnerException; failed++; Console.WriteLine("FAIL "+name+": "+e.Message); }
        }
        private static void DbCase(string name,Action test,bool changePassword=true)
        {
            Check(name,()=>
            {
                using(connection=new SqlConnection(connectionString))
                {
                    connection.Open(); using(transaction=connection.BeginTransaction())
                    {
                        try { service=new ApplicationService(new Database(connection,transaction)); admin=service.Login("Admin",adminPassword); if(changePassword) admin=service.ChangePassword(admin,TestPassword); test(); }
                        finally { if(transaction.Connection!=null) transaction.Rollback(); }
                    }
                }
            });
        }
        private static ProfileRecord CreateProfile(Permission mask)
        {
            var p=new ProfileRecord { Name="Test-"+Guid.NewGuid().ToString("N").Substring(0,8),Description="Temporal",Permissions=mask,IsActive=true }; service.SaveProfile(admin,p); return service.Profiles(admin,p.Name).Single();
        }
        private static UserRecord CreateUser(ProfileRecord profile)
        {
            var u=new UserRecord { Username="test-"+Guid.NewGuid().ToString("N").Substring(0,8),FullName="Usuario temporal",Email="test@example.com",ProfileId=profile.Id,IsActive=true }; service.SaveUser(admin,u,TestPassword); return service.Users(admin,u.Username).Single();
        }
        private static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
        private static void Expect(Action action,int code)
        {
            try { action(); } catch(BusinessException e) { Assert(e.Code==code,"Código esperado "+code+", recibido "+e.Code); return; } throw new Exception("Se esperaba rechazo "+code);
        }
        private static object Scalar(string sql) { using(var cmd=new SqlCommand(sql,connection,transaction)) return cmd.ExecuteScalar(); }
        private static void Execute(string sql) { using(var cmd=new SqlCommand(sql,connection,transaction)) cmd.ExecuteNonQuery(); }
        private static string Snapshot()
        {
            using(var c=new SqlConnection(connectionString))
            {
                c.Open(); var result=new StringBuilder();
                foreach(string name in new[]{"Users","Profiles","Permissions","ProfilePermissions","AuditLog","Sessions","SchemaVersions"}) using(var cmd=new SqlCommand("SELECT COUNT(*),ISNULL(CHECKSUM_AGG(BINARY_CHECKSUM(*)),0) FROM dbo."+name,c)) using(var reader=cmd.ExecuteReader()) { reader.Read(); result.Append(name+":"+reader.GetValue(0)+":"+reader.GetValue(1)+";"); }
                return result.ToString();
            }
        }
        private static void RenderViews(string[] args)
        {
            var diagnostics=new BindingDiagnostics(); PresentationTraceSources.DataBindingSource.Listeners.Add(diagnostics); PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Warning;
            if(Application.Current==null) { var app=new App(); app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/AdmitOne.Desktop;component/Theme.xaml",UriKind.Relative) }); }
            string folder=args.FirstOrDefault(a=>a.StartsWith("--renders="))?.Substring(10)??Path.Combine(Environment.CurrentDirectory,"artifacts","screenshots"); Directory.CreateDirectory(folder);
            var login=new LoginWindow(service); var panel=new AdministrationWindow(service,admin); panel.Model.Rows=service.Users(admin);
            var password=new PasswordWindow(null,"Actualiza tu contraseña","Para continuar, reemplaza tu contraseña temporal por una de al menos 12 caracteres.",value=>System.Threading.Tasks.Task.CompletedTask);
            var userEditor=(Window)typeof(AdministrationWindow).GetMethod("UserDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{new UserRecord { IsActive=true },service.Profiles(admin)});
            var profileEditor=(Window)typeof(AdministrationWindow).GetMethod("ProfileDialog",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{new ProfileRecord { IsActive=true,Permissions=Permission.All }});
            foreach(double scale in new[]{1.0,1.5})
            {
                Render(login.Surface,1050,640,scale,Path.Combine(folder,"login-"+(int)(scale*100)+".png"));
                Render(password.Surface,530,610,scale,Path.Combine(folder,"password-"+(int)(scale*100)+".png"));
                Render((FrameworkElement)userEditor.Content,550,650,scale,Path.Combine(folder,"user-editor-"+(int)(scale*100)+".png"));
                Render((FrameworkElement)profileEditor.Content,650,580,scale,Path.Combine(folder,"profile-editor-"+(int)(scale*100)+".png"));
                panel.Model.Switch(true); panel.ConfigureColumns(); panel.Model.Rows=service.Users(admin); Render(panel.Surface,1200,740,scale,Path.Combine(folder,"users-"+(int)(scale*100)+".png"));
                panel.Model.Switch(false); panel.ConfigureColumns(); panel.Model.Rows=service.Profiles(admin); Render(panel.Surface,1200,740,scale,Path.Combine(folder,"profiles-"+(int)(scale*100)+".png"));
            }
            Render(panel.Surface,900,540,1.5,Path.Combine(folder,"profiles-small-150.png"));
            PresentationTraceSources.DataBindingSource.Listeners.Remove(diagnostics);
            Assert(diagnostics.Messages.Length==0,"Alertas de binding: "+diagnostics.Messages);
        }
        private sealed class BindingDiagnostics : TraceListener
        {
            public readonly StringBuilder Messages=new StringBuilder();
            public override void Write(string message) { Messages.Append(message); }
            public override void WriteLine(string message) { Messages.AppendLine(message); }
        }
        private static void Render(FrameworkElement view,int width,int height,double scale,string file)
        {
            view.Measure(new Size(width,height)); view.Arrange(new Rect(0,0,width,height)); view.UpdateLayout();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{})); view.UpdateLayout();
            var image=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32); image.Render(view);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(var stream=File.Create(file)) encoder.Save(stream);
            Assert(view.ActualWidth==width && view.ActualHeight==height,"Tamaño de vista incorrecto");
        }
    }
}
