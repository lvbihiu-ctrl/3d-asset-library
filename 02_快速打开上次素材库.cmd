@echo off
setlocal
set "INDEX=%~dp0.local-data\index.html"
if exist "%INDEX%" (
 start "" "%INDEX%"
) else (
 echo First run 01 to scan a folder.
 pause
)
