"""Publishes a packed mod to Nexus Mods as a new version of its existing file.

    python tools/nexus-release.py SmartRestockEmployees            # show what would be sent
    python tools/nexus-release.py SmartRestockEmployees --publish  # actually send it

Without --publish nothing leaves this machine except read-only lookups, so the default run is the
review step: it prints the zip, the version, the file description and the changelog exactly as
Nexus would receive them.

Everything published is read from files already in the repo, so a release is: bump ModInfo.Version,
write the changelog section in <Mod>/Publish/README.md, pack with tools/pack.ps1, run this.

  version      <Mod>/Main.cs, ModInfo.Version
  zip          <Mod>/Publish/out/<Mod>-<version>-Nexus.zip, from tools/pack.ps1
  changelog    the "### <version>" section of <Mod>/Publish/README.md
  description  the first fenced block under the "File Description" heading of <Mod>/Publish/nexus.md
  mod, file    tools/nexus.json

The API key is read from NEXUS_API_KEY in the environment or in .env at the repository root. It is
sent only to api.nexusmods.com and is never printed.

The upload itself follows Nexus's own GitHub action (github.com/Nexus-Mods/upload-action): a
multipart upload to presigned URLs, then finalise, wait until the upload is available, attach it
to the file as a new version, and add the changelog.
"""

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API_V1 = "https://api.nexusmods.com/v1"
API_V3 = "https://api.nexusmods.com/v3"
USER_AGENT = "AnimeShopMods-release/1.0"
FILE_DESCRIPTION_LIMIT = 255


class ReleaseError(Exception):
    pass


# ----------------------------------------------------------------------------- reading the repo

def read_api_key():
    key = os.environ.get("NEXUS_API_KEY")
    env_path = os.path.join(ROOT, ".env")
    if not key and os.path.exists(env_path):
        with open(env_path, encoding="utf-8") as env:
            for line in env:
                name, _, value = line.strip().partition("=")
                if name.strip() == "NEXUS_API_KEY":
                    key = value.strip().strip('"').strip("'")
    if not key:
        raise ReleaseError("NEXUS_API_KEY is not set, in the environment or in .env at the repo root.")
    return key


def read_version(mod):
    path = os.path.join(ROOT, mod, "Main.cs")
    with open(path, encoding="utf-8") as source:
        match = re.search(r'const string Version = "([^"]+)"', source.read())
    if not match:
        raise ReleaseError(f"No ModInfo.Version in {path}.")
    return match.group(1)


def read_changelog(mod, version):
    path = os.path.join(ROOT, mod, "Publish", "README.md")
    with open(path, encoding="utf-8") as readme:
        text = readme.read()
    match = re.search(rf"^### {re.escape(version)}\s*$(.*?)(?=^### |\Z)", text, re.M | re.S)
    if not match:
        raise ReleaseError(f'No "### {version}" section in {path}. Write the changelog before publishing.')
    body = match.group(1).strip()
    # Nexus shows changelog entries as plain text; Markdown markers would show up literally.
    body = re.sub(r"\*\*(.+?)\*\*", r"\1", body, flags=re.S)
    body = body.replace("`", "")
    # Rejoin hard-wrapped lines so each paragraph or list item is one line on the page. A list item
    # starts a new line even without a blank line before it, which is how the READMEs write lists.
    paragraphs = []
    for block in re.split(r"\n\s*\n", body):
        items, current = [], []
        for line in block.splitlines():
            if re.match(r"\s*[-*] ", line) and current:
                items.append(" ".join(current))
                current = []
            current.append(line.strip())
        if current:
            items.append(" ".join(current))
        paragraphs.append("\n".join(items))
    return "\n\n".join(p for p in paragraphs if p)


def read_file_description(mod):
    path = os.path.join(ROOT, mod, "Publish", "nexus.md")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as copy:
        text = copy.read()
    heading = re.search(r"^##.*File Description.*$", text, re.M)
    if not heading:
        return None
    block = re.search(r"```\n(.*?)```", text[heading.end():], re.S)
    return block.group(1).strip() if block else None


def zip_path(mod, version):
    return os.path.join(ROOT, mod, "Publish", "out", f"{mod}-{version}-Nexus.zip")


# ----------------------------------------------------------------------------- talking to Nexus

def request(method, url, key=None, body=None, headers=None, raw=None):
    all_headers = {"User-Agent": USER_AGENT}
    if key:
        all_headers["apikey"] = key
    data = raw
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        all_headers["Content-Type"] = "application/json"
    all_headers.update(headers or {})
    req = urllib.request.Request(url, data=data, method=method, headers=all_headers)
    try:
        with urllib.request.urlopen(req, timeout=120) as response:
            payload = response.read()
            return response, payload
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")[:500]
        # The URL may be a presigned one carrying credentials of its own; show only its host and path.
        shown = url.split("?")[0]
        raise ReleaseError(f"{method} {shown} failed: {error.code} {detail}") from None


def get_json(url, key):
    _, payload = request("GET", url, key)
    return json.loads(payload)


def resolve_v3_ids(game, mod_id, file_id, key):
    """The upload API speaks its own ids, not the ones in a mod page's URL.

    nexus.json holds the ids a person can read off the site (mods/4, file 20). The v3 API rejects
    those with "Mod file not found", so they are translated here through the game-scoped lookups
    the API offers for exactly this.
    """
    v3_mod = get_json(f"{API_V3}/games/{game}/mods/{mod_id}", key)["data"]["id"]
    v3_file = get_json(f"{API_V3}/games/{game}/mod-file-versions/{file_id}", key)["data"]["file"]["id"]
    return v3_mod, v3_file


def upload(zip_file, key):
    size = os.path.getsize(zip_file)
    _, payload = request("POST", f"{API_V3}/uploads/multipart", key,
                         body={"filename": os.path.basename(zip_file), "size_bytes": str(size)})
    created = json.loads(payload)["data"]
    upload_id = created["id"]
    part_size = created["part_size_bytes"]
    urls = created["part_presigned_urls"]
    print(f"  upload {upload_id}: {len(urls)} part(s)")

    parts = []
    with open(zip_file, "rb") as source:
        for number, url in enumerate(urls, start=1):
            chunk = source.read(part_size)
            response, _ = request("PUT", url, raw=chunk,
                                  headers={"Content-Type": "application/octet-stream",
                                           "Content-Length": str(len(chunk))})
            etag = (response.headers.get("ETag") or "").replace('"', "")
            if not etag:
                raise ReleaseError(f"Part {number} was accepted but no ETag came back.")
            parts.append((number, etag))

    xml = "<CompleteMultipartUpload>\n" + "\n".join(
        f"  <Part>\n    <PartNumber>{n}</PartNumber>\n    <ETag>{e}</ETag>\n  </Part>" for n, e in parts
    ) + "\n</CompleteMultipartUpload>"
    request("POST", created["complete_presigned_url"], raw=xml.encode("utf-8"),
            headers={"Content-Type": "application/xml"})

    request("POST", f"{API_V3}/uploads/{upload_id}/finalise", key)

    # Nexus scans the file before it can be attached; this mirrors the official action's back-off.
    delay = 2.0
    for _ in range(60):
        state = get_json(f"{API_V3}/uploads/{upload_id}", key)["data"]["state"]
        print(f"  upload state: {state}")
        if state == "available":
            return upload_id
        time.sleep(delay)
        delay = min(delay * 1.5, 30.0)
    raise ReleaseError(f"Upload {upload_id} never became available.")


# ----------------------------------------------------------------------------- the release

def main():
    parser = argparse.ArgumentParser(description="Publish a packed mod to Nexus Mods.")
    parser.add_argument("mod", help="Project folder name, e.g. SmartRestockEmployees")
    parser.add_argument("--publish", action="store_true", help="Actually upload. Without it, only show the plan.")
    parser.add_argument("--force", action="store_true", help="Publish even if Nexus already shows this version.")
    args = parser.parse_args()

    with open(os.path.join(ROOT, "tools", "nexus.json"), encoding="utf-8") as config_file:
        config = json.load(config_file)
    target = config["mods"].get(args.mod)
    if not target:
        raise ReleaseError(f"{args.mod} is not in tools/nexus.json.")
    game, mod_id, file_id = config["game"], target["mod_id"], target["file_id"]

    key = read_api_key()
    version = read_version(args.mod)
    zip_file = zip_path(args.mod, version)
    if not os.path.exists(zip_file):
        raise ReleaseError(f"{zip_file} does not exist. Pack it first:\n"
                           f"  powershell -ExecutionPolicy Bypass -File tools\\pack.ps1 -Mod {args.mod}")
    changelog = read_changelog(args.mod, version)
    description = read_file_description(args.mod)
    if description and len(description.replace("\n", "\r\n")) > FILE_DESCRIPTION_LIMIT:
        raise ReleaseError(f"The file description is over {FILE_DESCRIPTION_LIMIT} characters; Nexus would cut it.")

    page = get_json(f"{API_V1}/games/{game}/mods/{mod_id}.json", key)
    live = page.get("version")
    # The page's version lags behind a fresh upload for a while, so it cannot be the only guard
    # against sending the same release twice: a file already carrying this version counts too.
    files = get_json(f"{API_V1}/games/{game}/mods/{mod_id}/files.json", key).get("files", [])
    if any(f.get("version") == version for f in files):
        live = version

    print(f"Mod        {page.get('name')}  (nexusmods.com/{game}/mods/{mod_id})")
    print(f"Version    {live} on Nexus  ->  {version} here")
    print(f"Zip        {zip_file}  ({os.path.getsize(zip_file):,} bytes)")
    print(f"File       new version of file {file_id}; the previous version is archived")
    print(f"\nFile description ({len(description.replace(chr(10), chr(13) + chr(10))) if description else 0}/{FILE_DESCRIPTION_LIMIT}):")
    print(description or "  (none: nexus.md has no File Description block)")
    print("\nChangelog:")
    print(changelog)

    if live == version and not args.force:
        print(f"\nNexus already shows {version}. Nothing to publish (use --force to send it anyway).")
        return
    if not args.publish:
        print("\nDry run. Nothing was uploaded. Add --publish to send this.")
        return

    print("\nPublishing...")
    v3_mod, v3_file = resolve_v3_ids(game, mod_id, file_id, key)
    upload_id = upload(zip_file, key)
    _, payload = request("POST", f"{API_V3}/mod-files/{v3_file}/versions", key, body={
        "upload_id": upload_id,
        "name": f"{args.mod} {version}",
        "description": description,
        "version": version,
        "file_category": "main",
        "archive_existing_file": True,
        "update_mod_version": True,
    })
    version_id = json.loads(payload)["data"]["version"]["id"]
    print(f"  file version {version_id} created")
    request("POST", f"{API_V3}/mods/{v3_mod}/changelogs", key, body={"version": version, "changelog": changelog})
    print("  changelog added")
    print(f"\nPublished {args.mod} {version}: https://www.nexusmods.com/{game}/mods/{mod_id}?tab=files")


if __name__ == "__main__":
    # Progress and errors must come out in the order they happen. Piped, stdout is block-buffered
    # while stderr is not, and a failure then prints before the step it failed in.
    sys.stdout.reconfigure(line_buffering=True)
    try:
        main()
    except ReleaseError as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)
