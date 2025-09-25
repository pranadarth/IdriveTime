@echo off
cd /d "%~dp0"
npx webpack
firebase deploy
pause
