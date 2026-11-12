# Zippe un ou plusieurs builds Unity et les publie dans une seule pré-release GitHub (tag build-…). Lancé par
# BuildPublisher.cs ; le workflow build-discord.yml poste ensuite les liens dans #releases (un seul message).
# Manifeste : une ligne par plateforme, « Plateforme|dossier du build|entrée|entrée… » (entrées relatives au dossier).
# Test sans rien publier : -DryRun (crée seulement les zips et affiche la release qui serait créée).
param(
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$ProjectFolder,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

try {
    Set-Location $ProjectFolder
    $builds = @(Get-Content $Manifest | Where-Object { $_.Trim() } | ForEach-Object {
        $parts = $_.Split('|')
        [pscustomobject]@{ Platform = $parts[0]; Folder = $parts[1]; Entries = @($parts[2..($parts.Length - 1)]) }
    })
    if ($builds.Count -eq 0) { throw "Manifeste $Manifest vide." }
    $platforms = ($builds | ForEach-Object Platform) -join ' + '
    $Host.UI.RawUI.WindowTitle = "Publication du build $platforms"
    if (-not $DryRun -and -not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "gh (GitHub CLI) introuvable." }
    if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw "python introuvable (sert à zipper avec les droits d'exécution)." }

    # Secondes comprises : deux builds de la même plateforme dans la même minute auraient le même tag.
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $tag = "build-$stamp-" + (($builds | ForEach-Object { $_.Platform.ToLower() }) -join '-')
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
    # Le workflow qui poste sur Discord est lu dans le commit de la release : sans lui, la release partirait sans annonce.
    # ls-tree plutôt que cat-file -e : rien sur stderr, que PowerShell 5.1 transformerait en erreur bloquante.
    # --full-tree : le script tourne dans Anachro/, et ls-tree lit les chemins depuis le dossier courant.
    if (-not (git ls-tree --full-tree --name-only $target .github/workflows/build-discord.yml)) { throw "Le commit $($target.Substring(0, 7)) n'a pas build-discord.yml : mettre la branche à jour depuis Dev, la pousser, puis rebuilder." }

    $zips = @()
    foreach ($build in $builds) {
        Write-Host "Compression du build $($build.Platform)…"
        $zip = Join-Path $env:TEMP "Anachro-$($build.Platform)-$stamp.zip"
        # make_zip.py garde le bit d'exécution (Linux, macOS) qu'un zip fait sous Windows n'a pas. steam_appid.txt est
        # ajouté depuis le projet : hors Steam, Steamworks en a besoin à côté de l'exécutable.
        python "$PSScriptRoot\make_zip.py" $zip $build.Folder $ProjectFolder @($build.Entries)
        if ($LASTEXITCODE -ne 0) { throw "Échec de la compression $($build.Platform) (make_zip.py, code $LASTEXITCODE)." }
        $zips += $zip
    }
    $sizeMb = [math]::Round(($zips | ForEach-Object { (Get-Item $_).Length } | Measure-Object -Sum).Sum / 1MB)

    $notes = @("Branche ``$branch`` · commit ``$short`` : $subject")
    if ($target -ne $commit) { $notes += "⚠️ Commit local non poussé : la release pointe sur ``$($target.Substring(0, 7))``." }
    if (git status --porcelain --untracked-files=no) { $notes += "⚠️ Build fait avec des modifications non commitées." }
    # App non notariée (build fait sous Windows) : macOS la bloque tant que la quarantaine n'est pas retirée et l'app re-signée.
    if ($builds.Platform -contains 'macOS') { $notes += "macOS : après extraction, dans le Terminal, dans le dossier du jeu : ``xattr -cr Anachro.app && codesign --force --deep -s - Anachro.app``" }
    $notes += "Hors Steam : lancer Steam avant le jeu (AppID de test 480)."
    $notesFile = Join-Path $env:TEMP "$tag.md"
    [IO.File]::WriteAllText($notesFile, ($notes -join "`n"), (New-Object Text.UTF8Encoding $false))
    $title = "Build $platforms · $branch · $(Get-Date -Format 'dd/MM HH:mm')"

    if ($DryRun) {
        Write-Host "[DryRun] $title`n[DryRun] tag $tag → $target`n[DryRun] $($zips -join ', ') ($sizeMb Mo)`n"
        Get-Content $notesFile
        foreach ($zip in $zips) { python -c "import sys, zipfile; [print(oct(i.external_attr >> 16), i.filename) for i in zipfile.ZipFile(sys.argv[1]).infolist() if i.filename.rstrip('/').count('/') == 0]" $zip }
        exit 0
    }

    Write-Host "Envoi sur GitHub ($sizeMb Mo)…"
    gh release create $tag @zips --prerelease --title $title --notes-file $notesFile --target $target
    if ($LASTEXITCODE -ne 0) { throw "Échec de gh release create (code $LASTEXITCODE)." }
    Remove-Item ($zips + $notesFile + $Manifest)
    Write-Host "Publié. L'annonce dans #releases suit dans une minute." -ForegroundColor Green
    Start-Sleep -Seconds 5
}
catch {
    Write-Host $_ -ForegroundColor Red
    Read-Host "Entrée pour fermer"
    exit 1
}
