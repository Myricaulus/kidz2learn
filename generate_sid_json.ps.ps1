Get-ChildItem -Path "./wwwroot/sids" -Filter "*.sid" | Select-Object -ExpandProperty Name | ConvertTo-Json | Set-Content -Encoding UTF8 "./wwwroot/sids/sidfiles.json" 
