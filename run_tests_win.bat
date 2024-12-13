@echo off
set time_=%time%
set time_=%time_:,=.%
set time_=%time_::=.%
set filemask_=%date%-%time_%
set testDir_=%~dp0.test
set testResults_=%testDir_%\%filemask_%_results.xml
set logFile_=%testDir_%\%filemask_%_log.txt
echo Will output test results to %testResults_%
if not exist %testDir_% ( 
	mkdir %~dp0.test
)
"%ProgramFiles%\Unity\Hub\Editor\2023.2.17f1\Editor\Unity.exe" -runTests -batchmode -projectPath "." -testPlatform StandaloneWindows64 -buildTarget Win64 -playergraphicsapi=OpenGLES3 -mtRendering -scriptingbackend=il2cpp -testResults "%testResults_%" -logfile "%logFile_%"
