@echo off

SET NAME=OcbClaimAutoRepair
SET MOD_DIR="C:\SteamLibrary\steamapps\common\7 Days To Die\Mods"

call "00-CompileModule.bat"

if NOT %ERRORLEVEL% == 0 (
  exit /b 1
)

if not exist build\ (
  mkdir build
)

if exist build\%NAME%\ (
  echo remove existing directory
  rmdir build\%NAME% /S /Q
)

mkdir build\%NAME%

SET VERSION=snapshot

if not "%1"=="" (
  SET VERSION=%1
)

echo create %VERSION%

xcopy *.dll build\%NAME%\
xcopy README.md build\%NAME%\
xcopy ModInfo.xml build\%NAME%\
xcopy Config build\%NAME%\Config\ /S
xcopy Resources build\%NAME%\Resources\ /S
xcopy UIAtlases build\%NAME%\UIAtlases\ /S

cd build
echo Packaging %NAME%-%VERSION%.zip
powershell Compress-Archive %NAME% %NAME%-%VERSION%.zip -Force
cd ..

SET RV=%ERRORLEVEL%

if exist %MOD_DIR%\%NAME% (
  rd %MOD_DIR%\%NAME% /S /Q
)

move build\%NAME% %MOD_DIR%\%NAME%