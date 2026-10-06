IF DB_NAME()<>N'CodexDesktopApp' THROW 50000,N'Base de datos incorrecta.',1;
GO
ALTER PROCEDURE dbo.sp_UsersList @Token uniqueidentifier,@Search nvarchar(120)=N'' AS
BEGIN
 SET NOCOUNT ON;
 IF dbo.fn_Can(@Token,1)=0 THROW 50001,N'No tienes permiso para consultar usuarios.',1;
 SELECT u.Id,u.Username,u.FullName,u.Email,u.ProfileId,p.Name ProfileName,u.IsActive,u.MustChangePassword,u.RowVersion
 FROM dbo.Users u JOIN dbo.Profiles p ON p.Id=u.ProfileId
 WHERE @Search=N'' OR CHARINDEX(@Search,u.Username)>0 OR CHARINDEX(@Search,u.FullName)>0 ORDER BY u.Username;
END;
GO
ALTER PROCEDURE dbo.sp_ProfilesList @Token uniqueidentifier,@Search nvarchar(120)=N'',@Lookup bit=0 AS
BEGIN
 SET NOCOUNT ON;
 IF NOT(dbo.fn_Can(@Token,16)=1 OR (@Lookup=1 AND (dbo.fn_Can(@Token,2)=1 OR dbo.fn_Can(@Token,4)=1))) THROW 50001,N'No tienes permiso para consultar perfiles.',1;
 SELECT p.Id,p.Name,p.Description,p.IsActive,p.RowVersion,ISNULL((SELECT SUM(PermissionBit) FROM dbo.ProfilePermissions pp WHERE pp.ProfileId=p.Id),0) PermissionMask
 FROM dbo.Profiles p WHERE @Search=N'' OR CHARINDEX(@Search,p.Name)>0 ORDER BY p.Name;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE Version=2) INSERT dbo.SchemaVersions(Version) VALUES(2);
