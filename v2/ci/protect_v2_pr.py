"""Flag unsafe file paths in V2 public GitHub PRs."""
import argparse
from pathlib import PurePosixPath
import re
import subprocess
import sys

BAD_EXT={".c4d",".fbx",".obj",".blend",".max",".3ds",".glb",".gltf",
".stl",".abc",".usd",".usdz",".skp",".hdr",".exr",".sbsar",".psd",
".psb",".tif",".tiff",".tga",".png",".jpg",".jpeg",".webp",".bmp",
".gif",".avif",".heic",".zip",".7z",".rar",".tar",".gz",".sqlite",
".sqlite3",".db",".pfx",".p12",".key",".exe",".msi",".msix"}
BAD_DIR={".local-data","user-assets","client-assets","customer-data",
"private-assets","client-renders","covers","thumbs"}
SHA=re.compile(r"^[0-9a-fA-F]{40}$")

def violation(name):
    if not name:
        return "empty path"
    path=name.replace("\\","/")
    bits=path.casefold().split("/")
    if path.startswith("/") or re.match(r"^[a-zA-Z]:/",path):
        return "absolute path"
    if any(v in ("",".","..") for v in bits):
        return "invalid path"
    if "/".join(bits).startswith("desktop-updates/native/"):
        return "frozen v0.1.7 native app/update directory"
    if any(v in BAD_DIR for v in bits):
        return "private user data/cache directory"
    if PurePosixPath("/".join(bits)).suffix in BAD_EXT:
        return "binary, render, asset, database, archive or signing key"
    return None

def verify(names):
    return [(n,reason) for n in names if (reason:=violation(n))]

def changed(base,head):
    if not SHA.fullmatch(base) or not SHA.fullmatch(head):
        raise ValueError("Commit SHA must be 40 hex characters")
    out=subprocess.run(["git","diff","--name-only","--no-renames",
                        "-z",base,head],check=True,capture_output=True).stdout
    return [x.decode("utf-8","surrogateescape") for x in out.split(b"\0") if x]

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--base",required=True)
    p.add_argument("--head",required=True)
    a=p.parse_args()
    try:
        names=changed(a.base,a.head)
    except (OSError,ValueError,subprocess.CalledProcessError) as e:
        print("Cannot verify changes:",e,file=sys.stderr)
        return 2
    errs=verify(names)
    for path,reason in errs:
        print("BLOCK",repr(path),reason,file=sys.stderr)
    if errs:
        return 1
    print("PASS",len(names),"changed file paths; human content review still required")
    return 0

if __name__=="__main__":
    raise SystemExit(main())
