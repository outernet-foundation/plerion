$solutionPath = "Outernet.sln"
function Get-UnityRoot([IO.DirectoryInfo]$dir) {
    while ($dir -ne $null) {
        if (Test-Path (Join-Path $dir.FullName "ProjectSettings/ProjectVersion.txt")) {
            return $dir.FullName
        }
        $dir = $dir.Parent
    }
    return $null
}
# Create solution if missing
if (-not (Test-Path $solutionPath)) {
    dotnet new sln --name "Outernet" | Out-Null
}
# Get existing projects to avoid dup-adds
$existing = @{}
$listed = dotnet sln $solutionPath list 2>$null
if ($LASTEXITCODE -eq 0) {
    ($listed | Select-String -Pattern '\.csproj').ForEach({
        $p = (Resolve-Path $_.Line.Trim()).Path
        $existing[$p] = $true
    })
}
# Add all csproj files, grouped by Unity project root
Get-ChildItem -Path . -Recurse -Filter *.csproj | ForEach-Object {
    $proj = (Resolve-Path $_.FullName).Path
    if ($existing.ContainsKey($proj)) { return }
    $unityRoot = Get-UnityRoot $_.Directory
    $folder = if ($unityRoot) { Split-Path -Leaf $unityRoot } else { Split-Path -Leaf $_.Directory.FullName }
    dotnet sln $solutionPath add --solution-folder "$folder" "$proj"
}