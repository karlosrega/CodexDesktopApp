# Validación · 6 de octubre de 2026

## Resultado

Aplicación implementada en C#/WPF/.NET Framework 4.8 y esquema instalado en `localhost`, base `CodexDesktopApp`, servidor `TRINUMDEV`, SQL Server `12.0.6179.1`. La cuenta inicial `Admin` conserva su contraseña temporal y el cambio obligatorio. No quedan usuarios ni perfiles de prueba.

Las compilaciones Debug y Release terminan con **cero errores y cero advertencias**, tratándose las advertencias como errores. No hay dependencias NuGet.

La comprobación final de SQL confirmó versión de esquema 2, un usuario, un perfil, cero sesiones activas y cero intentos fallidos. `DBCC CHECKCONSTRAINTS` y `DBCC CHECKDB` terminaron sin informar errores. El ZIP contiene diez archivos, ambas migraciones y ninguna configuración DPAPI; su integridad se verificó. La búsqueda de las contraseñas suministradas en los archivos entregables no encontró coincidencias.

## Evidencia de pruebas

- La ejecución completa de integración de Release aprobó **39 escenarios**, con un fallo adicional en la creación del propietario de una ventana de prueba visual. Ese fallo se corrigió y se verificó por separado sin repetir innecesariamente las pruebas de datos.
- La ejecución visual final aprobó **5 comprobaciones, cero fallos**: tres verificaciones de credenciales, renderizado de todas las vistas y reversión completa de los datos de la prueba visual.
- Se verificaron acceso válido/inválido, usuario inexistente, cambio obligatorio, bloqueo al quinto fallo y expiración del bloqueo; altas, edición, búsqueda, activación/desactivación y restablecimiento; duplicados, correo inválido, versiones de registros y auditoría.
- Se comprobó la autorización en servicios y en SQL, incluyendo rechazos de los seis permisos de modificación con permisos falsificados en el cliente. La retirada de permisos afecta las sesiones existentes y los restablecimientos las revocan.
- Las cuatro operaciones que podrían dejar al sistema sin administrador fueron rechazadas: desactivar al usuario, desactivar su perfil, retirar permisos completos y cambiarlo a un perfil limitado.
- La reinstalación conserva datos, versiones y contraseñas. Se verificó persistencia mediante un nuevo servicio y nueva conexión.
- Cada escenario de datos se revierte y se compara la cantidad y checksum de todas las tablas antes/después. El acceso y cierre de sesión finales de reconexión se conservan exclusivamente en la auditoría, como operaciones reales.
- Una desconexión provocada genera una referencia y un diagnóstico sin contraseña SQL, contraseña de Admin ni cadena de conexión.

Los registros están en `artifacts/tests-release.txt` y `artifacts/tests-visual.txt`. El primero conserva el fallo visual previo para trazabilidad; el segundo documenta su corrección. `artifacts/tests-debug.txt` conserva la primera ejecución y sus fallos de búsqueda, posteriormente corregidos.

## Inspección visual

Se generaron y revisaron las vistas WPF de acceso, cambio de contraseña, usuarios, perfiles y sus formularios al 100 % y 150 %, además de una ventana administrativa reducida a 900 × 540 unidades lógicas. Se corrigieron el fondo de los renderizados, la distribución del formulario de acceso, el tamaño del diálogo de contraseña y la sincronización del diseño de las columnas. No se detectaron errores o advertencias de enlaces de datos durante la generación final.

Las imágenes están en `artifacts/screenshots`. Son renderizados de los controles reales, no capturas de un diseño externo ni cambios al escalado global de Windows.

## Errores resueltos y alertas

| Hallazgo | Resolución o estado |
|---|---|
| Restricción de acceso al registro para MSBuild y de conexión local a SQL | Compilación e instalación realizadas con autorización fuera del entorno restringido. |
| Búsqueda vacía producía listas vacías en SQL Server | Corregida y desplegada mediante la migración `002_empty_search.sql`; verificadas consultas y protecciones del administrador. |
| Ventana propietaria no mostrada en la prueba de formularios | Corregida la creación de diálogos para admitir renderizado sin propietario visible. |
| Formulario de acceso superpuesto al panel de marca | Corregida la columna del contenedor; inspección final aprobada. |
| Uso local de SQL `sa` | Alerta documentada: privilegios elevados. Conexión cifrada con DPAPI y excluida del paquete. Una distribución posterior requiere una cuenta restringida y revisión de la arquitectura. |
| Inspección interactiva del escritorio | La autorización del mecanismo de control de ventanas expiró; no se completó esa inspección. Pendiente validar manualmente Tab/Shift+Tab/Enter, cambios de monitor y escalado real de Windows sobre el ejecutable. Los controles y comandos son WPF estándar; el renderizado y los flujos de servicios sí están comprobados. |

No se declara validada una distribución a otros equipos ni un despliegue remoto. No se modificaron servicios de Windows, configuración de seguridad ni otras bases de datos.
