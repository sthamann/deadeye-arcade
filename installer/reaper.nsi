Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!ifndef VERSION
!error "VERSION is required"
!endif
!ifndef PAYLOAD
!error "PAYLOAD is required"
!endif
!ifndef OUTPUT
!error "OUTPUT is required"
!endif
Name "Deadeye Arcade"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Deadeye Arcade"
InstallDirRegKey HKCU "Software\ReaperArcade\Installer" "Directory"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Deadeye Arcade"
VIAddVersionKey "FileDescription" "Deadeye Arcade Windows installer"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "Copyright 2026 Stefan Hamann"
!define MUI_ABORTWARNING
!define MUI_ICON "${PAYLOAD}\deadeye.ico"
!define MUI_UNICON "${PAYLOAD}\deadeye.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\ReaperArcade.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"
LangString CloseApp ${LANG_ENGLISH} "Please close Deadeye Arcade before installing."
LangString CloseApp ${LANG_GERMAN} "Bitte Deadeye Arcade vor der Installation schließen."
LangString RuntimeFailed ${LANG_ENGLISH} "WebView2 could not be installed. Check your Internet connection and run Setup again."
LangString RuntimeFailed ${LANG_GERMAN} "WebView2 konnte nicht installiert werden. Internetverbindung prüfen und Setup erneut starten."
Var Restart
Var WaitCount
Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_OK "This package requires 64-bit Windows."
    Abort
  ${EndIf}
  !insertmacro MUI_LANGDLL_DISPLAY
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/RESTART" $1
  ${IfNot} ${Errors}
    StrCpy $Restart "yes"
  ${EndIf}
  StrCpy $WaitCount 0
  wait_app:
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\ReaperArcade-v1") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    ${If} $Restart == "yes"
      IntOp $WaitCount $WaitCount + 1
      ${If} $WaitCount < 120
        Sleep 500
        Goto wait_app
      ${EndIf}
    ${EndIf}
    IfSilent +2
      MessageBox MB_OK "$(CloseApp)"
    SetErrorLevel 2
    Abort
  ${EndIf}
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeadeyeDesktopGuns"
  ${If} $0 != ""
    ExecWait '"$INSTDIR\ReaperArcade.exe" --stop-desktop-guns'
  ${EndIf}
FunctionEnd
Function .onInstSuccess
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeadeyeDesktopGuns"
  ${If} $0 != ""
    Exec '"$INSTDIR\ReaperArcade.exe" --desktop-guns'
  ${EndIf}
  ${If} $Restart == "yes"
    Exec '"$INSTDIR\ReaperArcade.exe"'
  ${EndIf}
FunctionEnd
Section "Deadeye Arcade"
  ; Bootstrapper is bundled; only a missing runtime needs an Internet connection.
  SetRegView 32
  ReadRegStr $0 HKLM "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${If} $0 == ""
  ${OrIf} $0 == "0.0.0.0"
    ReadRegStr $0 HKCU "Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${EndIf}
  ${If} $0 == ""
  ${OrIf} $0 == "0.0.0.0"
    InitPluginsDir
    SetOutPath "$PLUGINSDIR"
    File /oname=MicrosoftEdgeWebview2Setup.exe "${BOOTSTRAPPER}"
    ExecWait '"$PLUGINSDIR\MicrosoftEdgeWebview2Setup.exe" /silent /install' $0
    ${If} $0 != 0
      IfSilent +2
        MessageBox MB_OK "$(RuntimeFailed)"
      SetErrorLevel 3
      Abort
    ${EndIf}
  ${EndIf}
  SetRegView 64
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "${PAYLOAD}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\ReaperArcade\Installer" "Directory" "$INSTDIR"
  WriteRegStr HKCU "Software\ReaperArcade\Installer" "Version" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "DisplayName" "Deadeye Arcade"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "Publisher" "Stefan Hamann"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "DisplayIcon" '$\"$INSTDIR\deadeye.ico$\",0'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade" "NoRepair" 1
  ; Retain data, mutex, registry and executable IDs for in-place upgrades.
  Delete "$DESKTOP\Reaper Arcade.lnk"
  Delete "$SMPROGRAMS\Reaper Arcade\Reaper Arcade.lnk"
  RMDir "$SMPROGRAMS\Reaper Arcade"
  CreateDirectory "$SMPROGRAMS\Deadeye Arcade"
  CreateShortcut "$SMPROGRAMS\Deadeye Arcade\Deadeye Arcade.lnk" "$INSTDIR\ReaperArcade.exe" "" "$INSTDIR\deadeye.ico" 0
  CreateShortcut "$DESKTOP\Deadeye Arcade.lnk" "$INSTDIR\ReaperArcade.exe" "" "$INSTDIR\deadeye.ico" 0
  ; Never enable autostart. Update an existing app-owned entry only.
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "ReaperArcade"
  ${If} $0 != ""
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "ReaperArcade" '$\"$INSTDIR\ReaperArcade.exe$\"'
  ${EndIf}
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeadeyeDesktopGuns"
  ${If} $0 != ""
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeadeyeDesktopGuns" '$\"$INSTDIR\ReaperArcade.exe$\" --desktop-guns'
  ${EndIf}
SectionEnd
Function un.onInit
  SetShellVarContext current
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\ReaperArcade-v1") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_OK "$(CloseApp)"
    Abort
  ${EndIf}
  ExecWait '"$INSTDIR\ReaperArcade.exe" --stop-desktop-guns'
FunctionEnd
Section "Uninstall"
  ; Remove only files shipped by this installer. Keep the user's library and media.
  !include "${UNINSTALL_FILES}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$DESKTOP\Deadeye Arcade.lnk"
  Delete "$SMPROGRAMS\Deadeye Arcade\Deadeye Arcade.lnk"
  RMDir "$SMPROGRAMS\Deadeye Arcade"
  SetRegView 64
  DeleteRegKey HKCU "Software\ReaperArcade\Installer"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ReaperArcade"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "ReaperArcade"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeadeyeDesktopGuns"
SectionEnd
