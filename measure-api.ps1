param(
    [string]$BaseUrl = "http://localhost:8080",
    [ValidateRange(5, 500)]
    [int]$Samples = 30,
    [ValidateRange(0, 50)]
    [int]$Warmup = 5,
    [string]$OutputPath = (Join-Path $env:TEMP "productionflow-performance.csv")
)

$ErrorActionPreference = "Stop"

# Plik .env odczytujemy lokalnie. Nie wyświetlamy hasła w konsoli.
$envPath = Join-Path $PSScriptRoot "..\.env"

if (-not (Test-Path -LiteralPath $envPath)) {
    throw "Nie znaleziono pliku .env w głównym folderze ProductionFlow."
}

$passwordLine = Get-Content -LiteralPath $envPath |
    Where-Object { $_ -match '^\s*DEMO_ADMIN_PASSWORD\s*=' } |
    Select-Object -First 1

if (-not $passwordLine) {
    throw "Brak DEMO_ADMIN_PASSWORD w pliku .env."
}

$password = ($passwordLine -split '=', 2)[1].Trim()

# Obsługujemy także hasło zapisane w .env w cudzysłowie.
if ($password.Length -ge 2 -and $password.StartsWith('"') -and $password.EndsWith('"')) {
    $password = $password.Substring(1, $password.Length - 2)
}

if ([string]::IsNullOrWhiteSpace($password)) {
    throw "DEMO_ADMIN_PASSWORD jest pusty."
}

# Sesja przechowuje cookies Identity i antiforgery.
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$base = $BaseUrl.TrimEnd('/')

$csrf = Invoke-RestMethod -Uri "$base/api/auth/csrf-token" `
    -WebSession $session

$loginBody = @{
    email = "admin@productionflow.local"
    password = $password
} | ConvertTo-Json

$null = Invoke-RestMethod -Uri "$base/api/auth/login" `
    -Method Post `
    -WebSession $session `
    -ContentType "application/json" `
    -Headers @{ "X-CSRF-TOKEN" = $csrf.requestToken } `
    -Body $loginBody

# Ustalamy ID zlecenia, aby porównywać to samo żądanie.
$firstPage = Invoke-RestMethod -Uri `
    "$base/api/orders?page=1&pageSize=1" -WebSession $session

$endpoints = @(
    [pscustomobject]@{
        Name = "Orders list"
        Path = "/api/orders?page=1&pageSize=10"
    },
    [pscustomobject]@{
        Name = "Products list"
        Path = "/api/products"
    },
    [pscustomobject]@{
        Name = "Work centers list"
        Path = "/api/workcenters"
    }
)

if ($firstPage.items.Count -gt 0) {
    $orderId = $firstPage.items[0].id
    Write-Host "Zlecenie użyte do pomiaru historii: $orderId"

    $endpoints += [pscustomobject]@{
        Name = "Order history"
        Path = "/api/orders/$orderId/history"
    }
}
else {
    Write-Warning "Brak zleceń. Historia nie zostanie zmierzona."
}

$results = foreach ($endpoint in $endpoints) {
    $url = $base + $endpoint.Path

    for ($i = 0; $i -lt $Warmup; $i++) {
        $null = Invoke-WebRequest -Uri $url -WebSession $session `
            -UseBasicParsing
    }

    $times = @(
        for ($i = 0; $i -lt $Samples; $i++) {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-WebRequest -Uri $url `
                -WebSession $session -UseBasicParsing
            $stopwatch.Stop()

            if ($response.StatusCode -ne 200) {
                throw "Błąd HTTP $($response.StatusCode) dla $url."
            }

            $stopwatch.Elapsed.TotalMilliseconds
        }
    )

    $sorted = @($times | Sort-Object)
    $middle = [int][Math]::Floor($Samples / 2)

    if ($Samples % 2 -eq 0) {
        $median = ($sorted[$middle - 1] + $sorted[$middle]) / 2
    }
    else {
        $median = $sorted[$middle]
    }

    $p95Index = [int][Math]::Ceiling($Samples * 0.95) - 1
    $average = ($times | Measure-Object -Average).Average

    [pscustomobject]@{
        Endpoint = $endpoint.Name
        Samples = $Samples
        AverageMs = [Math]::Round($average, 2)
        MedianMs = [Math]::Round($median, 2)
        P95Ms = [Math]::Round($sorted[$p95Index], 2)
    }
}

$results | Format-Table -AutoSize
$results | Export-Csv -LiteralPath $OutputPath -NoTypeInformation `
    -Delimiter ';' -Encoding UTF8

Write-Host "Wyniki zapisano w: $OutputPath"