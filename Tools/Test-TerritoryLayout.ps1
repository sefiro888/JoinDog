$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw (Join-Path $PSScriptRoot '../Assets/_JoinDog/WorldMap/WorldMapScreenController.cs')
$profiles = [regex]::Matches($source, 'case "([a-z_]+)": p = new\[\]\{([^}]+)\}')
if ($profiles.Count -ne 10) { throw 'Expected ten territory layout profiles' }
Add-Type -AssemblyName System.Drawing
$checks = 0
foreach ($profile in $profiles) {
    $name = $profile.Groups[1].Value
    $values = @($profile.Groups[2].Value.Split(',') | ForEach-Object { [double]::Parse($_.Trim().TrimEnd('f'), [Globalization.CultureInfo]::InvariantCulture) })
    for ($i=1; $i -lt $values.Count; $i++) {
        if ($values[$i] -le $values[$i-1]) { throw "Unordered bands: $name" }
    }
    $asset = Join-Path $PSScriptRoot "../Assets/_JoinDog/Resources/Magic/LevelCards/$name.png"
    $bitmap = [Drawing.Image]::FromFile((Resolve-Path $asset))
    $ratio = $bitmap.Width / $bitmap.Height
    $bitmap.Dispose()
    foreach ($viewport in @(@(320,568),@(360,800),@(390,844),@(1280,720))) {
        $width = [Math]::Min($viewport[0]*.95, $viewport[1]*.95*$ratio)
        $height = $width/$ratio
        if ($width -gt $viewport[0] -or $height -gt $viewport[1]) { throw "Clipped artwork: $name" }
        # All ten levels of a territory use this profile; validate information bands.
        for ($level=1; $level -le 10; $level++) {
            if ($values[8]+.018 -ge $values[9]-.018 -or $values[9]+.018 -ge $values[10]-.018) { throw "Overlapping rows: $name" }
            $checks++
        }
    }
}
Write-Output "PASS: $checks level/viewport geometry cases, ten native image ratios. This does not certify text rendering or touch usability."
