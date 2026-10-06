using System;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AdmitOne
{
    public sealed class PasswordDigest
    {
        public byte[] Hash { get; set; }
        public byte[] Salt { get; set; }
        public int Iterations { get; set; }
    }
    public static class Passwords
    {
        public const int Iterations = 210000;
        public static void Validate(string password)
        {
            if (String.IsNullOrWhiteSpace(password) || password.Length<12 || password.Length>256)
                throw new BusinessException("La contraseña debe tener entre 12 y 256 caracteres.");
        }
        public static PasswordDigest Create(string password, bool initial=false)
        {
            if (!initial) Validate(password);
            if (String.IsNullOrEmpty(password) || password.Length>256) throw new BusinessException("Contraseña inválida.");
            var salt=new byte[32]; using(var rng=RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return new PasswordDigest { Salt=salt, Hash=Derive(password,salt,Iterations), Iterations=Iterations };
        }
        public static byte[] Derive(string password,byte[] salt,int iterations)
        {
            if(iterations<100000 || iterations>2000000 || salt==null || salt.Length!=32) throw new BusinessException("Formato de credenciales no compatible.");
            using(var kdf=new Rfc2898DeriveBytes(password,salt,iterations,HashAlgorithmName.SHA256)) return kdf.GetBytes(32);
        }
    }
    public static class LocalConfiguration
    {
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AdmitOne"); } }
        public static string FilePath { get { return Path.Combine(DirectoryPath,"connection.dpapi"); } }
        public static void Save(string connectionString)
        {
            Validate(connectionString);
            var bytes=Encoding.UTF8.GetBytes(connectionString);
            try
            {
                var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(DirectoryPath);
                string temp=FilePath+".tmp"; File.WriteAllBytes(temp,encrypted);
                if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
            }
            finally { Array.Clear(bytes,0,bytes.Length); }
        }
        public static string Load()
        {
            if(!File.Exists(FilePath)) throw new BusinessException("Configura la conexión ejecutando AdmitOne.Installer.exe --configure antes de iniciar.");
            var bytes=ProtectedData.Unprotect(File.ReadAllBytes(FilePath),null,DataProtectionScope.CurrentUser);
            try { string result=Encoding.UTF8.GetString(bytes); Validate(result); return result; }
            finally { Array.Clear(bytes,0,bytes.Length); }
        }
        private static void Validate(string connectionString)
        {
            var builder=new SqlConnectionStringBuilder(connectionString);
            if(!String.Equals(builder.InitialCatalog,"CodexDesktopApp",StringComparison.OrdinalIgnoreCase) || !String.IsNullOrEmpty(builder.AttachDBFilename))
                throw new BusinessException("La conexión debe apuntar exclusivamente a CodexDesktopApp.");
        }
    }
    public static class ErrorLog
    {
        public static string Describe(Exception exception)
        {
            var business=exception as BusinessException;
            if(business!=null) return business.Message;
            string id=Guid.NewGuid().ToString("N").Substring(0,12);
            try
            {
                string folder=Path.Combine(LocalConfiguration.DirectoryPath,"logs"); Directory.CreateDirectory(folder);
                var sql=exception as SqlException;
                // Do not persist exception messages: providers may include connection details.
                File.AppendAllText(Path.Combine(folder,DateTime.UtcNow.ToString("yyyy-MM-dd")+".log"),
                    DateTime.UtcNow.ToString("O")+" | "+id+" | "+exception.GetType().FullName+" | HResult="+exception.HResult+
                    (sql==null ? "" : " | SQL="+sql.Number)+Environment.NewLine+exception.StackTrace+Environment.NewLine);
            }
            catch(IOException) { return "No se pudo completar la operación ni guardar el diagnóstico. Referencia: "+id; }
            catch(UnauthorizedAccessException) { return "No se pudo completar la operación ni guardar el diagnóstico. Referencia: "+id; }
            return "No se pudo completar la operación. Comprueba la conexión a SQL Server. Referencia: "+id;
        }
    }
}
