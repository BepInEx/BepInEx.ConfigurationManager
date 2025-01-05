if ($PSScriptRoot -match '.+?\\bin\\?') {
    $dir = $PSScriptRoot + "\"
}
else {
    $dir = $PSScriptRoot + "\bin\"
}


foreach($target in ("BepInEx5","IL2CPP"))
{
    $copy = $dir + "copy\BepInEx\plugins\ConfigurationManager\"

    Remove-Item -Force -Path ($copy) -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path ($copy)
    
    Copy-Item -Path ($dir + $target + "\*") -Destination ($copy) -Recurse -Force
    Remove-Item -Force -Path ($copy + "\*.deps.json") -Recurse -ErrorAction SilentlyContinue

    Copy-Item -Path ($dir + "\..\README.md") -Destination ($copy) -Recurse -Force
    Copy-Item -Path ($dir + "\..\LICENSE") -Destination ($copy) -Recurse -Force
    Copy-Item -Path ($dir + "\..\ConfigurationManagerAttributes.cs") -Destination ($copy) -Recurse -Force

    $ver = (Get-ChildItem -Path ($dir + $target) -Filter ("*ConfigurationManager.dll") -Recurse -Force)[0].VersionInfo.FileVersion.ToString() -replace "([\d+\.]+?\d+)[\.0]*$", '${1}'
    Compress-Archive -Path ($dir + "copy\BepInEx") -Force -CompressionLevel "Optimal" -DestinationPath ($dir + "BepInEx.ConfigurationManager." + $target +"_v" + $ver + ".zip")
}

Remove-Item -Force -Path ($dir + "copy") -Recurse -ErrorAction SilentlyContinue
