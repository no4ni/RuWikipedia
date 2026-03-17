param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    [Parameter(Mandatory=$true)]
    [string[]]$Keywords,
    [int]$TargetCount = 10
)

if (-not (Test-Path -LiteralPath $FilePath)) {
    Write-Output "Файл не найден: $FilePath"
    exit 1
}

$TargetCount = 10
$relevantProb = 0.7
$nonRelevantProb = 0.3

$selected = [System.Collections.Generic.List[string]]::new()
$encoding = [System.Text.UTF8Encoding]::new($false)

try {
    $reader = [System.IO.StreamReader]::new($FilePath, $encoding)
    while ($selected.Count -lt $TargetCount -and ($line = $reader.ReadLine()) -ne $null) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrEmpty($trimmed)) { continue }

        $containsKeyword = $false
        foreach ($kw in $Keywords) {
            if ($trimmed -like "*$kw*") {
                $containsKeyword = $true
                break
            }
        }

        $rand = Get-Random -Minimum 0.0 -Maximum 1.0
        if ($containsKeyword) {
            if ($rand -lt $relevantProb) {
                $selected.Add($trimmed)
            }
        } else {
            if ($rand -lt $nonRelevantProb) {
                $selected.Add($trimmed)
            }
        }
    }
    $reader.Close()
} catch {
    Write-Output "Ошибка при чтении файла: $_"
    exit 1
}

if ($selected.Count -eq 0) {
    Write-Output "Не найдено строк."
} else {
    $selected | ForEach-Object { Write-Output "- $_" }
}