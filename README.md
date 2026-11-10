# Voidburn Launcher

Windows launcher source for connecting to the Voidburn game server with RuneLite through RSProx.

## Download

Get [Voidburn 2.1.11 for Windows](https://github.com/GazpaNova/VoidburnLauncher/releases/tag/v2.1.11). Extract the ZIP and run **VoidburnSetup.exe**, then choose **Install and Play**. Use the Voidburn desktop shortcut for later sessions.

The installer is unsigned in Windows. Hosting it here makes the source and release files available for review. Antivirus products can still flag the download.

Check the release ZIP in PowerShell:

```powershell
Get-FileHash .\Voidburn-2.1.11-Windows.zip -Algorithm SHA256
```

The ZIP should have this SHA-256:

```text
81B69E8606BF564CA6A9BE21FB88007F0A6EE5829DDFFE81D2F3C360656ADBED
```

The extracted installer should have this SHA-256:

```text
E6ED62A2D851E016875F57B1098D921FA211B45163C0B82F90B23B679B528BF2
```

## What the launcher does

- Prepares a separate client profile under `%LOCALAPPDATA%\Voidburn` and opens RuneLite at the Voidburn login.
- Downloads the [official RSProx setup](https://github.com/blurite/rsprox/releases/tag/v1.0) when the Java dependency is missing or needs repair. It checks the download against a pinned SHA-256 before running it.
- Fetches the pinned RSProx runtime libraries from the upstream CDN or the Voidburn mirror. Both sources must match the same hashes in [RuntimeFiles.txt](RuntimeFiles.txt).
- Checks the Voidburn update feed on startup. [AutoUpdate.cs](AutoUpdate.cs) verifies the feed signature and the download's size, checksum, and executable version. This is separate from Windows code signing.
- Login Utilities keeps a remembered session token encrypted for the current Windows account. Use **Login Utilities → Forget account** in RuneLite to clear it.

## Build from source

Run this from the repository root on 64-bit Windows with .NET Framework 4.8 and its C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

```powershell
New-Item -ItemType Directory -Force .\build | Out-Null
.\build.ps1 -Output (Join-Path $PWD 'build\VoidburnSetup.exe')
```

The launcher build uses the checked-in JARs and YamlDotNet DLL as pinned inputs, so it can compile offline. [VoidburnBootstrap.java](VoidburnBootstrap.java) and [addons/](addons/) contain the Java sources for the bootstrap and bundled Voidburn add-ons. [BUILD-INPUTS.sha256](BUILD-INPUTS.sha256) lists the hashes of the bundled binary inputs. The C# build can produce a different EXE checksum because of compiler metadata. Use the release checksums above to verify the published download.

Third-party projects and terms are listed in [Licenses.txt](Licenses.txt) and [the ASM notice](vendor/ASM-LICENSE.txt). See the original projects for [RSProx](https://github.com/blurite/rsprox), [RuneLite](https://github.com/runelite/runelite), and [YamlDotNet](https://github.com/aaubry/YamlDotNet).

## Troubleshooting

Setup errors are recorded in `%LOCALAPPDATA%\Voidburn\setup.log`. If you share a log, send the relevant error and omit account information and profile files.
