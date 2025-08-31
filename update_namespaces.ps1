$oldNamespace = "ArkPlotWpf"
$newNamespace = "ArkPlot.Core"
$path = "C:\TechProjects\About_MyRepos\ArkPlot\ArkPlot.Core"

Get-ChildItem -Path $path -Filter "*.cs" -Recurse | ForEach-Object {
    (Get-Content $_.FullName) | ForEach-Object {
        $_ -replace $oldNamespace, $newNamespace
    } | Set-Content $_.FullName
}