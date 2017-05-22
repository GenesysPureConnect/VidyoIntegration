[CmdletBinding(SupportsShouldProcess)]
Param(
[Parameter()]
[string] $IceLibPath
)
begin
{
  if (! (Test-Path (Join-Path $IceLibPath 'bin')))
  {
    Write-Error "Not a valid IceLib folder (must contain a bin folder)"
    exit 2
  }
  $IceLibPathName = Split-Path $IceLibPath -Leaf
  $IceLibTrace    = (Get-ChildItem "${IceLibPath}\bin" | Where Name -like 'i3trace_dotnet_tracing-w32r-*.dll' | Select -ExpandProperty Name) -replace '.dll',''
  $IceLibVersion  = $IceLibTrace -replace '.*w32r\-', ''

  Write-Output "Updating to IceLib $IceLibPathName"
  Write-Output "  Trace: $IceLibTrace"
}
process
{
  # Find all Visual Studio Project that reference IceLib
  $Projects = Get-ChildItem . -Recurse -Filter '*.csproj' | Select-String 'IceLib' | Group Path | Select -ExpandProperty Name

  Write-Output "Processing $($Projects.Length) projects"
  Foreach ($project in $Projects)
  {
    $ProjectName = Split-Path $Project -Leaf
    Write-Output "Processing project: $ProjectName"
    if ($PSCmdlet.ShouldProcess($Project, "Patching VisualStudio Project"))
    {
      (Get-Content $Project) | Foreach {
        $_ -replace 'IC4_\d{4}_R\d+_P\d+ \(x86\)',            $IceLibPathName `
           -replace 'i3trace_dotnet_tracing\-w32r\-\d+\-\d+', $IceLibTrace 
      } | Set-Content $Project
    }
  }

  # Find all Wix scripts that reference IceLib Trace
  $Wixes = Get-ChildItem . -Recurse -Filter '*.wxs' | Select-String 'IceLib' | Group Path | Select -ExpandProperty Name

  Write-Output "Processing $($Wixes.Length) wix scripts"
  Foreach ($wix in $wixes)
  {
    $WixName = Split-Path $Wix -Leaf
    if ($PSCmdlet.ShouldProcess($Wix, "Patching Wix Script"))
    {
      (Get-Content $Wix) | Foreach {
        $_ -replace 'w32r\-\d+\-\d+', "w32r-$IceLibVersion" `
           -replace 'a00r\-\d+\-\d+', "a00r-$IceLibVersion"
      } | Set-Content $Wix
    }
  }
}

