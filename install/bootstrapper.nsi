!include nsDialogs.nsh
!include LogicLib.nsh
!include MUI2.nsh
!include x64.nsh

InstallDir "$PROGRAMFILES\Yunker\$(ProductName)"
!define SHORT_CUT "$(ProductName).lnk"

; The file to write
OutFile "${Y_BOOTSTRAPPER}"
SetCompressor lzma

!define MUI_COMPONENTSPAGE_SMALLDESC

; Request application privileges for Windows Vista
RequestExecutionLevel admin

XPStyle on

;--------------------------------

Name "$(ProductName)"

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

;--------------------------------

!insertmacro MUI_LANGUAGE ${Y_LANGUAGE_NSIS}
!include Bootstrapper_${Y_PRODUCT}_${Y_LANGUAGE}.nsh
!include Bootstrapper_${Y_LANGUAGE}.nsh

Section "$(ProductName) ${Y_PRODUCT_VERSION}" Product
    SetOutPath $INSTDIR
    File "${Y_FILE_PATH}\GCodeCorrector.exe"
    File "${Y_FILE_PATH}\GCodeCorrector.config"
    File "${Y_FILE_PATH}\*.dll"
    
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${Y_PRODUCT}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${Y_PRODUCT}" "DisplayName" "$(ProductName) ${Y_PRODUCT_VERSION}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${Y_PRODUCT}" "DisplayIcon" "$INSTDIR\GCodeCorrector.exe"
 
    CreateShortCut "$DESKTOP\${SHORT_CUT}" "$INSTDIR\GCodeCorrector.exe"
	CreateDirectory "$SMPROGRAMS\${Y_PRODUCT}"
	CreateShortCut "$SMPROGRAMS\${Y_PRODUCT}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortCut "$SMPROGRAMS\${Y_PRODUCT}\${SHORT_CUT}" "$INSTDIR\GCodeCorrector.exe"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${Product} "$(ProductDescription)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function .onInstSuccess

FunctionEnd

Function .onInit
    System::Call 'kernel32::CreateMutexA(i 0, i 0, t "YunkerMutex") i .r1 ?e'
    Pop $R0

    StrCmp $R0 0 +3
        MessageBox MB_OK|MB_ICONEXCLAMATION "$(AlreadyRunning)"
        Abort
        
    SetRebootFlag false
	
	SectionGetFlags ${Product} $R0
	IntOp $R0 $R0 | ${SF_RO}
	SectionSetFlags ${Product} $R0
FunctionEnd

Section "Uninstall"
    RMDir  "$INSTDIR"
    Delete "$DESKTOP\${SHORT_CUT}"
	Delete "$SMPROGRAMS\${Y_PRODUCT}\*.*"
    RmDir  "$SMPROGRAMS\${Y_PRODUCT}"
	RmDir  "$PROGRAMFILES\Yunker"
    DeleteRegKey /ifempty HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${Y_PRODUCT}"
SectionEnd