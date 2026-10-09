[CmdletBinding()]
param([uri]$BaseUrl = 'http://localhost:8086/', [string]$OutputPath = '.data/phase21/demo-validation.json')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $passwordLine = [System.IO.File]::ReadAllLines((Join-Path (Get-Location) '.env')) |
        Where-Object { $_ -match '^DEMO_PASSWORD=.+$' }
    if (@($passwordLine).Count -ne 1) { throw 'Configura una sola DEMO_PASSWORD en .env.' }
    $password = $passwordLine.Substring('DEMO_PASSWORD='.Length)
    $actors = @(
        @{Id='900000001';Role='ADMIN';Vehicles=0}, @{Id='900000002';Role='GUARD';Vehicles=0},
        @{Id='900000003';Role='USER';Vehicles=2}, @{Id='900000004';Role='USER';Vehicles=1}, @{Id='900000005';Role='USER';Vehicles=0}
    )
    $headers = @{}
    $results = @()
    foreach ($actor in $actors) {
        $body = @{ identificationNumber=$actor.Id; password=$password } | ConvertTo-Json
        for ($attempt = 0; $attempt -lt 2; $attempt++) {
            try {
                $login = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/auth/login')) -Method Post -ContentType 'application/json' -Body $body
                break
            } catch {
                if ([int]$_.Exception.Response.StatusCode -ne 429 -or $attempt -eq 1) { throw }
                $retry = $_.Exception.Response.Headers.RetryAfter.Delta.TotalSeconds
                $seconds = if ($retry -gt 0) { [int][Math]::Ceiling($retry) } else { 60 }
                if ($seconds -gt 60) { throw 'Retry-After supera un minuto; repite la validación cuando termine la espera indicada por el servidor.' }
                Write-Output "Login limitado; esperando $seconds segundos según Retry-After."
                Start-Sleep -Seconds $seconds
            }
        }
        $auth = @{ Authorization='Bearer ' + $login.accessToken }
        $headers[$actor.Id] = $auth
        $me = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/users/me')) -Headers $auth
        if ($me.identificationNumber -ne $actor.Id -or $me.roles -notcontains $actor.Role) { throw 'Identidad o roles demo inesperados.' }
        $vehicles = @(Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/vehicles/me')) -Headers $auth)
        # PowerShell may wrap a JSON array as one pipeline object; normalize explicitly.
        $vehicles = @($vehicles | ForEach-Object { $_ })
        if ($vehicles.Count -ne $actor.Vehicles) { throw "Cantidad de vehículos inesperada para $($actor.Id)." }
        if ($actor.Id -eq '900000003' -and @($vehicles | Where-Object type -eq 'CAR').Count -ne 0) { throw 'Student no puede tener Car.' }
        $results += [pscustomobject]@{Identification=$actor.Id;Roles=@($me.roles);MemberType=$me.memberType;VehicleCount=$vehicles.Count}
    }
    $lookup = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/parking/access/lookup')) -Headers $headers['900000002'] -Method Post -ContentType 'application/json' -Body '{"qrPayload":"OTAwMDAwMDAz"}'
    if ($lookup.user.id -ne '21de0000-0000-4000-8000-000000000003') { throw 'Lookup demo incorrecto.' }
    $history = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/parking/history/me')) -Headers $headers['900000003']
    $news = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/news')) -Headers $headers['900000003']
    $null = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/users')) -Headers $headers['900000001']
    $detail = Invoke-RestMethod ([uri]::new($BaseUrl,'api/v1/vehicles/21de0000-0000-4000-8000-000000000030')) -Headers $headers['900000003']
    $documentUrl = [uri]::new($BaseUrl,$detail.documents[0].contentUrl)
    $allowed = Invoke-WebRequest $documentUrl -Headers $headers['900000003']
    $denied = Invoke-WebRequest $documentUrl -Headers $headers['900000005'] -SkipHttpErrorCheck
    if ($allowed.StatusCode -ne 200 -or $denied.StatusCode -ne 404) { throw 'Privacidad de soportes demo incorrecta.' }
    $report = [pscustomobject]@{
        CheckedAtUtc=[DateTimeOffset]::UtcNow; BaseUrl=$BaseUrl.AbsoluteUri; Actors=$results
        EligibleStudentVehicles=@($lookup.eligibleVehicles).Count; PersonalHistoryCount=$history.totalCount
        PrivateDocumentOwnerStatus=$allowed.StatusCode; PrivateDocumentUnrelatedUserStatus=$denied.StatusCode
        AdminUsersAccessible=$true; NewsAccessible=($null -ne $news)
    }
    $absolute = [System.IO.Path]::GetFullPath($OutputPath)
    $root = [System.IO.Path]::GetFullPath((Get-Location).Path) + [System.IO.Path]::DirectorySeparatorChar
    if (!$absolute.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath debe permanecer dentro del repositorio.' }
    $null = [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($absolute))
    [System.IO.File]::WriteAllText($absolute, ($report | ConvertTo-Json -Depth 6))
    $results | Format-Table Identification,MemberType,VehicleCount
    Write-Output 'Validación demo aprobada; evidencia sin contraseñas ni tokens guardada.'
} finally { $password = $null; Pop-Location }
