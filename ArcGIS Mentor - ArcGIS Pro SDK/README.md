# ArcGIS Mentor — extensión del asistente de IA de ArcGIS Pro

Add-in de ArcGIS Pro (.NET, Pro SDK) que conecta el asistente de IA nativo de Pro con el
catálogo de herramientas de geoprocesamiento propias de una organización — sus toolboxes
`.pyt` y `.atbx`, no las herramientas de sistema de Esri.

El asistente de Pro ya sabe responder sobre las herramientas que trae ArcGIS de fábrica.
No tiene forma de saber qué hay en la toolbox que un equipo se escribió para sí mismo, ni
si esas herramientas se pueden encadenar entre sí sin romperse. Este add-in cierra esa
brecha registrando cinco funciones que el asistente puede invocar durante la conversación:
buscar una herramienta propia por lo que la persona quiere hacer, verificar si una
secuencia de varias se sostiene, diagnosticar el contrato de una toolbox, listar el
inventario completo y abrir el formulario real de una herramienta ya precargado.

## Qué hay en esta carpeta

| Archivo | Qué es |
|---|---|
| `AsistenteMentor.cs` | La extensión del asistente: las cinco funciones y las instrucciones que le dicen al modelo cuándo usarlas. |
| `Module1.cs` | El módulo del add-in. Deliberadamente vacío: separa la pregunta de si Pro carga el add-in de si la extensión funciona. |
| `Rastro.cs` | Deja un registro en disco de que el add-in cargó, para poder confirmarlo sin tener Pro abierto en el momento. |
| `ArcGISMentorAddIn.csproj` | El proyecto, compilable con `dotnet build` sin depender de Visual Studio. |
| `Config.daml` | El manifiesto del add-in: cómo se registra ante el asistente de Pro y qué categoría de Esri reutiliza. |
| `empaquetar.py` | Arma el `.esriAddInX` a mano — el paso que normalmente hacen las plantillas del Pro SDK en Visual Studio. |

## Por qué está construido así

**Compilar sin Visual Studio.** El ProGuide de Esri asume las plantillas de proyecto del
Pro SDK, que vienen con Visual Studio. Sin esas plantillas, `dotnet build` sigue
funcionando: basta con referenciar los ensamblados de una instalación existente de Pro
(`ArcGIS.Desktop.Framework.dll`, `ArcGIS.Core.dll`, `ArcGIS.Desktop.Core.dll`) con
`Private=false`, para que Pro los provea en tiempo de ejecución en lugar de que el add-in
cargue una copia propia. Empaquetar el `.esriAddInX` es el otro paso que normalmente hacen
esas plantillas; `empaquetar.py` lo reemplaza construyendo a mano el contenedor OPC que
Pro espera (el mismo formato contenedor que usa un `.docx`, con otra extensión).

**El catálogo no se lee desde C#.** Cada función de `AsistenteMentor.cs` invoca un
subproceso de Python (`python -m toolscout.pro`) en lugar de reimplementar en C# la
búsqueda, la verificación de cadenas y el diagnóstico de contratos. El motor que hace ese
trabajo — el catálogo SQLite, el analizador estático de los `.pyt`/`.atbx` y las reglas de
verificación — es la parte propia del proyecto y no se publica aquí; esta carpeta cubre la
integración con el Pro SDK, que es lo que trata la charla. El contrato entre los dos
lados es intencionalmente angosto: `toolscout.pro` recibe un comando y devuelve una línea
de texto o JSON por salida estándar, y `AsistenteMentor.cs` no asume nada más sobre cómo se
llegó a esa respuesta.

**Ninguna función escribe en disco.** Diagnosticar una toolbox no corrige nada, y abrir una
herramienta no la ejecuta: deja el formulario real de Pro precargado con lo que la persona
mencionó en la conversación, y es ella quien le da a Ejecutar. Un modelo que se equivoca al
buscar cuesta una mala respuesta; uno que se equivoca al escribir cuesta un archivo.

## Compilar

```
dotnet build -p:ArcGISProBin="C:\Program Files\ArcGIS\Pro\bin"
python empaquetar.py --compilado bin\Release --config Config.daml --salida dist\ArcGISMentor.esriAddInX
```

`ArcGISProBin` solo hace falta si Pro no está instalado en la ruta estándar. El
`.esriAddInX` resultante se instala haciendo doble clic, como cualquier add-in de Pro.
