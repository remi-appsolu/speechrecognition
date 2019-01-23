
@echo off
rem copy *.nupkg %HOMEPATH%\dropbox\nuget\ /y

nuget push -Source "AppsoluTaxi" -ApiKey VSTS .\Plugin.SpeechRecognition\bin\Release\*.symbols.nupkg nuget.exe
nuget push -Source "AppsoluTaxi" -ApiKey VSTS .\Plugin.SpeechDialogs\bin\Release\*.symbols.nupkg nuget.exe
rem del .\Plugin.SpeechRecognition\bin\Release\*.nupkg

rem nuget push .\MvvmCross.Plugin.BluetoothLE\bin\Release\*.nupkg -Source https://www.nuget.org/api/v2/package
rem del .\MvvmCross.Plugin.BluetoothLE\bin\Release\*.nupkg
pause