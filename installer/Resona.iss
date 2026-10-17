; Script Inno Setup de Resona.
; Compile automatiquement par le workflow GitHub (.github/workflows/build.yml), qui passe ces variables :
;   /DAppVersion=3.2.0   /DSourceDir=<dossier de publication>   /DOutputName=<nom du .exe genere>

#ifndef SourceDir
  #define SourceDir "..\src\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif
#ifndef AppVersion
  ; Pas de version fournie (compilation a la main) : on la lit dans Resona.exe (donc dans le .csproj), jamais "0.0.0".
  #define MajV 0
  #define MinV 0
  #define RevV 0
  #define BldV 0
  #expr ParseVersion(AddBackslash(SourceDir) + "Resona.exe", MajV, MinV, RevV, BldV)
  #define AppVersion Str(MajV) + "." + Str(MinV) + "." + Str(RevV)
#endif
#ifndef OutputName
  #define OutputName "Resona-Setup"
#endif

[Setup]
; Ne jamais changer cet identifiant : c'est lui qui permet a Windows de reconnaitre une mise a jour de Resona.
AppId={{A3F5C8E1-7B24-4D96-8E0F-5C1A9D2B6E47}
AppName=Resona
AppVersion={#AppVersion}
AppPublisher=Loukkkk
AppPublisherURL=https://github.com/Loukkkk/Resona
AppSupportURL=https://github.com/Loukkkk/Resona/issues
AppUpdatesURL=https://github.com/Loukkkk/Resona/releases

; Installation pour l'utilisateur courant (aucun droit administrateur) : %LOCALAPPDATA%\Programs\Resona.
; Le raccourci du menu Demarrer est le meme fichier (Resona.lnk) que celui que l'app recree a chaque lancement : pas de doublon.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Resona
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Si Resona est ouvert pendant une mise a jour, l'installateur le ferme proprement.
CloseApplications=yes
RestartApplications=no

SetupIconFile=..\src\icon.ico
UninstallDisplayIcon={app}\Resona.exe
UninstallDisplayName=Resona
OutputDir=output
OutputBaseFilename={#OutputName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Integration a Windows creee par l'application elle-meme au lancement (types de fichiers audio, menu "Ouvrir avec Resona") :
; on la retire proprement a la desinstallation.
[Registry]
Root: HKCU; Subkey: "Software\Classes\Resona.AudioFile"; ValueType: none; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ResonaOpen"; ValueType: none; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Classes\Applications\Resona.exe"; ValueType: none; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Resona"; ValueType: none; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: none; ValueName: "Resona"; Flags: uninsdeletevalue dontcreatekey

[Icons]
Name: "{autoprograms}\Resona"; Filename: "{app}\Resona.exe"
Name: "{autodesktop}\Resona"; Filename: "{app}\Resona.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Resona.exe"; Description: "{cm:LaunchProgram,Resona}"; Flags: nowait postinstall skipifsilent
