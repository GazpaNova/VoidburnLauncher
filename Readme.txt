VOIDBURN — ONE-CLICK RUNELITE

Run VoidburnSetup.exe and click Install and Play. Setup downloads the tested RSProx installer directly from its official GitHub release, checks its SHA256, installs Java connection tools if needed, prepares a private Voidburn profile and creates your Voidburn desktop shortcut.

RuneLite opens directly at the server login. Login Utilities uses the original RuneScape login controls: sign in once, then use the native Play Now button on subsequent launches. Logging out resets the native one-use ticket and prepares Play Now for another login in the same RuneLite window. It stores a rotating server token encrypted for your Windows account; it does not save your password. To return to another account, open RuneLite plugin settings > Login Utilities > Forget account. You can turn off Remember account there. Expired or rejected tokens return to the normal login screen. No RSProx menu, SSH access or Jagex account is needed. First launch downloads RuneLite; later launches reuse the downloaded files. Internet access is required.

Use the Voidburn desktop shortcut for later sessions. The shortcut shows the Voidburn logo, checks for published client updates, and displays download progress when an update is available. It then prepares RuneLite and closes the loading screen when RuneLite opens. Updates preserve the private profile, plugins and screenshots. It does not open the installer or run installation again. The setup screen appears only when you run VoidburnSetup.exe directly. Log out of the same character in the browser or another client before logging in. Character progress stays on the server. Renaming the launcher does not reset your character.

The proxy runs in the background while RuneLite is open and stops after its tracked client processes close. Each Voidburn instance uses its own private configuration and selects only the Voidburn server. It never chooses an official RuneScape target as a fallback. Your regular RSProx targets and profile files are not modified.

Voidburn helper, runtime and logs:
  %LOCALAPPDATA%\Voidburn
Voidburn client/proxy profile:
  %LOCALAPPDATA%\Voidburn\profile
RuneLite profile inside that private home:
  %LOCALAPPDATA%\Voidburn\profile\.rlcustom

The official RSProx Windows installer supplies Java at %LOCALAPPDATA%\RSProx\jdk if it is not already installed. You never need to open its interface. Voidburn uses a pinned RSProx 1.0.5 proxy runtime, reusing matching cached libraries read-only or downloading them from the official CDN. Every library is checked against the official bootstrap's SHA256 hashes. RuneLite's launcher downloads the supported client into the private profile as usual.

No game passwords, Jagex tokens, SSH credentials or server private keys are embedded. The launcher does not ask for your game password; type that only into the RuneLite login screen.

If opening fails, the setup dialog shows an error. Connection logs are under the private profile's .rsprox\logs folder; setup errors are in %LOCALAPPDATA%\Voidburn\setup.log. Starting another session while one is open is refused; close the existing Voidburn RuneLite window first.

This 2.1.10 package connects to the live game through the layout gateway on port 43596. The layout fix originally tested in TSPS PR 1813 is now merged upstream. It includes the same Fixed/Resizable Classic/Resizable Modern layout fixes previously offered in Revenant Layout Test. This patch lives on the server gateway, not in the RuneLite client. It uses the same characters and game settings as the regular gateway. It includes the friends-list addon that displays online friends as green Online. Run this updated setup once and reopen RuneLite to install it into the private Voidburn profile. Server gameplay changes are managed separately.

To uninstall the Voidburn helper, close its RuneLite window, remove its Voidburn shortcuts and its %LOCALAPPDATA%\Voidburn folder. Java/RSProx has a normal Windows uninstaller; other RSProx profiles remain separate.

Website: https://osrs.voidburn.net/
RSProx source: https://github.com/blurite/rsprox
RuneLite source: https://github.com/runelite/runelite

Voidburn is a privately packaged, unsigned launcher, not an official Jagex, RuneLite or RSProx release. The supplied logo is the user's original artwork; the installer UI uses dark charcoal, purple and teal, with no yellow UI text or controls.

VERSION 2.0.4 — STARTUP REPAIR AND DIAGNOSTICS
This release fixes Java argument-file encoding failures with Windows paths containing accents and uses compatible short paths where available for characters outside the Windows code page. Java startup output is captured before the proxy initializes. Setup also repairs a missing/incomplete bundled Java runtime, even if RSProx.exe is present.

Install on each PC using VoidburnSetup.exe. Copying another player's Voidburn folder is not a standalone installation: Java normally lives separately under %LOCALAPPDATA%\RSProx\jdk, and runtime paths belong to that PC. Setup regenerates the current user's runtime paths. Game characters remain saved on the server.

If startup still fails, send the exact new error and the setup.log named by the dialog. Normal setup log: %LOCALAPPDATA%\Voidburn\setup.log. If that location cannot be written, the launcher tries %TEMP%\Voidburn-setup.log. Per-launch diagnostics live in %LOCALAPPDATA%\Voidburn\launch-*\java-startup.log and bootstrap.log. These capture Java failures even when RuneLite has not been downloaded yet. Do not send your entire profile folder.

AUTOMATIC CLIENT UPDATES
Install 2.1.0 once to enable startup update checks. Earlier launchers cannot update themselves.
The installed shortcut checks https://osrs.voidburn.net/downloads/updates/stable.xml. Release details are signed with the Voidburn release key; download size, SHA256 and executable version must match before installation. The previous executable is kept as Voidburn.previous.exe. A failed startup health check restores it. If the update service is unavailable, the installed version opens as usual. This does not update the game server or fetch arbitrary GitHub commits.
Player profile files are not part of the updater payload. Future proxy/runtime changes ship inside a tested launcher release. Logs for update failures are in setup.log.

VERSION 2.1.1 — DEPENDENCY DOWNLOADS AND CONNECTION REPAIRS
The RSProx installer is downloaded from https://github.com/blurite/rsprox/releases/download/v1.0/RSProxSetup.exe only when the Java/RSProx installation needs installation or repair. The download must match the pinned tested SHA256 before it can run. If upstream changes that release file, setup refuses it until the server owner reviews the replacement. No dependency installer is embedded in Voidburn.
RSProx is installed automatically after you choose Install and Play. The local proxy is required to connect this RuneLite client to Voidburn. This package remains unsigned; removing the embedded installer does not guarantee that antivirus products will accept it.

CONNECTION LIBRARY FALLBACK
If cdn.rsprox.net cannot be reached, setup retries and then uses https://osrs.voidburn.net/downloads/runtime/rsprox-1.0.5/ for the remaining pinned connection libraries. These are unchanged copies of the same 131 upstream JARs. Both sources must match the original embedded SHA256 manifest. Already verified files are reused on retry; incomplete files are not installed. Runtime library downloads have timeouts and size limits.
These connection repairs are included in the 2.1.1 release.

ACCENTED WINDOWS PROFILE SOCKET REPAIR
The pinned RSProx Unix-socket library can fail with a misleading network-down message when its Windows socket path contains accented letters. This release uses an ASCII Windows short-path alias for Java's profile home and launch files, referring to the same existing folders. It does not rename the Windows account, migrate profile files or reset settings. If Windows has no usable ASCII alias, setup reports that limitation rather than changing system settings.

GATEWAY UPDATE — 2.1.1
Connects to the updated Voidburn gateway with upstream PR 1974: welcome-screen Escape handling, modal windows across layouts, V2 target events and clan resynchronization. These protocol fixes run on the server gateway; existing launchers benefit after reconnecting. This installer also includes the previously tested dependency-download, checksum-verified CDN fallback and accented Windows socket-path repairs. It remains unsigned by a Windows publisher. The separate startup-update manifest is authenticated with the Voidburn release key.

VERSION 2.1.2 - PROFILE IMPORT FILE PICKER
Repairs Import Profile and other Java file pickers on Windows by retaining the real Windows profile environment. Java still uses the same private Voidburn profile directory, including the accented-path socket repair. Existing RuneLite settings and player progress are unchanged. Close and reopen Voidburn to install the startup update.

VERSION 2.1.3 - VOIDBURN HISCORES
The built-in RuneLite Hiscores panel now looks up Voidburn characters through the Voidburn Hiscores addon, enabled by default in this private profile. Names missing from Voidburn do not fall back to OSRS. Levels, XP and ranks come from saved Voidburn characters; the service refreshes every 30 seconds after saves. Recorded boss/activity counters are included; unavailable scores remain unranked. Normal includes all main characters; Ironman includes all iron types, with separate Hardcore/Ultimate filters. League/Deadman/Tournament/Fresh Start profiles are not supported by this server. Your normal RuneLite installation is unaffected. Close RuneLite and reopen Voidburn to receive the update.

VERSION 2.1.4 - PREINSTALLED DROP-ALL PLUGIN
Includes Revenant Drop-All in the private Voidburn RuneLite profile. Existing installations receive it through the signed launcher update on the next startup. Close RuneLite and reopen the Voidburn desktop shortcut. Enable Revenant Drop-All in RuneLite's plugin list, then choose items in its settings or Shift + right-click an inventory item and choose Add to Drop-All. All items and Shift-click Drop-All remain optional and off by default. The plugin itself is off by default to avoid accidental drops. Existing plugin settings are preserved; changed plugin JARs are backed up before replacement. No game server restart is needed.

VERSION 2.1.5 - VOIDBURN GATEWAY LATENCY
Voidburn Ping is preinstalled and enabled by default. While logged in to Voidburn, it displays estimated TCP connection latency to the remote gateway (186.101.219.92:43596), sampled every five seconds with a two-second timeout on a background worker. It replaces the built-in World Hopper ping overlay only during the Voidburn session and restores that overlay when disabled or outside the session. No stock plugin settings are modified. Failed samples display Unavailable instead of 0 ms or a stale reading. This measures network connection latency, not game-tick or server-processing delay. Close RuneLite and reopen the Voidburn desktop shortcut to receive this signed startup update. Drop-All, Hiscores, existing profile settings and the gateway layout patch are retained.

VERSION 2.1.6 - NATIVE-STYLE YELLOW PING DISPLAY
Voidburn Ping now draws plain yellow text at the top of the screen in the same position and style as RuneLite's World Hopper ping display, adjusting around the visible side panel. There is no background panel or latency-dependent colour change. It still measures the remote Voidburn gateway, rather than the local RSProx connection. Existing settings, Drop-All, Hiscores and startup updates are preserved.

VERSION 2.1.7 - REMEMBERED ACCOUNT AND PLAY NOW
The RuneLite login screen now offers a Voidburn Sign in button. Enter your existing game username and password and leave Remember me on this PC selected. The password is saved only after successful login, encrypted with Windows Data Protection for your current Windows user. Later launches offer Play now for that account. Use another account opens a new sign-in form; Forget account removes the encrypted local login. Use standard login returns to the unmodified RuneLite login controls. This is a local remembered password, not a Jagex session or the browser's token system. No authentication changes are made to the server or gateway. The password never appears in command-line arguments, settings.properties, setup logs or a plaintext file. The encrypted store is %LOCALAPPDATA%\Voidburn\remembered-account.bin. Close RuneLite and reopen the Voidburn desktop shortcut to update. Existing characters, plugin settings, yellow ping, Drop-All, Hiscores and the layout fix are preserved.

VERSION 2.1.8 - RESTORE THE ORIGINAL RUNELITE LOGIN SCREEN
Removes the additional remembered-login panel and sign-in dialog introduced in 2.1.7. The launcher disables that specific addon before starting RuneLite, preserving its encrypted remembered-account file and existing plugin settings. RuneLite returns to its original login screen. Native Play now integration is not implemented in this release. The yellow top-screen ping display, Drop-All, Hiscores and layout fixes remain available. Close RuneLite and reopen the Voidburn shortcut to update.
