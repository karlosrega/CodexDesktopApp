using System;

namespace AdmitOne
{
    [Flags]
    public enum Permission { None=0, UsersView=1, UsersCreate=2, UsersEdit=4, UsersDeactivate=8, ProfilesView=16, ProfilesCreate=32, ProfilesEdit=64, ProfilesDeactivate=128, All=255 }
    public sealed class Session
    {
        public Guid Token { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public bool MustChangePassword { get; set; }
        public Permission Permissions { get; set; }
        public bool Can(Permission value) { return !MustChangePassword && (Permissions & value)==value; }
    }
    public sealed class UserRecord
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public byte[] Version { get; set; }
        public string State { get { return IsActive ? "Activo" : "Inactivo"; } }
        public string PasswordState { get { return MustChangePassword ? "Cambio requerido" : "Actualizada"; } }
        public UserRecord Copy() { return (UserRecord)MemberwiseClone(); }
    }
    public sealed class ProfileRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public Permission Permissions { get; set; }
        public byte[] Version { get; set; }
        public string State { get { return IsActive ? "Activo" : "Inactivo"; } }
        public ProfileRecord Copy() { return (ProfileRecord)MemberwiseClone(); }
    }
    public sealed class BusinessException : Exception
    {
        public int Code { get; private set; }
        public BusinessException(string message, int code=50002) : base(message) { Code=code; }
    }
}
