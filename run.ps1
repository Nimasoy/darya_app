Write-Host "==============================================" -ForegroundColor Magenta
Write-Host "        STARTING DARYA APPLICATION MVP        " -ForegroundColor Magenta
Write-Host "==============================================" -ForegroundColor Magenta

# Set up local workspace path variables
$workspaceRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($workspaceRoot)) {
    $workspaceRoot = (Get-Item .).FullName
}

# Start Backend API Server
Write-Host "🚀 Launching Backend API on http://localhost:5000..." -ForegroundColor Cyan
Start-Process dotnet -ArgumentList "run" -WorkingDirectory "$workspaceRoot/server"

# Start Client Dev Server
Write-Host "🎨 Launching Frontend client dev server on http://localhost:5173..." -ForegroundColor Green
Start-Process cmd -ArgumentList "/c npm run dev" -WorkingDirectory "$workspaceRoot/client"

Write-Host "----------------------------------------------" -ForegroundColor Gray
Write-Host "Darya is launching! Live logs will display in the separate popup console windows." -ForegroundColor Yellow
Write-Host "Navigate to http://localhost:5173 to test the app." -ForegroundColor Yellow
Write-Host "==============================================" -ForegroundColor Magenta
