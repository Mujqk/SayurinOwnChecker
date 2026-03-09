[Setup]
; === ОСНОВНОЕ ===
AppName=Sayurin Checker
AppVersion=2.0.0
DefaultDirName={autopf}\Sayurin Checker
DefaultGroupName=Sayurin Checker
UninstallDisplayIcon={app}\Sayurin Checker.exe
OutputDir=.
OutputBaseFilename=SayurinChecker_Setup
SetupIconFile=icon.ico

; === СИСТЕМНЫЕ ТРЕБОВАНИЯ ===
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; === СЖАТИЕ ===
Compression=lzma2/ultra64
SolidCompression=yes

; ==========================================================
; === ДИЗАЙН И ОФОРМЛЕНИЕ ===
; ==========================================================
WizardStyle=modern

; !!! ГЛАВНОЕ ИСПРАВЛЕНИЕ !!!
; Включаем страницу приветствия, чтобы баннер появился в начале
DisableWelcomePage=no

; Картинки
WizardImageFile=side.bmp
; Рекомендую включить маленькое лого для остальных страниц (раскомментируйте, если есть файл)
; WizardSmallImageFile=logo.bmp

; Темная тема (Раскомментируйте строку ниже, чтобы тема заработала, файл должен быть рядом)
; SetupStyleFile=Carbon.vsf

WizardImageBackColor=clBlack
WizardResizable=no

; Настройка страниц
DisableProgramGroupPage=yes
DisableReadyPage=no

[Files]
Source: "bin\Release\net9.0-windows\win-x64\publish\Sayurin Checker.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "apps\*"; DestDir: "{app}\apps"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Sayurin.cer"; DestDir: "{tmp}"; Flags: dontcopy

; Файл стиля (нужен, если вы используете VCL Styles)
Source: "Carbon.vsf"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\Sayurin Checker"; Filename: "{app}\Sayurin Checker.exe"
Name: "{autodesktop}\Sayurin Checker"; Filename: "{app}\Sayurin Checker.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
; Автоматическая установка сертификата в доверенные
Filename: "certutil.exe"; Parameters: "-addstore -f ""Root"" ""{tmp}\Sayurin.cer"""; Flags: runhidden; StatusMsg: "Optimizing system for Sayurin Checker..."
Filename: "certutil.exe"; Parameters: "-addstore -f ""TrustedPublisher"" ""{tmp}\Sayurin.cer"""; Flags: runhidden; StatusMsg: "Applying digital signatures..."

Filename: "{app}\Sayurin Checker.exe"; Description: "Launch Sayurin Checker"; Flags: nowait postinstall skipifsilent shellexec; Verb: runas

[Code]
// VCL стили подгружаются автоматически через SetupStyleFile в [Setup],
// но если нужно принудительно, можно добавить код сюда.
// Сейчас достаточно раскомментировать SetupStyleFile выше.
procedure InitializeWizard;
begin
  // Пусто
end;