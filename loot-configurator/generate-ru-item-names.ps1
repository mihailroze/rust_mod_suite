param(
  [string]$CatalogPath = (Join-Path $PSScriptRoot "catalog-embedded.js"),
  [string]$OutputPath = (Join-Path $PSScriptRoot "item-names-ru.js"),
  [int]$BatchSize = 12
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$apiUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t"

function Convert-CatalogJsToObject {
  param([string]$Path)

  $text = Get-Content -Path $Path -Raw -Encoding UTF8
  $start = $text.IndexOf("{")
  $end = $text.LastIndexOf("};")
  if ($start -lt 0 -or $end -lt 0 -or $end -le $start) {
    throw "Cannot parse catalog file: $Path"
  }

  $jsonText = $text.Substring($start, $end - $start + 1)
  return ($jsonText | ConvertFrom-Json)
}

function Invoke-TranslateBatch {
  param(
    [string[]]$SourceLines
  )

  if ($SourceLines.Count -eq 0) {
    return @{}
  }

  $joined = [string]::Join("`n", $SourceLines)
  $raw = & curl.exe -s -X POST $apiUrl --data-urlencode ("q=" + $joined)
  if ([string]::IsNullOrWhiteSpace($raw)) {
    return $null
  }

  try {
    $arr = $raw | ConvertFrom-Json
  } catch {
    return $null
  }

  $translatedParts = @()
  foreach ($segment in $arr[0]) {
    if ($null -ne $segment -and $segment.Count -gt 0) {
      $translatedParts += [string]$segment[0]
    }
  }

  $translatedJoined = [string]::Join("", $translatedParts)
  $translatedLines = @($translatedJoined -replace "`r", "" -split "`n")
  if ($translatedLines.Count -lt $SourceLines.Count) {
    return $null
  }

  $result = @{}
  for ($i = 0; $i -lt $SourceLines.Count; $i++) {
    $en = $SourceLines[$i]
    $ru = [string]$translatedLines[$i]
    if ([string]::IsNullOrWhiteSpace($ru)) {
      $ru = $en
    }
    $result[$en] = $ru.Trim()
  }

  return $result
}

function Invoke-TranslateOne {
  param([string]$SourceLine)

  for ($attempt = 1; $attempt -le 6; $attempt++) {
    $raw = & curl.exe -s -X POST $apiUrl --data-urlencode ("q=" + $SourceLine)
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
      try {
        $arr = $raw | ConvertFrom-Json
        $ru = [string]$arr[0][0][0]
        if (-not [string]::IsNullOrWhiteSpace($ru)) {
          return $ru.Trim()
        }
      } catch {
      }
    }
    Start-Sleep -Milliseconds (350 * $attempt)
  }

  return $SourceLine
}

$catalog = Convert-CatalogJsToObject -Path $CatalogPath

$displayToShortnames = @{}
foreach ($item in $catalog.Items) {
  $shortname = [string]$item.Shortname
  $displayName = [string]$item."Display name"
  if ([string]::IsNullOrWhiteSpace($shortname) -or [string]::IsNullOrWhiteSpace($displayName)) {
    continue
  }

  $shortname = $shortname.Trim().ToLowerInvariant()
  $displayName = $displayName.Trim()

  if (-not $displayToShortnames.ContainsKey($displayName)) {
    $displayToShortnames[$displayName] = New-Object System.Collections.Generic.List[string]
  }

  if (-not $displayToShortnames[$displayName].Contains($shortname)) {
    [void]$displayToShortnames[$displayName].Add($shortname)
  }
}

$displayNames = @($displayToShortnames.Keys | Sort-Object)
$displayToRu = @{}

for ($offset = 0; $offset -lt $displayNames.Count; $offset += $BatchSize) {
  $last = [Math]::Min($offset + $BatchSize - 1, $displayNames.Count - 1)
  $batch = @($displayNames[$offset..$last])

  $translated = $null
  for ($attempt = 1; $attempt -le 5; $attempt++) {
    $translated = Invoke-TranslateBatch -SourceLines $batch
    if ($null -ne $translated) {
      break
    }
    Start-Sleep -Milliseconds (250 * $attempt)
  }

  if ($null -eq $translated) {
    foreach ($en in $batch) {
      $displayToRu[$en] = Invoke-TranslateOne -SourceLine $en
    }
  } else {
    foreach ($en in $batch) {
      $displayToRu[$en] = [string]$translated[$en]
    }
  }

  Write-Host ("Translated {0}/{1}" -f ($last + 1), $displayNames.Count)
}

$shortToRu = @{}
foreach ($en in $displayNames) {
  $ru = [string]$displayToRu[$en]
  if ([string]::IsNullOrWhiteSpace($ru)) {
    $ru = $en
  }

  foreach ($shortname in $displayToShortnames[$en]) {
    $shortToRu[$shortname] = $ru
  }
}

$keys = @($shortToRu.Keys | Sort-Object)
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("window.DEFAULT_RU_ITEM_NAMES = {")

for ($i = 0; $i -lt $keys.Count; $i++) {
  $key = $keys[$i]
  $value = [string]$shortToRu[$key]
  $escapedKey = $key.Replace("\", "\\").Replace('"', '\"')
  $escapedValue = $value.Replace("\", "\\").Replace('"', '\"')
  $suffix = if ($i -lt $keys.Count - 1) { "," } else { "" }
  $lines.Add(('  "{0}": "{1}"{2}' -f $escapedKey, $escapedValue, $suffix))
}

$lines.Add("};")
Set-Content -Path $OutputPath -Value $lines -Encoding UTF8

Write-Host ("Done: {0} items -> {1}" -f $keys.Count, $OutputPath)
