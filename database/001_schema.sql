-- SQL Server 2014+. Executed in one installer-owned transaction, only in CodexDesktopApp.
IF DB_NAME() <> N'CodexDesktopApp' THROW 50000, N'Base de datos incorrecta.', 1;
IF OBJECT_ID(N'dbo.SchemaVersions') IS NULL CREATE TABLE dbo.SchemaVersions(Version int NOT NULL PRIMARY KEY, AppliedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID(N'dbo.Profiles') IS NULL CREATE TABLE dbo.Profiles(
 Id int IDENTITY PRIMARY KEY, Name nvarchar(80) NOT NULL UNIQUE, Description nvarchar(250) NOT NULL DEFAULT N'', IsActive bit NOT NULL DEFAULT 1, RowVersion rowversion NOT NULL,
 CONSTRAINT CK_ProfileName CHECK(LEN(LTRIM(RTRIM(Name))) > 0));
IF OBJECT_ID(N'dbo.Permissions') IS NULL CREATE TABLE dbo.Permissions(BitValue int PRIMARY KEY, Code varchar(40) NOT NULL UNIQUE);
IF OBJECT_ID(N'dbo.ProfilePermissions') IS NULL CREATE TABLE dbo.ProfilePermissions(ProfileId int NOT NULL REFERENCES dbo.Profiles(Id), PermissionBit int NOT NULL REFERENCES dbo.Permissions(BitValue), PRIMARY KEY(ProfileId,PermissionBit));
IF OBJECT_ID(N'dbo.Users') IS NULL BEGIN
 CREATE TABLE dbo.Users(Id int IDENTITY PRIMARY KEY, Username nvarchar(64) NOT NULL UNIQUE, FullName nvarchar(120) NOT NULL, Email nvarchar(254) NULL,
 ProfileId int NOT NULL REFERENCES dbo.Profiles(Id), IsActive bit NOT NULL DEFAULT 1, PasswordHash binary(32) NOT NULL, PasswordSalt binary(32) NOT NULL,
 PasswordIterations int NOT NULL, PasswordAlgorithm varchar(24) NOT NULL DEFAULT 'PBKDF2-SHA256-v1', MustChangePassword bit NOT NULL DEFAULT 1,
 FailedAttempts int NOT NULL DEFAULT 0, LockedUntil datetime2 NULL, LastLoginAt datetime2 NULL, CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT CK_UserNames CHECK(LEN(LTRIM(RTRIM(Username)))>0 AND LEN(LTRIM(RTRIM(FullName)))>0), CONSTRAINT CK_PasswordIterations CHECK(PasswordIterations>=100000), CONSTRAINT CK_FailedAttempts CHECK(FailedAttempts>=0));
 CREATE INDEX IX_Users_Profile ON dbo.Users(ProfileId);
END;
IF OBJECT_ID(N'dbo.Sessions') IS NULL BEGIN
 CREATE TABLE dbo.Sessions(Token uniqueidentifier NOT NULL PRIMARY KEY, UserId int NOT NULL REFERENCES dbo.Users(Id), ExpiresAt datetime2 NOT NULL);
 CREATE INDEX IX_Sessions_User ON dbo.Sessions(UserId);
END;
IF OBJECT_ID(N'dbo.AuditLog') IS NULL CREATE TABLE dbo.AuditLog(Id bigint IDENTITY PRIMARY KEY, AtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), ActorId int NULL REFERENCES dbo.Users(Id), Action varchar(40) NOT NULL, EntityId int NULL, Detail nvarchar(250) NOT NULL DEFAULT N'');
INSERT dbo.Permissions(BitValue,Code) SELECT v.BitValue,v.Code FROM (VALUES(1,'Users.View'),(2,'Users.Create'),(4,'Users.Edit'),(8,'Users.Deactivate'),(16,'Profiles.View'),(32,'Profiles.Create'),(64,'Profiles.Edit'),(128,'Profiles.Deactivate'))v(BitValue,Code) WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.BitValue=v.BitValue);
GO
IF OBJECT_ID(N'dbo.fn_Actor') IS NULL EXEC(N'CREATE FUNCTION dbo.fn_Actor(@Token uniqueidentifier) RETURNS int AS BEGIN RETURN NULL END');
GO
ALTER FUNCTION dbo.fn_Actor(@Token uniqueidentifier) RETURNS int AS
BEGIN
 RETURN(SELECT u.Id FROM dbo.Sessions s JOIN dbo.Users u ON u.Id=s.UserId JOIN dbo.Profiles p ON p.Id=u.ProfileId WHERE s.Token=@Token AND s.ExpiresAt>SYSUTCDATETIME() AND u.IsActive=1 AND p.IsActive=1);
END;
GO
IF OBJECT_ID(N'dbo.fn_Can') IS NULL EXEC(N'CREATE FUNCTION dbo.fn_Can(@Token uniqueidentifier,@Bit int) RETURNS bit AS BEGIN RETURN 0 END');
GO
ALTER FUNCTION dbo.fn_Can(@Token uniqueidentifier,@Bit int) RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.ProfilePermissions pp ON pp.ProfileId=u.ProfileId WHERE u.Id=dbo.fn_Actor(@Token) AND u.MustChangePassword=0 AND pp.PermissionBit=@Bit) THEN 1 ELSE 0 END;
END;
GO
IF OBJECT_ID(N'dbo.sp_AdminGuard') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_AdminGuard AS RETURN');
GO
ALTER PROCEDURE dbo.sp_AdminGuard AS
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Profiles p ON p.Id=u.ProfileId WHERE u.IsActive=1 AND p.IsActive=1 AND (SELECT COUNT(*) FROM dbo.ProfilePermissions pp WHERE pp.ProfileId=p.Id)=8)
 THROW 50003,N'La operación dejaría el sistema sin un administrador activo con todos los permisos.',1;
END;
GO
IF OBJECT_ID(N'dbo.sp_AdminLock') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_AdminLock AS RETURN');
GO
ALTER PROCEDURE dbo.sp_AdminLock AS
BEGIN
 DECLARE @r int;
 EXEC @r=sys.sp_getapplock @Resource=N'AdmitOne.AdminInvariant',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @r<0 THROW 50004,N'No se pudo obtener el bloqueo. Intenta nuevamente.',1;
END;
GO
IF OBJECT_ID(N'dbo.sp_AuthRead') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_AuthRead AS RETURN');
GO
ALTER PROCEDURE dbo.sp_AuthRead @Username nvarchar(64) AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id,PasswordSalt,PasswordIterations,PasswordAlgorithm,RowVersion FROM dbo.Users WHERE Username=@Username;
END;
GO
IF OBJECT_ID(N'dbo.sp_AuthComplete') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_AuthComplete AS RETURN');
GO
ALTER PROCEDURE dbo.sp_AuthComplete @Username nvarchar(64),@Candidate binary(32),@Version binary(8)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRANSACTION;
 DECLARE @Id int,@Hash binary(32),@Rv binary(8),@Active bit,@ProfileActive bit,@Failures int,@Locked datetime2;
 SELECT @Id=u.Id,@Hash=u.PasswordHash,@Rv=u.RowVersion,@Active=u.IsActive,@ProfileActive=p.IsActive,@Failures=u.FailedAttempts,@Locked=u.LockedUntil FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Profiles p ON p.Id=u.ProfileId WHERE u.Username=@Username;
 IF @Id IS NOT NULL AND (@Version IS NULL OR @Version<>@Rv) BEGIN ROLLBACK; THROW 50005,N'Las credenciales cambiaron; vuelve a intentar.',1; END;
 IF @Id IS NULL OR @Active=0 OR @ProfileActive=0 OR @Locked>SYSUTCDATETIME() BEGIN
  INSERT dbo.AuditLog(ActorId,Action) VALUES(@Id,'LoginRejected'); COMMIT; RETURN;
 END;
 IF @Hash<>@Candidate BEGIN
  SET @Failures=CASE WHEN @Locked IS NOT NULL THEN 1 ELSE @Failures+1 END;
  UPDATE dbo.Users SET FailedAttempts=@Failures,LockedUntil=CASE WHEN @Failures>=5 THEN DATEADD(minute,15,SYSUTCDATETIME()) ELSE NULL END WHERE Id=@Id;
  INSERT dbo.AuditLog(ActorId,Action) VALUES(@Id,'LoginFailed'); COMMIT; RETURN;
 END;
 UPDATE dbo.Users SET FailedAttempts=0,LockedUntil=NULL,LastLoginAt=SYSUTCDATETIME() WHERE Id=@Id;
 DELETE dbo.Sessions WHERE ExpiresAt<=SYSUTCDATETIME();
 DECLARE @Token uniqueidentifier=NEWID();
 INSERT dbo.Sessions(Token,UserId,ExpiresAt) VALUES(@Token,@Id,DATEADD(hour,8,SYSUTCDATETIME()));
 INSERT dbo.AuditLog(ActorId,Action) VALUES(@Id,'LoginSucceeded');
 COMMIT;
 SELECT @Token AS Token;
END;
GO
IF OBJECT_ID(N'dbo.sp_SessionRead') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_SessionRead AS RETURN');
GO
ALTER PROCEDURE dbo.sp_SessionRead @Token uniqueidentifier AS
BEGIN
 SET NOCOUNT ON;
 SELECT u.Id,u.Username,u.FullName,u.MustChangePassword,CASE WHEN u.MustChangePassword=1 THEN 0 ELSE ISNULL((SELECT SUM(PermissionBit) FROM dbo.ProfilePermissions pp WHERE pp.ProfileId=u.ProfileId),0) END PermissionMask
 FROM dbo.Users u WHERE u.Id=dbo.fn_Actor(@Token);
END;
GO
IF OBJECT_ID(N'dbo.sp_Logout') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_Logout AS RETURN');
GO
ALTER PROCEDURE dbo.sp_Logout @Token uniqueidentifier AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(ActorId,Action) SELECT UserId,'Logout' FROM dbo.Sessions WHERE Token=@Token;
 DELETE dbo.Sessions WHERE Token=@Token;
END;
GO
IF OBJECT_ID(N'dbo.sp_UsersList') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_UsersList AS RETURN');
GO
ALTER PROCEDURE dbo.sp_UsersList @Token uniqueidentifier,@Search nvarchar(120)=N'' AS
BEGIN
 SET NOCOUNT ON;
 IF dbo.fn_Can(@Token,1)=0 THROW 50001,N'No tienes permiso para consultar usuarios.',1;
 SELECT u.Id,u.Username,u.FullName,u.Email,u.ProfileId,p.Name ProfileName,u.IsActive,u.MustChangePassword,u.RowVersion FROM dbo.Users u JOIN dbo.Profiles p ON p.Id=u.ProfileId WHERE @Search=N'' OR CHARINDEX(@Search,u.Username)>0 OR CHARINDEX(@Search,u.FullName)>0 ORDER BY u.Username;
END;
GO
IF OBJECT_ID(N'dbo.sp_ProfilesList') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_ProfilesList AS RETURN');
GO
ALTER PROCEDURE dbo.sp_ProfilesList @Token uniqueidentifier,@Search nvarchar(120)=N'',@Lookup bit=0 AS
BEGIN
 SET NOCOUNT ON;
 IF NOT(dbo.fn_Can(@Token,16)=1 OR (@Lookup=1 AND (dbo.fn_Can(@Token,2)=1 OR dbo.fn_Can(@Token,4)=1))) THROW 50001,N'No tienes permiso para consultar perfiles.',1;
 SELECT p.Id,p.Name,p.Description,p.IsActive,p.RowVersion,ISNULL((SELECT SUM(PermissionBit) FROM dbo.ProfilePermissions pp WHERE pp.ProfileId=p.Id),0) PermissionMask FROM dbo.Profiles p WHERE @Search=N'' OR CHARINDEX(@Search,p.Name)>0 ORDER BY p.Name;
END;
GO
IF OBJECT_ID(N'dbo.sp_ProfileSave') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_ProfileSave AS RETURN');
GO
ALTER PROCEDURE dbo.sp_ProfileSave @Token uniqueidentifier,@Id int=NULL,@Name nvarchar(80),@Description nvarchar(250),@Active bit,@Mask int,@Version binary(8)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION; EXEC dbo.sp_AdminLock;
 IF @Mask<0 OR @Mask>255 THROW 50002,N'Permisos inválidos.',1;
 IF @Id IS NULL BEGIN
  IF dbo.fn_Can(@Token,32)=0 THROW 50001,N'No tienes permiso para crear perfiles.',1;
  INSERT dbo.Profiles(Name,Description,IsActive) VALUES(@Name,@Description,@Active); SET @Id=SCOPE_IDENTITY();
 END ELSE BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Profiles WHERE Id=@Id AND RowVersion=@Version) THROW 50005,N'El perfil cambió. Actualiza el listado antes de guardar.',1;
  IF EXISTS(SELECT 1 FROM dbo.Profiles WHERE Id=@Id AND (Name<>@Name OR Description<>@Description OR ISNULL((SELECT SUM(PermissionBit) FROM dbo.ProfilePermissions WHERE ProfileId=@Id),0)<>@Mask)) AND dbo.fn_Can(@Token,64)=0 THROW 50001,N'No tienes permiso para editar perfiles.',1;
  IF EXISTS(SELECT 1 FROM dbo.Profiles WHERE Id=@Id AND IsActive<>@Active) AND dbo.fn_Can(@Token,128)=0 THROW 50001,N'No tienes permiso para cambiar el estado del perfil.',1;
  IF dbo.fn_Can(@Token,64)=0 AND dbo.fn_Can(@Token,128)=0 THROW 50001,N'No tienes permiso para modificar perfiles.',1;
  UPDATE dbo.Profiles SET Name=@Name,Description=@Description,IsActive=@Active WHERE Id=@Id;
 END;
 DELETE dbo.ProfilePermissions WHERE ProfileId=@Id;
 INSERT dbo.ProfilePermissions(ProfileId,PermissionBit) SELECT @Id,BitValue FROM dbo.Permissions WHERE (@Mask & BitValue)=BitValue;
 EXEC dbo.sp_AdminGuard;
 INSERT dbo.AuditLog(ActorId,Action,EntityId) VALUES(dbo.fn_Actor(@Token),'ProfileSaved',@Id);
 COMMIT; SELECT @Id Id;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF OBJECT_ID(N'dbo.sp_UserSave') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_UserSave AS RETURN');
GO
ALTER PROCEDURE dbo.sp_UserSave @Token uniqueidentifier,@Id int=NULL,@Username nvarchar(64),@FullName nvarchar(120),@Email nvarchar(254)=NULL,@ProfileId int,@Active bit,@Version binary(8)=NULL,@Hash binary(32)=NULL,@Salt binary(32)=NULL,@Iterations int=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION; EXEC dbo.sp_AdminLock;
 IF @Id IS NULL BEGIN
  IF dbo.fn_Can(@Token,2)=0 THROW 50001,N'No tienes permiso para crear usuarios.',1;
  IF @Hash IS NULL OR @Salt IS NULL OR @Iterations IS NULL THROW 50002,N'La contraseña inicial es obligatoria.',1;
  INSERT dbo.Users(Username,FullName,Email,ProfileId,IsActive,PasswordHash,PasswordSalt,PasswordIterations) VALUES(@Username,@FullName,@Email,@ProfileId,@Active,@Hash,@Salt,@Iterations); SET @Id=SCOPE_IDENTITY();
 END ELSE BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@Id AND RowVersion=@Version) THROW 50005,N'El usuario cambió. Actualiza el listado antes de guardar.',1;
  IF EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@Id AND (Username<>@Username OR FullName<>@FullName OR ISNULL(Email,N'')<>ISNULL(@Email,N'') OR ProfileId<>@ProfileId)) AND dbo.fn_Can(@Token,4)=0 THROW 50001,N'No tienes permiso para editar usuarios.',1;
  IF EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@Id AND IsActive<>@Active) AND dbo.fn_Can(@Token,8)=0 THROW 50001,N'No tienes permiso para cambiar el estado del usuario.',1;
  IF dbo.fn_Can(@Token,4)=0 AND dbo.fn_Can(@Token,8)=0 THROW 50001,N'No tienes permiso para modificar usuarios.',1;
  UPDATE dbo.Users SET Username=@Username,FullName=@FullName,Email=@Email,ProfileId=@ProfileId,IsActive=@Active WHERE Id=@Id;
 END;
 EXEC dbo.sp_AdminGuard;
 -- Audit actor is taken from the session even when the actor deactivates their own account.
 INSERT dbo.AuditLog(ActorId,Action,EntityId) SELECT UserId,'UserSaved',@Id FROM dbo.Sessions WHERE Token=@Token;
 COMMIT; SELECT @Id Id;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF OBJECT_ID(N'dbo.sp_PasswordSet') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_PasswordSet AS RETURN');
GO
ALTER PROCEDURE dbo.sp_PasswordSet @Token uniqueidentifier,@UserId int,@Hash binary(32),@Salt binary(32),@Iterations int,@Version binary(8)=NULL,@Reset bit=0 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION; EXEC dbo.sp_AdminLock;
 DECLARE @Actor int=dbo.fn_Actor(@Token);
 IF @Actor IS NULL THROW 50001,N'La sesión no está disponible. Inicia sesión nuevamente.',1;
 IF @Reset=1 BEGIN
  IF dbo.fn_Can(@Token,4)=0 THROW 50001,N'No tienes permiso para restablecer contraseñas.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@UserId AND RowVersion=@Version) THROW 50005,N'El usuario cambió. Actualiza el listado.',1;
 END ELSE IF @Actor<>@UserId THROW 50001,N'No puedes cambiar esta contraseña.',1;
 UPDATE dbo.Users SET PasswordHash=@Hash,PasswordSalt=@Salt,PasswordIterations=@Iterations,PasswordAlgorithm='PBKDF2-SHA256-v1',MustChangePassword=@Reset,FailedAttempts=0,LockedUntil=NULL WHERE Id=@UserId;
 DELETE dbo.Sessions WHERE UserId=@UserId AND (@Reset=1 OR Token<>@Token);
 INSERT dbo.AuditLog(ActorId,Action,EntityId) VALUES(@Actor,CASE WHEN @Reset=1 THEN 'PasswordReset' ELSE 'PasswordChanged' END,@UserId);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF OBJECT_ID(N'dbo.sp_SeedAdmin') IS NULL EXEC(N'CREATE PROCEDURE dbo.sp_SeedAdmin AS RETURN');
GO
ALTER PROCEDURE dbo.sp_SeedAdmin @Username nvarchar(64),@Hash binary(32),@Salt binary(32),@Iterations int AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION; EXEC dbo.sp_AdminLock;
 IF NOT EXISTS(SELECT 1 FROM dbo.Users) BEGIN
  DECLARE @ProfileId int;
  SELECT @ProfileId=Id FROM dbo.Profiles WHERE Name=N'Administradores';
  IF @ProfileId IS NULL BEGIN INSERT dbo.Profiles(Name,Description) VALUES(N'Administradores',N'Control completo del sistema'); SET @ProfileId=SCOPE_IDENTITY(); END;
  INSERT dbo.ProfilePermissions(ProfileId,PermissionBit) SELECT @ProfileId,BitValue FROM dbo.Permissions p WHERE NOT EXISTS(SELECT 1 FROM dbo.ProfilePermissions pp WHERE pp.ProfileId=@ProfileId AND pp.PermissionBit=p.BitValue);
  INSERT dbo.Users(Username,FullName,ProfileId,PasswordHash,PasswordSalt,PasswordIterations) VALUES(@Username,N'Administrador',@ProfileId,@Hash,@Salt,@Iterations);
  INSERT dbo.AuditLog(Action,EntityId) VALUES('AdminSeeded',SCOPE_IDENTITY());
 END;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE Version=1) INSERT dbo.SchemaVersions(Version) VALUES(1);
