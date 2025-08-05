@echo off
setlocal

:: Ensure exactly one argument was provided
if "%~1"=="" (
  echo Usage: %~nx0 [start^|stop]
  exit /b 1
)

:: Dispatch based on argument (case-insensitive)
if /I "%~1"=="start" (
  docker compose up --build -d
  tailscale serve --bg --tcp 8000 tcp://localhost:8000
  tailscale serve --bg --tcp 8978 tcp://localhost:8978
  tailscale serve --bg --tcp 9000 tcp://localhost:9000
  tailscale serve --bg --tcp 9001 tcp://localhost:9001
  echo Services started.
  exit /b 0
) else if /I "%~1"=="stop" (
  tailscale serve --tcp=8000 off
  tailscale serve --tcp=8978 off
  tailscale serve --tcp=9000 off
  tailscale serve --tcp=9001 off
  docker compose down
  echo Services stopped.
  exit /b 0
) else (
  echo Invalid argument: %~1
  echo Usage: %~nx0 [start^|stop]
  exit /b 1
)
