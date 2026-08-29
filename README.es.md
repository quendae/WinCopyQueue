<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <strong>Español</strong> · <a href="README.pt.md">Português</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

# WinCopyQueue

WinCopyQueue añade al Explorador de Windows una cola sencilla para copiar y mover archivos. En lugar de ejecutar varias transferencias al mismo tiempo, las procesa de forma secuencial: una sesión tras otra y un archivo cada vez.

La aplicación funciona desde la bandeja del sistema y no mantiene una ventana principal abierta permanentemente. El panel compacto de la cola aparece únicamente cuando se añade una transferencia, puede ocultarse en cualquier momento y las operaciones continúan en segundo plano.

## Descarga

Versión actual: **1.0.0**

- [Descargar el instalador de WinCopyQueue 1.0.0](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [Descargar WinCopyQueue.exe independiente](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [Ver la versión v1.0.0](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue funciona en **Windows 10 1809 o posterior**, incluido Windows 11. El instalador se aplica al usuario actual y no requiere permisos de administrador.

> Este repositorio no incluye actualmente un archivo `LICENSE`.

## Cómo funciona

1. Inicia `WinCopyQueue.exe`.
2. En el Explorador de Windows, copia o corta archivos normalmente con `Ctrl+C` / `Ctrl+X`.
3. En la carpeta de destino, pulsa `Ctrl+V` o selecciona **Pegar con WinCopyQueue** en el menú contextual.

Si ya hay una transferencia en curso, la siguiente simplemente se añade al final de la cola. Así se evita que varias operaciones grandes compitan al mismo tiempo por el mismo disco.

En Windows 11, la entrada estática del menú contextual puede aparecer dentro de **Mostrar más opciones**.

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="WinCopyQueue durante una transferencia activa" width="480">
</p>

## Funciones principales

- copiar y mover archivos individuales o carpetas completas,
- varias sesiones independientes dentro de una única cola secuencial,
- pausar y reanudar toda la cola o archivos individuales,
- cancelar una sesión completa o un archivo seleccionado,
- cancelar una sesión sin eliminar los archivos que ya se hayan copiado correctamente,
- gestión de conflictos comparando ruta, tamaño y fecha de modificación,
- decisiones **Reemplazar**, **Omitir** y **Cancelar sesión**, con opción de aplicar la elección a conflictos posteriores,
- panel compacto con archivo actual, progreso, número de archivos y velocidad de transferencia,
- lista virtualizada y desplegable de todos los archivos y sus estados,
- historial de sesiones completadas, canceladas y con error,
- notificaciones del sistema al añadir, completar o fallar una transferencia,
- inicio automático opcional con Windows,
- ocho idiomas de interfaz: inglés, polaco, alemán, francés, español, portugués, chino simplificado y japonés.

## Copias y movimientos más seguros

WinCopyQueue no escribe un archivo incompleto directamente con su nombre final. Los datos se escriben primero en un archivo temporal `*.queue-part-*` y solo se publican con el nombre definitivo cuando la transferencia termina correctamente.

Para las copias normales se puede activar una verificación **SHA-256** opcional. WinCopyQueue calcula el hash del origen durante la copia y vuelve a leer el archivo de destino para comparar el resultado.

Al mover archivos entre volúmenes distintos, la verificación se realiza automáticamente antes de eliminar el origen, independientemente de la opción seleccionada en la interfaz. Si falla la copia, la verificación o la finalización, el archivo de origen permanece intacto.

## Panel de cola y bandeja del sistema

El panel de la cola se abre automáticamente cuando se añade una transferencia y aparece cerca de la esquina inferior derecha de la pantalla sin quitar el foco al Explorador. Puede minimizarse mientras las transferencias continúan en segundo plano.

Haz doble clic en el icono de la bandeja o selecciona **Mostrar cola** para abrir de nuevo el panel. Desde el menú de la bandeja también se puede pausar o reanudar toda la cola, activar o desactivar el inicio automático, reparar la integración con el Explorador y cerrar la aplicación.

## Conflictos de archivos

Si ya existe un archivo con el mismo nombre en el destino, WinCopyQueue muestra ambos archivos junto con su tamaño y fecha de modificación. Hay tres acciones disponibles:

- **Reemplazar**,
- **Omitir**,
- **Cancelar sesión**.

Reemplazar u Omitir también puede aplicarse a todos los conflictos posteriores de la misma sesión.

## Configuración y diagnóstico

La configuración del usuario se guarda en:

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

El registro de diagnóstico se encuentra en:

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

El idioma seleccionado y la preferencia de verificación SHA-256 se recuerdan entre ejecuciones.

## Línea de comandos

WinCopyQueue también puede recibir transferencias directamente desde la línea de comandos:

```powershell
WinCopyQueue.exe --copy "D:\Destino" "D:\Archivo.txt" "D:\Carpeta"
WinCopyQueue.exe --move "D:\Destino" "D:\Archivo.txt"
WinCopyQueue.exe --paste "D:\Destino"
```

Volver a iniciar la aplicación no crea una segunda cola. Los comandos se envían al proceso principal mediante una named pipe.

## Compilar el proyecto

Se requiere .NET 10 SDK.

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

Ejecutar la aplicación desde el repositorio:

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### Pruebas

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Las pruebas de humo del núcleo realizan operaciones reales sobre archivos temporales aislados y comprueban, entre otras cosas, el orden de las sesiones, los conflictos, SHA-256, pausa/reanudación, cancelación, limpieza del historial y controles por archivo. Las pruebas de la aplicación cubren WPF, localización, el diálogo de conflictos y escenarios de cierre.

### Instalador

Crear el instalador con:

```powershell
.\installer\Build-Installer.ps1
```

El script publica una compilación autocontenida `win-x64` y crea un instalador con Inno Setup 7. Los binarios finales se publican en [Releases](https://github.com/quendae/WinCopyQueue/releases) y no se almacenan en el repositorio.

## Estructura del proyecto

```text
src/WinCopyQueue.Core/       lógica de la cola y operaciones con archivos
src/WinCopyQueue.App/        aplicación WPF, bandeja e integración con Explorer
tests/                       pruebas de humo del núcleo y de la aplicación
installer/                   definición de Inno Setup y script de compilación
```
