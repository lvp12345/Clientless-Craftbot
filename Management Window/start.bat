@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PYTHON_SCRIPT=!SCRIPT_DIR!src\craftbot_management_window.py

python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python is not installed or not in PATH
    pause
    exit /b 1
)

if not exist "!PYTHON_SCRIPT!" (
    echo ERROR: craftbot_management_window.py not found
    pause
    exit /b 1
)

REM Create VBScript to launch Python hidden
set VBSCRIPT=!TEMP!\run_management.vbs
(
    echo CreateObject("WScript.Shell"^).Run "python """ "!PYTHON_SCRIPT!" """ ", 0, False
) > "!VBSCRIPT!"

REM Run VBScript and exit immediately
cscript.exe //nologo "!VBSCRIPT!"
del "!VBSCRIPT!" >nul 2>&1
exit /b 0

