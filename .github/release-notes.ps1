# Prints release notes for a tag: Conventional Commit subjects since the previous tag, grouped by type, plus a fixed footer.
param([Parameter(Mandatory)][string]$Tag)

$version = $Tag.TrimStart('v')
$prev = git describe --tags --abbrev=0 "$Tag^" 2>$null
$range = if ($prev) { "$prev..$Tag" } else { $Tag }

# Types missing here (chore, refactor, test, style) are not user-facing and are left out.
$titles = [ordered]@{ feat = 'New'; fix = 'Fixes'; perf = 'Performance'; docs = 'Documentation'; build = 'Build'; ci = 'Build' }
$items = foreach ($subject in git log --no-merges --reverse --format=%s $range) {
    if ($subject -match '^(\w+)(\([^)]*\))?!?: (.+)$' -and $titles.Contains($Matches[1])) {
        [pscustomobject]@{ Title = $titles[$Matches[1]]; Text = '- ' + $Matches[3].Substring(0, 1).ToUpper() + $Matches[3].Substring(1) }
    }
}

$body = foreach ($title in $titles.Values | Select-Object -Unique) {
    $lines = @($items | Where-Object Title -eq $title)
    if ($lines) { "**$title**"; $lines.Text; '' }
}
if (-not $body) { $body = 'Maintenance release.', '' }

$body
"``winmangrus-$version-win-x64.zip`` contains winmangrus.exe, VirtualDesktopAccessor.dll and both licenses, for networks that block downloading .exe files. Unpack it into any folder."
''
'VirtualDesktopAccessor.dll is needed only for the desktop hotkeys; keep it in the same folder as winmangrus.exe. It is [VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor) by Jari Pennanen (Ciantic), MIT license.'
''
if ($prev) { "Full changelog: https://github.com/vdasus/winmangrus/compare/$prev...$Tag"; '' }
'Provided as is, without warranty. See README.'
