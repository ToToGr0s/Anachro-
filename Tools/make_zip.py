"""Zippe un build avec les droits Unix (rwxr-xr-x) : un zip fait sous Windows n'en a pas, et sans le bit d'exécution
l'exécutable Linux et l'app macOS (Anachro.app/Contents/MacOS/Anachro) refusent de se lancer.

Usage : python make_zip.py <zip> <dossier du build> <dossier du projet> <entrée> [<entrée>…]
Les entrées sont relatives au dossier du build ; steam_appid.txt est ajouté depuis le dossier du projet.
"""
import os, sys, zipfile

zip_path, build_folder, project_folder, *entries = sys.argv[1:]
FILE_MODE = 0o100755
DIR_MODE = 0o040755


def add(archive, path, name):
    info = zipfile.ZipInfo.from_file(path, name)
    info.create_system = 3  # Unix : sans ça, macOS et unzip ignorent les droits
    if info.is_dir():
        info.external_attr = (DIR_MODE << 16) | 0x10
        archive.writestr(info, b"")
        return
    info.external_attr = FILE_MODE << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    with open(path, "rb") as source, archive.open(info, "w") as target:
        while chunk := source.read(1 << 20):
            target.write(chunk)


with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
    for entry in entries:
        root = os.path.join(build_folder, entry)
        if os.path.isfile(root):
            add(archive, root, entry)
            continue
        for folder, _, files in os.walk(root):
            add(archive, folder, os.path.relpath(folder, build_folder).replace(os.sep, "/") + "/")
            for file in files:
                path = os.path.join(folder, file)
                add(archive, path, os.path.relpath(path, build_folder).replace(os.sep, "/"))
    add(archive, os.path.join(project_folder, "steam_appid.txt"), "steam_appid.txt")
