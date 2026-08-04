function Get-AudioFileNames($path) {
    if (Test-Path $path) {
        @(Get-ChildItem -Path $path -File | Select-Object -ExpandProperty Name)
    } else {
        @()
    }
}

$manifest = [ordered]@{
    erfolg      = Get-AudioFileNames "./wwwroot/audio/affirmations/erfolg"
    misserfolg  = Get-AudioFileNames "./wwwroot/audio/affirmations/misserfolg"
}

$manifest | ConvertTo-Json | Set-Content -Encoding UTF8 "./wwwroot/audio/affirmations/affirmations.json"
