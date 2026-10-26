# Logitech G HUB CLI - Script de Compilação Multiplataforma
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Compilando Logitech G HUB CLI para Windows, Linux e macOS" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$targets = @(
    @{ Rid = "win-x64";   Output = "build/cli/win-x64";   Name = "ghub-cli.exe" },
    @{ Rid = "linux-x64"; Output = "build/cli/linux-x64"; Name = "ghub-cli" },
    @{ Rid = "osx-x64";   Output = "build/cli/osx-x64";   Name = "ghub-cli" },
    @{ Rid = "osx-arm64"; Output = "build/cli/osx-arm64"; Name = "ghub-cli" }
)

foreach ($t in $targets) {
    Write-Host "`n--> Compilando para $($t.Rid)..." -ForegroundColor Yellow
    dotnet publish cli/GHubCli.csproj -c Release -r $t.Rid --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o $t.Output
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    [OK] Sucesso: $($t.Output)/$($t.Name)" -ForegroundColor Green
    } else {
        Write-Host "    [FALHA] Erro ao compilar para $($t.Rid)" -ForegroundColor Red
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Compilação concluída!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
