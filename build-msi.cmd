@echo off
setlocal
call "%~dp0build.cmd"
if errorlevel 1 exit /b 1
if "%WIX%"=="" set "WIX=%~dp0..\wix314\bin"
if not exist "%WIX%\candle.exe" (
  echo WiX Toolset bulunamadi: %WIX%
  exit /b 1
)
"%WIX%\candle.exe" -nologo -arch x86 -ext WixUtilExtension -ext WixUIExtension -out "%~dp0dist\Installer.wixobj" "%~dp0Installer.wxs"
if errorlevel 1 exit /b 1
"%WIX%\light.exe" -nologo -sval -ext WixUtilExtension -ext WixUIExtension -cultures:tr-tr -out "%~dp0dist\ASCOS-LegacyRun-3.5.0.msi" "%~dp0dist\Installer.wixobj"
if errorlevel 1 exit /b 1
echo MSI hazir: %~dp0dist\ASCOS-LegacyRun-3.5.0.msi
