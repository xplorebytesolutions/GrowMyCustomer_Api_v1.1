@echo off
REM This script will find all files and output their name and content into one file.
REM The output file will be named [FolderName]_AllFileDump.txt.

REM Get the current folder's name and set it as the output file name with the custom suffix
for %%I in ("%cd%") do set "outputFile=%%~nI_AllFileDump.txt"

REM Clear the output file to start fresh
> "%outputFile%" (echo Folder and File Content Report)
echo. >> "%outputFile%"

REM Loop through all files in the current directory and subdirectories
for /R . %%F in (*.*) do (
    echo ====================================================== >> "%outputFile%"
    echo FILE: %%F >> "%outputFile%"
    echo ====================================================== >> "%outputFile%"
    echo. >> "%outputFile%"
    type "%%F" >> "%outputFile%" 2>nul
    echo. >> "%outputFile%"
    echo. >> "%outputFile%"
)

echo Finished! All content has been extracted to %outputFile%