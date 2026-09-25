#!/usr/bin/env python3
"""Builds the .esriAddInX package that ArcGIS Pro installs.

Why this exists
----------------
Packaging normally comes from the Pro SDK's Visual Studio project templates. Without Visual
Studio, `dotnet build` still produces the DLL without trouble, but nothing turns that DLL into
an installable package — this script fills that one gap, and the package format turns out to
be simple enough that it does not need much.

The package format
------------------
RegisterAddIn.dll, the installer Pro ships under bin/, references
`System.IO.Compression.ZipFile`, `ZipArchive`, `PackageDigitalSignatureManager` and a pattern
matching the `.esriAddInX` extension. In other words: an OPC package — the same container
format used by a `.docx` file — under a different extension. That is where the three pieces
below come from:

    [Content_Types].xml     required by OPC: declares the MIME type for each extension used
    Config.daml             the manifest, at the root of the package
    Install/…               the add-in's compiled assemblies

What is deliberately left out
------------------------------
- Pro's own assemblies. Pro provides them at runtime; bundling them is the usual way to end
  up with two different versions of the same type loaded at once (see `Private=false` in the
  .csproj, the other half of that same decision).
- The .pdb and .deps.json files. Pro's loader does not use them, and the .pdb embeds absolute
  paths from the machine that compiled it.
- A digital signature. `ArcGISSignAddIn.exe` exists and can sign a package, but an unsigned
  add-in still loads as long as the machine's policy allows user add-ins at all
  (`AreUserAddInsEnabled` in Pro's own framework).
"""

from __future__ import annotations

import argparse
import zipfile
from pathlib import Path

#: MIME type per extension. OPC requires every extension present in the package to be
#: declared; a missing one makes the whole package invalid, and the installer will not say
#: which extension was the problem.
TIPOS = {
    "daml": "text/xml",
    "dll": "application/octet-stream",
    "png": "image/png",
    "xml": "text/xml",
}

_CONTENT_TYPES = (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">\n'
    + "".join(f'  <Default Extension="{ext}" ContentType="{tipo}"/>\n' for ext, tipo in sorted(TIPOS.items()))
    + "</Types>\n"
)

#: What gets copied from the build output folder. Listed explicitly rather than "everything
#: in the folder": a Release build directory accumulates leftovers from earlier builds, and an
#: add-in that quietly ships a stray DLL is a problem that surfaces on someone else's machine.
SUFIJOS_QUE_VIAJAN = (".dll",)


def empaquetar(compilado: Path, config: Path, salida: Path) -> list[str]:
    """Writes the .esriAddInX package and returns what was placed inside it."""
    if not config.is_file():
        raise SystemExit(f"no está el manifiesto: {config}")
    ensamblados = sorted(p for p in compilado.glob("*") if p.suffix.lower() in SUFIJOS_QUE_VIAJAN)
    if not ensamblados:
        raise SystemExit(f"no hay ensamblados que empaquetar en {compilado} — ¿se compiló?")

    salida.parent.mkdir(parents=True, exist_ok=True)
    dentro = []
    with zipfile.ZipFile(salida, "w", zipfile.ZIP_DEFLATED) as paquete:
        paquete.writestr("[Content_Types].xml", _CONTENT_TYPES)
        dentro.append("[Content_Types].xml")
        paquete.write(config, "Config.daml")
        dentro.append("Config.daml")
        for ensamblado in ensamblados:
            destino = f"Install/{ensamblado.name}"
            paquete.write(ensamblado, destino)
            dentro.append(destino)
    return dentro


def main(argv: list[str] | None = None) -> int:
    analizador = argparse.ArgumentParser(
        prog="empaquetar.py",
        description="Arma el .esriAddInX a partir de la carpeta de compilación y el Config.daml.",
    )
    aqui = Path(__file__).resolve().parent
    analizador.add_argument("--compilado", type=Path, default=aqui / "bin" / "Release")
    analizador.add_argument("--config", type=Path, default=aqui / "Config.daml")
    analizador.add_argument("--salida", type=Path, default=aqui / "dist" / "ArcGISMentor.esriAddInX")
    a = analizador.parse_args(argv)

    dentro = empaquetar(a.compilado, a.config, a.salida)
    print(f"{a.salida}  ({a.salida.stat().st_size:,} bytes)")
    for nombre in dentro:
        print(f"  {nombre}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
