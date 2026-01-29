@echo off
REM This script will find relevant source files and output their names and contents into one file.
REM The output file will be named [FolderName]_AllFileDump.txt.

REM Get the current folder's name and set it as the output file name with the custom suffix
for %%I in ("%cd%") do set "outputFile=%%~nI_AllFileDump.txt"

REM Clear the output file to start fresh and write a small header
> "%outputFile%" (
    echo Folder and File Content Report
    echo Root folder: %cd%
    echo Generated at: %date% %time%
)
echo. >> "%outputFile%"

REM NOTE:
REM We now only dump RELEVANT text/code files (no binaries, no images, no node_modules, etc.)
REM This keeps the file smaller and much easier to review.

REM Loop through all relevant files in the current directory and subdirectories
REM Extensions included: C#, JS/TS/React, JSON, config, SQL, Markdown, YAML
for /R . %%F in (*.cs *.csproj *.jsx *.tsx *.js *.ts *.json *.config *.sql *.md *.yml *.yaml *.bat) do (

    REM Skip some noisy folders by path substring (node_modules, bin, obj, .git, dist, .vs)
    echo "%%F" | findstr /I /C:"\node_modules\" /C:"\bin\" /C:"\obj\" /C:"\.git\" /C:"\dist\" /C:"\.vs\" >nul
    if errorlevel 1 (
        echo ====================================================== >> "%outputFile%"
        echo FILE: %%F >> "%outputFile%"
        echo ====================================================== >> "%outputFile%"
        echo. >> "%outputFile%"
        type "%%F" >> "%outputFile%" 2>nul
        echo. >> "%outputFile%"
        echo. >> "%outputFile%"
    )
)

echo Finished! All content has been extracted to %outputFile%
