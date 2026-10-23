# Zippe un build Unity et le publie en pré-release GitHub (tag build-…). Lancé par BuildPublisher.cs après chaque
# build non Development ; le workflow build-discord.yml poste ensuite le lien dans #releases.
# Test sans rien publier : -DryRun (crée seulement le zip et affiche la release qui serait créée).
param(
    [Parameter(Mandatory)][ValidateSet('Windows', 'Linux')][string]$Platform,
    [Parameter(Mandatory)][string]$BuildFolder,
    [Parameter(Mandatory)][string]$EntriesFile,
    [Parameter(Mandatory)][string]$ProjectFolder,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$Host.UI.RawUI.WindowTitle = "Publication du build $Platform"

try {
    Set-Location $ProjectFolder
    if (-not $DryRun -and -not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "gh (GitHub CLI) introuvable." }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
    $tag = "build-$stamp-$($Platform.ToLower())"
    $zip = Join-Path $env:TEMP "Anachro-$Platform-$stamp.zip"
    $branch = git rev-parse --abbrev-ref HEAD
    $commit = git rev-parse HEAD
    $short = git rev-parse --short HEAD
    $subject = git log -1 --pretty=%s

    # Une release doit viser un commit présent sur GitHub : sinon on prend la branche distante, à défaut Dev.
    $target = $commit
    if (-not (git branch -r --contains $commit)) {
        $target = git rev-parse --verify -q "origin/$branch"
        if (-not $target) { $target = git rev-parse origin/Dev }
    }

    Write-Host "Compression du build $Platform…"
    # Le tar de Windows (bsdtar) sait écrire un zip ; celui de Git for Windows non, d'où le chemin complet.
    # steam_appid.txt est ajouté depuis le projet : hors Steam, Steamworks en a besoin à côté de l'exécutable.
    $entries = Get-Content $EntriesFile | Where-Object { $_.Trim() }
    & "$env:SystemRoot\System32\tar.exe" -a -c -f $zip -C $BuildFolder @entries -C $ProjectFolder steam_appid.txt
    if ($LASTEXITCODE -ne 0) { throw "Échec de la compression (tar, code $LASTEXITCODE)." }
    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB)

    $notes = @("Branche ``$branch`` · commit ``$short`` : $subject")
    if ($target -ne $commit) { $notes += "⚠️ Commit local non poussé : la release pointe sur ``$($target.Substring(0, 7))``." }
    if (git status --porcelain --untracked-files=no) { $notes += "⚠️ Build fait avec des modifications non commitées." }
    if ($Platform -eq 'Linux') { $notes += "Linux : ``chmod +x Anachro.x86_64`` après extraction." }
    $notes += "Hors Steam : lancer Steam avant le jeu (AppID de test 480)."
    $notesFile = Join-Path $env:TEMP "$tag.md"
    [IO.File]::WriteAllText($notesFile, ($notes -join "`n"), (New-Object Text.UTF8Encoding $false))
    $title = "Build $Platform · $branch · $(Get-Date -Format 'dd/MM HH:mm')"

    if ($DryRun) {
        Write-Host "[DryRun] $title`n[DryRun] tag $tag → $target`n[DryRun] $zip ($sizeMb Mo)`n"
        Get-Content $notesFile
        & "$env:SystemRoot\System32\tar.exe" -t -f $zip | Select-Object -First 15
        exit 0
    }

    Write-Host "Envoi sur GitHub ($sizeMb Mo)…"
    gh release create $tag $zip --prerelease --title $title --notes-file $notesFile --target $target
    if ($LASTEXITCODE -ne 0) { throw "Échec de gh release create (code $LASTEXITCODE)." }
    Remove-Item $zip, $notesFile
    Write-Host "Publié. L'annonce dans #releases suit dans une minute." -ForegroundColor Green
    Start-Sleep -Seconds 5
}
catch {
    Write-Host $_ -ForegroundColor Red
    Read-Host "Entrée pour fermer"
    exit 1
}
