param([string]$Project = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TwinInspector'))
$ErrorActionPreference = 'Stop'
$cecilPackage = Get-ChildItem -LiteralPath (Join-Path $Project 'Library/PackageCache') -Directory -Filter 'com.unity.nuget.mono-cecil@*' | Select-Object -First 1
if (-not $cecilPackage) { throw 'The Unity Mono.Cecil package is not available.' }
[Reflection.Assembly]::LoadFrom((Join-Path $cecilPackage.FullName 'Mono.Cecil.dll')) | Out-Null
$source = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Project 'Library/Bee/PlayerScriptAssemblies/TwinInspector.Runtime.dll'))
$stripped = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Project 'Library/Bee/artifacts/Android/ManagedStripped/TwinInspector.Runtime.dll'))
try {
    $names = @('AlignmentState','MarkerCatalog','MarkerDefinition','Annotation','AnnotationDocument','AnnotationRegion','DismissedDocument','Dismissal','TwinManifest','SamModels')
    $types = @($source.MainModule.Types | Where-Object {$_.Namespace -eq 'TwinInspector' -and $_.Name -in $names})
    $types += @($types | ForEach-Object {$_.NestedTypes})
    $retained = @($stripped.MainModule.Types) + @($stripped.MainModule.Types | ForEach-Object {$_.NestedTypes})
    foreach ($type in $types) {
        $actual = $retained | Where-Object {$_.FullName -eq $type.FullName} | Select-Object -First 1
        if (-not $actual) { throw "Stripped JSON type: $($type.FullName)" }
        foreach ($field in $type.Fields | Where-Object IsPublic) {
            if ($field.Name -notin $actual.Fields.Name) { throw "Stripped JSON field: $($type.FullName).$($field.Name)" }
        }
        if (-not @($actual.Methods | Where-Object {$_.IsConstructor -and -not $_.IsStatic -and $_.Parameters.Count -eq 0}).Count) {
            throw "Missing JSON constructor: $($type.FullName)"
        }
        Write-Output "PASS: $($type.FullName) public fields and default constructor retained"
    }
    Write-Output "PASS: $($types.Count) JSON data types survive Android managed stripping."
} finally {
    $source.Dispose()
    $stripped.Dispose()
}
