$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$ui = Join-Path $root 'src/QS3D.AutoCAD/UI'
$managerPath = Join-Path $ui 'Qs3dThemeManager.cs'
$workspacePath = Join-Path $ui 'Qs3dWorkspaceControl.cs'
$browserPath = Join-Path $ui 'Qs3dBrowserControl.cs'
$mepPath = Join-Path $ui 'MepReviewControl.cs'

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw "UI theme contract failed: $message" }
}

Require (Test-Path -LiteralPath $managerPath) 'shared Qs3dThemeManager.cs is missing.'

$manager = Get-Content -LiteralPath $managerPath -Raw
$workspace = Get-Content -LiteralPath $workspacePath -Raw
$browser = Get-Content -LiteralPath $browserPath -Raw
$mep = Get-Content -LiteralPath $mepPath -Raw

Require ($manager -match 'enum\s+Qs3dThemeMode') 'Qs3dThemeMode enum is missing.'
foreach ($mode in @('System', 'Light', 'Dark')) {
    Require ($manager -match "\b$mode\b") "theme mode '$mode' is missing."
}
Require ($manager -match 'ResolveHostTheme\s*\(\s*int\s+colorTheme\s*\)') 'host-theme mapping must be isolated in ResolveHostTheme(int).'
Require ($manager -match 'colorTheme\s*==\s*0\s*\?\s*Qs3dResolvedTheme\.Dark\s*:\s*Qs3dResolvedTheme\.Light') 'AutoCAD COLORTHEME mapping must be 0=Dark and nonzero=Light.'
Require ($manager -match 'ApplicationData') 'theme preference must live under roaming ApplicationData.'
Require ($manager -match 'ui-theme\.txt') 'bounded theme preference file is missing.'
Require ($manager -match 'File\.Replace|File\.Move') 'theme preference must use atomic replace/move semantics.'
Require ($manager -match 'SystemVariableChanged') 'System mode must observe AutoCAD SystemVariableChanged.'
Require ($manager -match 'COLORTHEME') 'System mode must react specifically to COLORTHEME.'
Require ($manager -match 'ThemeChanged') 'shared theme manager must publish a ThemeChanged event.'

Require ($workspace -match 'Qs3dThemeManager') 'workspace must consume shared theme manager.'
Require ($workspace -notmatch 'private\s+sealed\s+class\s+WorkspaceTheme') 'legacy private WorkspaceTheme must be removed.'
Require ($workspace -match 'System|Light|Dark') 'workspace must expose a three-mode theme selector.'
Require ($workspace -match 'ApplyThemeSelectorPalette\s*\(') 'theme selector must explicitly re-apply popup palette colors after theme changes.'
foreach ($resourceKey in @('WindowBrushKey', 'ControlBrushKey', 'WindowTextBrushKey', 'ControlTextBrushKey', 'HighlightBrushKey', 'HighlightTextBrushKey')) {
    Require ($workspace -match "SystemColors\.$resourceKey") "theme selector popup must override SystemColors.$resourceKey instead of inheriting default WPF light colors."
}
Require ($workspace -match 'item\.Background\s*=\s*_theme\.Card') 'theme selector items must use the active card background.'
Require ($workspace -match 'item\.Foreground\s*=\s*_theme\.Foreground') 'theme selector items must use the active foreground color.'
Require ($workspace -match 'ApplyThemeSelectorPalette\s*\(\s*\)\s*;[\s\S]*SelectThemeMode\s*\(\s*mode\s*\)') 'theme application must recolor the dropdown before restoring the selected mode.'
Require ($browser -match 'Qs3dThemeManager|Qs3dThemePalette') 'WinForms browser must consume the shared theme.'
Require ($mep -match 'Qs3dThemeManager|Qs3dThemePalette') 'MEP review palette must consume the shared theme.'

Write-Host 'QS3D UI theme contract PASS: System/Light/Dark, AutoCAD mapping, persistence, eventing, themed ComboBox popup resources and shared WPF/WinForms palette coverage are present.'
