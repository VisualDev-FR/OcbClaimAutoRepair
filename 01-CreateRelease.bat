@echo off

SET NAME="OcbClaimAutoRepair"

call "00-CompileModule.bat"

if NOT %ERRORLEVEL% == 0 (
  exit /b 1
)

if not exist build\ (
  mkdir build
)

if exist build\%NAME%\ (
  echo
  echo remove existing directory
  rmdir build\%NAME% /S /Q
)

mkdir build\%NAME%

SET VERSION=snapshot

if not "%1"=="" (
  SET VERSION=%1
)

echo:
echo create %VERSION%

xcopy *.dll build\%NAME%\
xcopy README.md build\%NAME%\
xcopy ModInfo.xml build\%NAME%\
xcopy Config build\%NAME%\Config\ /S
xcopy Resources build\%NAME%\Resources\ /S
xcopy UIAtlases build\%NAME%\UIAtlases\ /S

cd build

echo:
echo Packaging %NAME%-%VERSION%.zip
powershell Compress-Archive %NAME% %NAME%-%VERSION%.zip -Force
cd ..

SET RV=%ERRORLEVEL%
SET MOD_PATH="%PATH_7D2D%"\Mods
SET DEST=%MOD_PATH%\%NAME%

if exist %DEST% (
  rd %DEST% /S /Q
)


echo:
echo moving archive to mod folder to %DEST%
move build\%NAME% %DEST%