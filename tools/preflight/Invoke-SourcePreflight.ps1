[CmdletBinding()]
param(
    [string]$RootPath = (Join-Path $PSScriptRoot "..\..")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RootPath).Path
if (-not (Test-Path (Join-Path $root "TeeKay87.MemoryEngine.sln") -PathType Leaf)) {
    Write-Error "Could not find TeeKay87.MemoryEngine.sln under '$root'."
    exit 2
}

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-PreflightError {
    param([string]$Message)
    $errors.Add($Message)
}

function Add-PreflightWarning {
    param([string]$Message)
    $warnings.Add($Message)
}

function Get-RelativePath {
    param([string]$Path)

    $rootUri = [Uri]::new(($root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar))
    $pathUri = [Uri]::new((Resolve-Path $Path).Path)
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('/', [IO.Path]::DirectorySeparatorChar)
}

function Test-IsUnsafeSetterEvent {
    param([System.Xml.XmlNode]$Node)

    $current = $Node.ParentNode
    while ($null -ne $current) {
        if ($current.LocalName -in @("DataTemplate", "ControlTemplate", "ItemsPanelTemplate")) {
            return $false
        }

        if ($current.LocalName -eq "Setter.Value") {
            return $true
        }

        $current = $current.ParentNode
    }

    return $false
}

function Resolve-WpfType {
    param([string]$LocalName)

    foreach ($qualifiedName in @(
        "System.Windows.Controls.$LocalName, PresentationFramework",
        "System.Windows.Controls.Primitives.$LocalName, PresentationFramework",
        "System.Windows.$LocalName, PresentationFramework",
        "System.Windows.Documents.$LocalName, PresentationFramework",
        "System.Windows.Shapes.$LocalName, PresentationFramework"
    )) {
        $type = [Type]::GetType($qualifiedName, $false)
        if ($null -ne $type) {
            return $type
        }
    }

    return $null
}

$xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml"
$presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
$knownEventNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in @(
    "Click", "Loaded", "Unloaded", "Closed", "Closing",
    "SelectionChanged", "TextChanged", "KeyDown", "KeyUp",
    "PreviewKeyDown", "PreviewKeyUp", "LostKeyboardFocus", "GotKeyboardFocus",
    "MouseDoubleClick", "MouseDown", "MouseUp", "PreviewMouseDown", "PreviewMouseUp",
    "PreviewMouseRightButtonDown", "ContextMenuOpening",
    "LoadingRow", "UnloadingRow", "Checked", "Unchecked",
    "DragEnter", "DragLeave", "DragOver", "Drop"
)) {
    [void]$knownEventNames.Add($name)
}

$wpfReflectionAvailable = $false
try {
    Add-Type -AssemblyName PresentationFramework -ErrorAction Stop
    $wpfReflectionAvailable = $true
}
catch {
    Add-PreflightWarning "PresentationFramework could not be loaded. Built-in WPF event/type reflection checks were skipped."
}

$xamlFiles = @(Get-ChildItem (Join-Path $root "src") -Recurse -File -Filter *.xaml | Sort-Object FullName)
$allResourceKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$xamlDocuments = @{}
$xamlTexts = @{}

foreach ($xamlFile in $xamlFiles) {
    $relative = Get-RelativePath $xamlFile.FullName
    $text = Get-Content $xamlFile.FullName -Raw
    $xamlTexts[$xamlFile.FullName] = $text

    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.PreserveWhitespace = $true
        $document.LoadXml($text)
        $xamlDocuments[$xamlFile.FullName] = $document
    }
    catch {
        Add-PreflightError "${relative}: invalid XML/XAML: $($_.Exception.Message)"
        continue
    }

    foreach ($node in $document.SelectNodes("//*")) {
        if ($null -eq $node.Attributes) {
            continue
        }

        $keyAttribute = $node.Attributes.GetNamedItem("Key", $xamlNamespace)
        if ($null -ne $keyAttribute -and -not [string]::IsNullOrWhiteSpace($keyAttribute.Value)) {
            [void]$allResourceKeys.Add($keyAttribute.Value)
        }
    }
}

$staticResourceReferences = [System.Collections.Generic.List[object]]::new()
$staticResourceRegex = [Regex]::new("\\{StaticResource\\s+([A-Za-z_][A-Za-z0-9_.-]*)\\}")
foreach ($xamlFile in $xamlFiles) {
    $relative = Get-RelativePath $xamlFile.FullName
    foreach ($match in $staticResourceRegex.Matches($xamlTexts[$xamlFile.FullName])) {
        $staticResourceReferences.Add([PSCustomObject]@{
            File = $relative
            Key = $match.Groups[1].Value
        })
    }
}

foreach ($reference in $staticResourceReferences) {
    if (-not $allResourceKeys.Contains($reference.Key)) {
        Add-PreflightError "$($reference.File): StaticResource '$($reference.Key)' has no x:Key definition in the source XAML tree."
    }
}

foreach ($xamlFile in $xamlFiles) {
    if (-not $xamlDocuments.ContainsKey($xamlFile.FullName)) {
        continue
    }

    $relative = Get-RelativePath $xamlFile.FullName
    $document = $xamlDocuments[$xamlFile.FullName]
    $rootElement = $document.DocumentElement
    $className = if ($null -ne $rootElement) { $rootElement.GetAttribute("Class", $xamlNamespace) } else { "" }
    $codeBehindPath = "$($xamlFile.FullName).cs"
    $codeBehindText = ""

    if (-not [string]::IsNullOrWhiteSpace($className)) {
        if (-not (Test-Path $codeBehindPath -PathType Leaf)) {
            Add-PreflightError "${relative}: x:Class '$className' has no matching '$([IO.Path]::GetFileName($codeBehindPath))'."
        }
        else {
            $codeBehindText = Get-Content $codeBehindPath -Raw
            $declaredType = $className.Substring($className.LastIndexOf('.') + 1)
            if ($codeBehindText -notmatch "(?m)\\bpartial\\s+class\\s+$([Regex]::Escape($declaredType))\\b") {
                Add-PreflightError "${relative}: code-behind does not declare partial class '$declaredType'."
            }
        }
    }

    foreach ($node in $document.SelectNodes("//*")) {
        if ($null -eq $node.Attributes) {
            continue
        }

        foreach ($attribute in $node.Attributes) {
            $eventName = $attribute.LocalName
            if (-not $knownEventNames.Contains($eventName) -or $attribute.Value.StartsWith("{")) {
                continue
            }

            $handlerName = $attribute.Value.Trim()
            if ([string]::IsNullOrWhiteSpace($handlerName)) {
                Add-PreflightError "${relative}: <$($node.LocalName)> has an empty '$eventName' event handler."
                continue
            }

            if (Test-IsUnsafeSetterEvent $node) {
                Add-PreflightError "${relative}: <$($node.LocalName)> wires '$eventName=$handlerName' inside Setter.Value. Move code-behind event wiring to a concrete control/object outside the shared Setter.Value object graph or use a command."
            }

            if (-not [string]::IsNullOrWhiteSpace($className) -and
                -not [string]::IsNullOrWhiteSpace($codeBehindText) -and
                $codeBehindText -notmatch "(?m)\\b$([Regex]::Escape($handlerName))\\s*\\(") {
                Add-PreflightError "${relative}: event handler '$handlerName' was not found in '$([IO.Path]::GetFileName($codeBehindPath))'."
            }

            if ($wpfReflectionAvailable -and $node.NamespaceURI -eq $presentationNamespace) {
                $wpfType = Resolve-WpfType $node.LocalName
                if ($null -ne $wpfType -and $null -eq $wpfType.GetEvent($eventName)) {
                    Add-PreflightError "${relative}: '$eventName' is not a public event on WPF type '$($wpfType.FullName)' used by <$($node.LocalName)>."
                }
            }
        }
    }
}

$sourceFiles = @(Get-ChildItem (Join-Path $root "src") -Recurse -File -Filter *.cs | Sort-Object FullName)

# Project-style guard for a recurring Roslyn declaration-space failure. Pattern variables
# using project/WPF type names are intentionally not reused within the same method. This
# conservative rule catches CS0136-prone shadowing without claiming to replace Roslyn.
$csharpMethodHeaderRegex = [Regex]::new(
    '^\s*(?:public|private|protected|internal)\s+(?:(?:static|async|sealed|override|virtual|partial|new|extern|unsafe)\s+)*(?:[A-Za-z_][A-Za-z0-9_.<>,?\[\]]*\s+)?(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>]+>)?\s*\(')
$csharpPatternVariableRegex = [Regex]::new(
    '\bis\s+(?:not\s+)?[A-Z][A-Za-z0-9_.<>,?\[\]]*\s+(?<name>(?!(?:or|and|not|when)\b)[A-Za-z_][A-Za-z0-9_]*)\b')

foreach ($sourceFile in $sourceFiles) {
    $relative = Get-RelativePath $sourceFile.FullName
    $lines = @(Get-Content $sourceFile.FullName)
    $currentMethod = $null
    $patternVariables = @{}
    $inBlockComment = $false

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $codeLine = [string]$lines[$lineIndex]
        $trimmedLine = $codeLine.Trim()

        if ($trimmedLine.StartsWith('/*', [StringComparison]::Ordinal)) {
            $inBlockComment = $true
        }

        if ($inBlockComment) {
            if ($trimmedLine.Contains('*/')) {
                $inBlockComment = $false
            }
            continue
        }

        $lineCommentIndex = $codeLine.IndexOf('//', [StringComparison]::Ordinal)
        if ($lineCommentIndex -ge 0) {
            $codeLine = $codeLine.Substring(0, $lineCommentIndex)
        }

        $methodMatch = $csharpMethodHeaderRegex.Match($codeLine)
        if ($methodMatch.Success) {
            $currentMethod = $methodMatch.Groups['method'].Value
            $patternVariables = @{}
        }

        if ([string]::IsNullOrWhiteSpace($currentMethod)) {
            continue
        }

        foreach ($patternMatch in $csharpPatternVariableRegex.Matches($codeLine)) {
            $variableName = $patternMatch.Groups['name'].Value
            $currentLineNumber = $lineIndex + 1
            if ($patternVariables.ContainsKey($variableName)) {
                Add-PreflightError "${relative}: method '$currentMethod' reuses pattern variable '$variableName' at lines $($patternVariables[$variableName]) and $currentLineNumber. Reuse one local or choose distinct names to avoid C# declaration-space conflicts such as CS0136."
            }
            else {
                $patternVariables[$variableName] = $currentLineNumber
            }
        }
    }
}

$sourceTypeIndex = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($sourceFile in $sourceFiles) {
    $text = Get-Content $sourceFile.FullName -Raw
    $namespaceMatch = [Regex]::Match($text, "(?m)^\\s*namespace\\s+([A-Za-z_][A-Za-z0-9_.]*)\\s*[;{]")
    if (-not $namespaceMatch.Success) {
        continue
    }

    $namespaceName = $namespaceMatch.Groups[1].Value
    foreach ($typeMatch in [Regex]::Matches($text, "(?m)^\\s*(?:public|internal|private|protected)?\\s*(?:sealed\\s+|static\\s+|abstract\\s+|partial\\s+)*(?:class|record|struct|interface|enum)\\s+([A-Za-z_][A-Za-z0-9_]*)\\b")) {
        [void]$sourceTypeIndex.Add("$namespaceName.$($typeMatch.Groups[1].Value)")
    }
}

$clrNamespaceRegex = [Regex]::new('xmlns:(?<prefix>[A-Za-z_][A-Za-z0-9_]*)="clr-namespace:(?<namespace>[^";]+)(?:;assembly=(?<assembly>[^"]+))?"')
foreach ($xamlFile in $xamlFiles) {
    $relative = Get-RelativePath $xamlFile.FullName
    $text = $xamlTexts[$xamlFile.FullName]

    foreach ($declaration in $clrNamespaceRegex.Matches($text)) {
        $prefix = $declaration.Groups["prefix"].Value
        $namespaceName = $declaration.Groups["namespace"].Value
        $assemblyName = $declaration.Groups["assembly"].Value
        if (-not [string]::IsNullOrWhiteSpace($assemblyName) -and
            $assemblyName -notlike "TeeKay87.MemoryEngine*") {
            continue
        }

        $typeNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($match in [Regex]::Matches($text, "</?$([Regex]::Escape($prefix)):(?<type>[A-Za-z_][A-Za-z0-9_]*)\\b")) {
            [void]$typeNames.Add($match.Groups["type"].Value)
        }
        foreach ($match in [Regex]::Matches($text, "\\b$([Regex]::Escape($prefix)):(?<type>[A-Za-z_][A-Za-z0-9_]*)\\.")) {
            [void]$typeNames.Add($match.Groups["type"].Value)
        }

        foreach ($typeName in $typeNames) {
            $qualifiedName = "$namespaceName.$typeName"
            if (-not $sourceTypeIndex.Contains($qualifiedName)) {
                Add-PreflightError "${relative}: XAML references '$qualifiedName', but no matching source type was found."
            }
        }
    }
}

$jsonFiles = @(Get-ChildItem (Join-Path $root "src") -Recurse -File -Filter *.json | Sort-Object FullName)
foreach ($jsonFile in $jsonFiles) {
    try {
        $null = Get-Content $jsonFile.FullName -Raw | ConvertFrom-Json
    }
    catch {
        Add-PreflightError "$(Get-RelativePath $jsonFile.FullName): invalid JSON: $($_.Exception.Message)"
    }
}

$projectFiles = @(Get-ChildItem $root -Recurse -File -Filter *.csproj | Sort-Object FullName)
foreach ($projectFile in $projectFiles) {
    $relative = Get-RelativePath $projectFile.FullName
    try {
        [xml]$projectXml = Get-Content $projectFile.FullName -Raw
    }
    catch {
        Add-PreflightError "${relative}: invalid project XML: $($_.Exception.Message)"
        continue
    }

    foreach ($reference in $projectXml.SelectNodes("//*[local-name()='ProjectReference']")) {
        $include = $reference.GetAttribute("Include")
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $resolvedReference = [IO.Path]::GetFullPath((Join-Path $projectFile.DirectoryName $include))
        if (-not (Test-Path $resolvedReference -PathType Leaf)) {
            Add-PreflightError "${relative}: ProjectReference '$include' does not exist."
        }
    }
}

$appInfoPath = Join-Path $root "src\TeeKay87.MemoryEngine.App\Application\AppInfo.cs"
if (Test-Path $appInfoPath -PathType Leaf) {
    $appInfoText = Get-Content $appInfoPath -Raw
    $versionMatch = [Regex]::Match($appInfoText, 'Version\s*=\s*"([^"]+)"')
    $revisionMatch = [Regex]::Match($appInfoText, 'Revision\s*=\s*(\d+)')
    $featureMatch = [Regex]::Match($appInfoText, 'FeatureTitle\s*=\s*"([^"]+)"')

    if ($versionMatch.Success -and $revisionMatch.Success -and $featureMatch.Success) {
        $version = $versionMatch.Groups[1].Value
        $revision = [int]$revisionMatch.Groups[1].Value
        $feature = $featureMatch.Groups[1].Value
        $displayVersion = "${version}.rev${revision}"
        $readmePath = Join-Path $root "README.md"
        $changelogPath = Join-Path $root "CHANGELOG.md"
        $verificationPath = Join-Path $root "docs\testing\APP_${version}_REV${revision}_VERIFICATION.md"

        if ((Get-Content $readmePath -Raw) -notmatch [Regex]::Escape("**$displayVersion - $feature**")) {
            Add-PreflightError "README.md does not identify the current build as '$displayVersion - $feature'."
        }

        if ((Get-Content $changelogPath -Raw) -notmatch [Regex]::Escape("TeeKay87's Memory Engine $displayVersion - $feature")) {
            Add-PreflightError "CHANGELOG.md has no entry for '$displayVersion - $feature'."
        }

        if (-not (Test-Path $verificationPath -PathType Leaf)) {
            Add-PreflightError "Missing current verification document 'docs\testing\APP_${version}_REV${revision}_VERIFICATION.md'."
        }
        else {
            $verificationText = Get-Content $verificationPath -Raw
            $expectedCheckMatch = [Regex]::Match($verificationText, 'Expected automated checks:\s+(\d+)')
            if ($expectedCheckMatch.Success) {
                $testProgramPath = Join-Path $root "tests\TeeKay87.MemoryEngine.Tests\Program.cs"
                if (Test-Path $testProgramPath -PathType Leaf) {
                    $testProgramText = Get-Content $testProgramPath -Raw
                    $registeredChecks = [Regex]::Matches(
                        $testProgramText,
                        '(?m)^\s*\("[^"]+",\s*[A-Za-z_][A-Za-z0-9_]*\),?\s*$').Count
                    $expectedChecks = [int]$expectedCheckMatch.Groups[1].Value
                    if ($registeredChecks -ne $expectedChecks) {
                        Add-PreflightError "Verification registry contains $registeredChecks checks, but the current verification document expects $expectedChecks."
                    }
                }
            }
        }
    }
    else {
        Add-PreflightError "AppInfo.cs does not expose recognizable Version, Revision, and FeatureTitle constants."
    }
}
else {
    Add-PreflightError "Missing centralized AppInfo.cs."
}

foreach ($artifactDirectoryName in @("bin", "obj", ".vs")) {
    foreach ($directory in Get-ChildItem $root -Recurse -Directory -Force | Where-Object { $_.Name -eq $artifactDirectoryName }) {
        Add-PreflightError "Release tree contains build/development artifact directory '$(Get-RelativePath $directory.FullName)'."
    }
}

Write-Host "TeeKay87's Memory Engine source preflight"
Write-Host "========================================="
Write-Host "Root: $root"
Write-Host "XAML files: $($xamlFiles.Count)"
Write-Host "C# source files: $($sourceFiles.Count)"
Write-Host "Project files: $($projectFiles.Count)"
Write-Host "JSON files: $($jsonFiles.Count)"

foreach ($warning in $warnings) {
    Write-Host "WARN  $warning" -ForegroundColor Yellow
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Host "FAIL  $errorMessage" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Source preflight failed with $($errors.Count) error(s)." -ForegroundColor Red
    exit 1
}

Write-Host "PASS  XAML/XML structure and event wiring"
Write-Host "PASS  StaticResource definitions"
Write-Host "PASS  Source-backed clr-namespace XAML types"
Write-Host "PASS  C# pattern-variable declaration spaces"
Write-Host "PASS  JSON syntax"
Write-Host "PASS  Project references"
Write-Host "PASS  Version/revision/verification consistency"
Write-Host "PASS  Release-tree artifact check"
Write-Host ""
Write-Host "Source preflight passed. A real Windows/WPF build is still required before release verification." -ForegroundColor Green
exit 0
