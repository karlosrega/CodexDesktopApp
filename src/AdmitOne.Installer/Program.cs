using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdmitOne;

namespace AdmitOne.Installer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding=new UTF8Encoding(false);
            try
            {
                bool configure=args.Contains("--configure"), redirected=args.Contains("--stdin");
                string cs;
                if(configure || !File.Exists(LocalConfiguration.FilePath))
                {
                    string server=Read("Servidor [localhost]: ",false,redirected); if(server.Length==0) server="localhost";
                    string sqlUser=Read("Usuario SQL: ",false,redirected);
                    string sqlPassword=Read("Contraseña SQL: ",true,redirected);
                    var builder=new SqlConnectionStringBuilder { DataSource=server,InitialCatalog="CodexDesktopApp",UserID=sqlUser,Password=sqlPassword,ConnectTimeout=10,ApplicationName="AdmitOne Desktop" };
                    cs=builder.ConnectionString;
                    using(var check=new SqlConnection(cs)) check.Open();
                    LocalConfiguration.Save(cs);
                }
                else cs=LocalConfiguration.Load();
                using(var connection=new SqlConnection(cs))
                {
                    connection.Open();
                    using(var tx=connection.BeginTransaction())
                    {
                        using(var lockCmd=new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AdmitOne.SchemaInstaller',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50004,N'Instalación concurrente.',1;",connection,tx)) lockCmd.ExecuteNonQuery();
                        foreach(string file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"database"),"*.sql").OrderBy(f=>f,StringComparer.Ordinal))
                        {
                            int number=Int32.Parse(Path.GetFileName(file).Split('_')[0]); bool installed;
                            using(var version=new SqlCommand("IF OBJECT_ID(N'dbo.SchemaVersions') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM dbo.SchemaVersions WHERE Version=@Version",connection,tx)) { version.Parameters.Add("@Version",SqlDbType.Int).Value=number; installed=(int)version.ExecuteScalar()>0; }
                            if(installed) { Console.WriteLine("Versión "+number+" ya instalada; se conserva sin cambios."); continue; }
                            foreach(string batch in Regex.Split(File.ReadAllText(file),@"^\s*GO\s*\r?$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                                if(!String.IsNullOrWhiteSpace(batch)) using(var cmd=new SqlCommand(batch,connection,tx)) { cmd.CommandTimeout=60; cmd.ExecuteNonQuery(); }
                            Console.WriteLine("Versión "+number+" aplicada en la transacción de instalación.");
                        }
                        tx.Commit(); Console.WriteLine("Esquema confirmado correctamente.");
                    }
                    int users; using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.Users",connection)) users=(int)cmd.ExecuteScalar();
                    if(users==0)
                    {
                        string username=Read("Administrador inicial [Admin]: ",false,redirected); if(username.Length==0) username="Admin";
                        username=ApplicationService.Trim(username,64);
                        string password=Read("Contraseña temporal inicial: ",true,redirected);
                        var d=Passwords.Create(password,true);
                        new Database(cs).Call("dbo.sp_SeedAdmin",Database.P("@Username",SqlDbType.NVarChar,username,64),Database.P("@Hash",SqlDbType.Binary,d.Hash,32),Database.P("@Salt",SqlDbType.Binary,d.Salt,32),Database.P("@Iterations",SqlDbType.Int,d.Iterations));
                        Console.WriteLine("Cuenta inicial creada. El cambio de contraseña es obligatorio.");
                    }
                    else Console.WriteLine("Las cuentas existentes se conservan; no se restablecen contraseñas.");
                }
                Console.WriteLine("Instalación verificada en CodexDesktopApp. Conexión protegida con DPAPI para este usuario de Windows.");
                return 0;
            }
            catch(Exception e) { Console.Error.WriteLine(ErrorLog.Describe(e)); return 1; }
        }
        private static string Read(string prompt,bool secret,bool redirected)
        {
            Console.Write(prompt);
            if(redirected) { var line=Console.ReadLine(); if(line==null) throw new BusinessException("Faltan datos de instalación."); return line; }
            if(!secret) return Console.ReadLine()??"";
            var value=new StringBuilder();
            for(;;)
            {
                var key=Console.ReadKey(true);
                if(key.Key==ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
                if(key.Key==ConsoleKey.Backspace) { if(value.Length>0) value.Length--; }
                else if(!Char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
            }
        }
    }
}
