# Admit One · Administración de escritorio

Aplicación WPF en C# para Windows y **.NET Framework 4.8**, con inicio de sesión, administración de usuarios y perfiles. Interfaz en español e identidad visual basada en https://www.admit-one.eu/. El logotipo incluido proviene del sitio oficial, solicitado como referencia por el propietario del proyecto.

## Ejecutar

En esta máquina la conexión ya está configurada para el usuario de Windows que realizó la instalación. Abre `artifacts/AdmitOne/AdmitOne.Desktop.exe`. La cuenta inicial es **Admin**; utiliza la contraseña temporal indicada en la conversación. Deberás sustituirla antes de entrar al panel. Las nuevas contraseñas requieren entre 12 y 256 caracteres.

Al cerrar sesión vuelve la pantalla de acceso. Cerrar la ventana termina la aplicación; los tokens almacenados expiran a las ocho horas. Las operaciones validan el estado del usuario, del perfil y los permisos vigentes en SQL Server.

## Configurar en otra instalación

1. Instala .NET Framework 4.8 y dispón de SQL Server 2014 o posterior con una base existente llamada `CodexDesktopApp`.
2. Extrae el paquete completo, conservando la carpeta `database` junto al instalador.
3. Ejecuta `AdmitOne.Installer.exe --configure` desde una terminal y proporciona servidor, usuario SQL y contraseña. Las contraseñas se capturan sin mostrarse.
4. Si no existen usuarios, el instalador solicita el usuario administrador y su contraseña temporal. Si ya existen, conserva todos los usuarios, perfiles y contraseñas.
5. Ejecuta `AdmitOne.Desktop.exe` con el mismo usuario de Windows.

El instalador no crea bases ni modifica otras bases. Las migraciones numeradas se instalan en una transacción, se registran en `dbo.SchemaVersions` y se omiten si la versión ya existe. El sembrado del administrador es independiente, transaccional y solo se realiza si no hay usuarios. Una interrupción antes del sembrado se resuelve repitiendo la instalación.

La conexión se guarda cifrada con **DPAPI CurrentUser** en `%LOCALAPPDATA%\AdmitOne\connection.dpapi`. No se distribuye con el paquete ni puede copiarse para otro usuario. Para cambiarla vuelve a ejecutar el instalador con `--configure`. No existe una contraseña SQL incrustada en el código.

## Desarrollo

**Visual Studio 2022:** abre `AdmitOne.sln`, selecciona `AdmitOne.Desktop` como proyecto de inicio y compila Debug o Release. Instala la carga de trabajo de desarrollo de escritorio de .NET y el targeting pack de .NET Framework 4.8. Los estilos están en `Theme.xaml`; las vistas se componen en C# y los modelos de vista están separados.

**Visual Studio Code:** abre esta carpeta y usa Terminal → Ejecutar tarea → Compilar Debug, Compilar Release o Ejecutar Admit One. Las tareas usan el MSBuild de Visual Studio/Build Tools, no el SDK moderno de .NET. VS Code permite editar todos los archivos; no proporciona el diseñador WPF de Visual Studio.

También puedes ejecutar:

```powershell
./scripts/build.ps1 -Configuration Release
./scripts/package.ps1 -Configuration Release
```

No se requieren paquetes NuGet. Los proyectos usan las bibliotecas de .NET Framework 4.8. `Directory.Build.props` trata las advertencias de compilación como errores.

## Operación

- Los usuarios tienen un único perfil. Se permite buscar, crear, editar, activar/desactivar y restablecer contraseñas temporales.
- Los perfiles definen ocho permisos: consultar, crear, editar y activar/desactivar para usuarios y perfiles. Para operar mediante la pantalla del módulo se necesita el permiso de consulta correspondiente.
- La desactivación conserva los registros. No se admite una operación que deje al sistema sin un usuario activo con perfil activo y los ocho permisos.
- Las contraseñas se derivan con PBKDF2-SHA256, 210 000 iteraciones y un salt aleatorio de 32 bytes por credencial. La cuenta temporal inicial es la única excepción al mínimo de 12 caracteres.
- Cinco fallos consecutivos bloquean nuevos accesos durante 15 minutos. Los mensajes de acceso no revelan si existe el usuario.
- El restablecimiento invalida las sesiones del usuario. Una nueva contraseña propia invalida sus otras sesiones.
- Las modificaciones usan `rowversion`; ante conflicto actualiza el listado antes de volver a editar.

## Diagnósticos y límites

Los errores inesperados muestran una referencia. Busca esa referencia en `%LOCALAPPDATA%\AdmitOne\logs\yyyy-MM-dd.log`. Se registran fecha UTC, tipo de excepción, código y pila; se omiten mensajes del proveedor para evitar que se filtren datos de conexión. Las validaciones de negocio se muestran en la pantalla. La auditoría de accesos y modificaciones reside en `dbo.AuditLog`; no almacena contraseñas. No se envían correos ni notificaciones externas.

La versión local utiliza el usuario SQL `sa` suministrado. **Tiene privilegios elevados:** DPAPI protege el archivo en reposo, pero el proceso iniciado por ese usuario puede descifrarlo. Esta arquitectura de cliente confiable con conexión directa no sustituye una API ni es adecuada para distribuir credenciales `sa` a equipos no confiables. Para un despliegue posterior debe usarse una cuenta SQL restringida y revisarse la frontera de autenticación. Los procedimientos validan tokens y permisos, pero un propietario con acceso SQL privilegiado puede modificar directamente los datos.

El acceso local al servidor instalado usa la configuración de transporte existente. Para acceso remoto debe configurarse TLS con un certificado válido; el código no deshabilita la validación de certificados.

## Pruebas

`AdmitOne.Tests.exe` ejecuta las pruebas locales de contraseñas. `AdmitOne.Tests.exe --database` ejecuta además las pruebas de integración contra la configuración DPAPI y solicita la contraseña actual de Admin. Las pruebas de instalación inicial y cambio obligatorio presuponen que Admin aún tiene su contraseña temporal.

Cada escenario de integración usa una transacción que se revierte; se compara el estado de tablas antes y después para verificar la limpieza. La prueba final de reconexión genera exclusivamente un acceso y cierre de sesión reales en la auditoría. Las imágenes de las vistas se generan en `artifacts/screenshots` al 100 % y 150 % para inspección visual. Son renderizados WPF, no una modificación del escalado global de Windows.

Consulta `VALIDATION.md` para los resultados y cualquier limitación pendiente.
