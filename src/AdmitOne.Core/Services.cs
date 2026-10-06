using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;
using System.Data.SqlClient;

namespace AdmitOne
{
    public sealed class ApplicationService
    {
        private readonly Database db;
        public ApplicationService(Database database) { db=database; }
        private static SqlParameter Token(Session session) { return Database.P("@Token",SqlDbType.UniqueIdentifier,session.Token); }
        private static void Require(Session session,Permission permission)
        {
            if(session==null || !session.Can(permission)) throw new BusinessException("No tienes permiso para realizar esta operación.",50001);
        }
        public Session Login(string username,string password)
        {
            username=(username??"").Trim();
            if(username.Length==0 || username.Length>64 || String.IsNullOrEmpty(password) || password.Length>256) throw new BusinessException("Usuario o contraseña incorrectos.",50010);
            for(int attempt=0; attempt<3; attempt++)
            {
                var read=db.Call("dbo.sp_AuthRead",Database.P("@Username",SqlDbType.NVarChar,username,64));
                byte[] salt=new byte[32],version=null; int iterations=Passwords.Iterations;
                if(read.Rows.Count>0)
                {
                    var row=read.Rows[0]; salt=(byte[])row["PasswordSalt"]; iterations=(int)row["PasswordIterations"]; version=(byte[])row["RowVersion"];
                    if((string)row["PasswordAlgorithm"]!="PBKDF2-SHA256-v1") throw new BusinessException("Formato de credenciales no compatible.");
                }
                byte[] candidate=Passwords.Derive(password,salt,iterations);
                DataTable result;
                try { result=db.Call("dbo.sp_AuthComplete",Database.P("@Username",SqlDbType.NVarChar,username,64),Database.P("@Candidate",SqlDbType.Binary,candidate,32),Database.P("@Version",SqlDbType.Binary,version,8)); }
                catch(BusinessException e) { if(e.Code==50005 && attempt<2) continue; throw; }
                finally { Array.Clear(candidate,0,candidate.Length); }
                if(result.Rows.Count==0) throw new BusinessException("Usuario o contraseña incorrectos. Si hubo varios intentos, espera 15 minutos.",50010);
                return ReadSession((Guid)result.Rows[0]["Token"]);
            }
            throw new BusinessException("Vuelve a intentar iniciar sesión.",50010);
        }
        public Session ReadSession(Guid token)
        {
            var rows=db.Call("dbo.sp_SessionRead",Database.P("@Token",SqlDbType.UniqueIdentifier,token));
            if(rows.Rows.Count==0) throw new BusinessException("La sesión expiró o el acceso fue desactivado. Inicia sesión nuevamente.",50001);
            var row=rows.Rows[0];
            return new Session { Token=token,UserId=(int)row["Id"],Username=(string)row["Username"],FullName=(string)row["FullName"],MustChangePassword=(bool)row["MustChangePassword"],Permissions=(Permission)(int)row["PermissionMask"] };
        }
        public void Logout(Session session) { db.Call("dbo.sp_Logout",Token(session)); }
        public List<UserRecord> Users(Session session,string search="")
        {
            Require(session,Permission.UsersView);
            var rows=db.Call("dbo.sp_UsersList",Token(session),Database.P("@Search",SqlDbType.NVarChar,Trim(search,120),120));
            var list=new List<UserRecord>(); foreach(DataRow r in rows.Rows) list.Add(new UserRecord { Id=(int)r["Id"],Username=(string)r["Username"],FullName=(string)r["FullName"],Email=r["Email"] as string,ProfileId=(int)r["ProfileId"],ProfileName=(string)r["ProfileName"],IsActive=(bool)r["IsActive"],MustChangePassword=(bool)r["MustChangePassword"],Version=(byte[])r["RowVersion"] }); return list;
        }
        public List<ProfileRecord> Profiles(Session session,string search="",bool lookup=false)
        {
            if(!lookup) Require(session,Permission.ProfilesView);
            var rows=db.Call("dbo.sp_ProfilesList",Token(session),Database.P("@Search",SqlDbType.NVarChar,Trim(search,120),120),Database.P("@Lookup",SqlDbType.Bit,lookup));
            var list=new List<ProfileRecord>(); foreach(DataRow r in rows.Rows) list.Add(new ProfileRecord { Id=(int)r["Id"],Name=(string)r["Name"],Description=(string)r["Description"],IsActive=(bool)r["IsActive"],Permissions=(Permission)(int)r["PermissionMask"],Version=(byte[])r["RowVersion"] }); return list;
        }
        public int SaveUser(Session session,UserRecord user,string initialPassword=null)
        {
            Require(session,user.Id==0 ? Permission.UsersCreate : Permission.UsersEdit);
            return WriteUser(session,user,initialPassword);
        }
        public void SetUserActive(Session session,UserRecord user,bool active)
        {
            Require(session,Permission.UsersDeactivate); var copy=user.Copy(); copy.IsActive=active; WriteUser(session,copy,null);
        }
        private int WriteUser(Session session,UserRecord user,string password)
        {
            user.Username=Required(user.Username,64,"usuario"); user.FullName=Required(user.FullName,120,"nombre completo"); user.Email=Trim(user.Email,254);
            if(user.Email.Length>0) { try { var address=new MailAddress(user.Email); if(address.Address!=user.Email) throw new FormatException(); } catch(FormatException) { throw new BusinessException("El correo electrónico no es válido."); } }
            PasswordDigest digest=user.Id==0 ? Passwords.Create(password) : null;
            var result=db.Call("dbo.sp_UserSave",Token(session),Database.P("@Id",SqlDbType.Int,user.Id==0 ? (object)null : user.Id),Database.P("@Username",SqlDbType.NVarChar,user.Username,64),Database.P("@FullName",SqlDbType.NVarChar,user.FullName,120),Database.P("@Email",SqlDbType.NVarChar,user.Email.Length==0 ? null : user.Email,254),Database.P("@ProfileId",SqlDbType.Int,user.ProfileId),Database.P("@Active",SqlDbType.Bit,user.IsActive),Database.P("@Version",SqlDbType.Binary,user.Version,8),Database.P("@Hash",SqlDbType.Binary,digest==null ? null : digest.Hash,32),Database.P("@Salt",SqlDbType.Binary,digest==null ? null : digest.Salt,32),Database.P("@Iterations",SqlDbType.Int,digest==null ? (object)null : digest.Iterations));
            return (int)result.Rows[0]["Id"];
        }
        public int SaveProfile(Session session,ProfileRecord profile)
        {
            Require(session,profile.Id==0 ? Permission.ProfilesCreate : Permission.ProfilesEdit); return WriteProfile(session,profile);
        }
        public void SetProfileActive(Session session,ProfileRecord profile,bool active)
        {
            Require(session,Permission.ProfilesDeactivate); var copy=profile.Copy(); copy.IsActive=active; WriteProfile(session,copy);
        }
        private int WriteProfile(Session session,ProfileRecord profile)
        {
            var rows=db.Call("dbo.sp_ProfileSave",Token(session),Database.P("@Id",SqlDbType.Int,profile.Id==0 ? (object)null : profile.Id),Database.P("@Name",SqlDbType.NVarChar,Required(profile.Name,80,"nombre de perfil"),80),Database.P("@Description",SqlDbType.NVarChar,Trim(profile.Description,250),250),Database.P("@Active",SqlDbType.Bit,profile.IsActive),Database.P("@Mask",SqlDbType.Int,(int)profile.Permissions),Database.P("@Version",SqlDbType.Binary,profile.Version,8)); return (int)rows.Rows[0]["Id"];
        }
        public Session ChangePassword(Session session,string newPassword)
        {
            SetPassword(session,session.UserId,null,newPassword,false); return ReadSession(session.Token);
        }
        public void ResetPassword(Session session,UserRecord user,string newPassword)
        {
            Require(session,Permission.UsersEdit); SetPassword(session,user.Id,user.Version,newPassword,true);
        }
        private void SetPassword(Session session,int userId,byte[] version,string password,bool reset)
        {
            var d=Passwords.Create(password);
            db.Call("dbo.sp_PasswordSet",Token(session),Database.P("@UserId",SqlDbType.Int,userId),Database.P("@Hash",SqlDbType.Binary,d.Hash,32),Database.P("@Salt",SqlDbType.Binary,d.Salt,32),Database.P("@Iterations",SqlDbType.Int,d.Iterations),Database.P("@Version",SqlDbType.Binary,version,8),Database.P("@Reset",SqlDbType.Bit,reset));
        }
        public static string Trim(string value,int max)
        {
            value=(value??"").Trim(); if(value.Length>max) throw new BusinessException("El texto excede el máximo de "+max+" caracteres."); return value;
        }
        private static string Required(string value,int max,string label)
        {
            value=Trim(value,max); if(value.Length==0) throw new BusinessException("Escribe el "+label+"."); return value;
        }
    }
}
