
; This script is used to create Installer for SaveGame Backup Tool application

/*
   SaveGame Backup Tool -  Application for automatic Games Saves backup
   Copyright (C) 2017 sebeksd

   This program is free software; you can redistribute it and/or modify
   it under the terms of the GNU Lesser General Public License as published by
   the Free Software Foundation; either version 3 of the License, or
   (at your option) any later version.
   
   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU Lesser General Public License for more details.

   You should have received a copy of the GNU Lesser General Public License
   along with this program; if not, write to the Free Software Foundation,
   Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301  USA
*/
;--------------------------------
!define PROGRAM_NAME "SaveGame Backup Tool"
!define DESCRIPTION "Application for automatic backup of Games Saves"
!define COPYRIGHT "sebeksd (c) 2020"
!define INSTALLER_VERSION "1.0.0.2"

!define MAIN_APP_EXE "SaveGame Backup Tool.exe"
!define DESTINATION_PATH "Release"
!define SOURCE_FILES_PATH "Release\Files"

!define REG_ROOT "HKLM"
!define HKLM_REG_UNINSTALL_PATH "Software\Microsoft\Windows\CurrentVersion\Uninstall"

;--------------------------------
; The name of the installer
Name "${PROGRAM_NAME} Installer"

; Installer file info
!getdllversion "${SOURCE_FILES_PATH}\${MAIN_APP_EXE}" Expv_
VIProductVersion "${Expv_1}.${Expv_2}.${Expv_3}.${Expv_4}"
VIAddVersionKey "ProductName"  "${PROGRAM_NAME}"
VIAddVersionKey "LegalCopyright"  "${COPYRIGHT}"
VIAddVersionKey "FileDescription"  "${DESCRIPTION}"
VIAddVersionKey "FileVersion"  "${INSTALLER_VERSION}"

SetCompressor /SOLID LZMA
XPStyle on
Unicode True

; The file to write
OutFile "${DESTINATION_PATH}\Install_SaveGameBackupTool.exe"

; The default installation directory
InstallDir "$PROGRAMFILES\${PROGRAM_NAME}"

; Request application privileges for Windows Vista
RequestExecutionLevel admin
;--------------------------------

; Pages
Page license
LicenseData "${SOURCE_FILES_PATH}\LICENSE"
	
Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles
;--------------------------------

Section "Install"
  ; Set output path to the installation directory.
  SetOutPath $INSTDIR
  
  # define uninstaller name
  WriteUninstaller $INSTDIR\uninstall.exe
  
  ; Put file there
  File "${SOURCE_FILES_PATH}\${MAIN_APP_EXE}"
  File "${SOURCE_FILES_PATH}\*.dll"
  File "${SOURCE_FILES_PATH}\*.md"
  File "${SOURCE_FILES_PATH}\LICENSE"
  
  ; Create start menu items
  CreateDirectory "$SMPROGRAMS\${PROGRAM_NAME}"
  CreateShortCut "$SMPROGRAMS\${PROGRAM_NAME}\${PROGRAM_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
  
  ; Write the uninstall keys for Windows
  WriteRegStr ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "DisplayName" "${PROGRAM_NAME}"
  WriteRegStr ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "InstallLocation" '"$INSTDIR\"'
  WriteRegDWORD ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "NoModify" 1
  WriteRegDWORD ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "NoRepair" 1
SectionEnd ; end the section

Section "Uninstall"
  ; Remove registry keys
  DeleteRegKey ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}"
 
  # Always delete uninstaller first
  Delete $INSTDIR\uninstall.exe

  # delete installed file
  Delete $INSTDIR\*.exe
  Delete $INSTDIR\*.dll
  Delete $INSTDIR\*.md
  RMDir "$INSTDIR"
  
  # delete start menu items
  Delete "$SMPROGRAMS\${PROGRAM_NAME}\*.*"
  RMDir "$SMPROGRAMS\${PROGRAM_NAME}"
SectionEnd

# helper function to TrimQuotes
Function TrimQuotes
Exch $R0
Push $R1
 
  StrCpy $R1 $R0 1
  StrCmp $R1 `"` 0 +2
    StrCpy $R0 $R0 `` 1
  StrCpy $R1 $R0 1 -1
  StrCmp $R1 `"` 0 +2
    StrCpy $R0 $R0 -1
 
Pop $R1
Exch $R0
FunctionEnd
 
!macro _TrimQuotes Input Output
  Push `${Input}`
  Call TrimQuotes
  Pop ${Output}
!macroend
!define TrimQuotes `!insertmacro _TrimQuotes`

# check for path from previous installation (for update)
Function .onInit
 
  ReadRegStr $R0 ${REG_ROOT} "${HKLM_REG_UNINSTALL_PATH}\${PROGRAM_NAME}" "InstallLocation"
  
  StrCmp $R0 "" noUpdate
  
  ${TrimQuotes} $R0 $R0
  StrCpy $InstDir $R0
  
  noUpdate:
FunctionEnd
