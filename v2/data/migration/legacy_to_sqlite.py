"""Dry-runable, *copy-on-import* legacy v0.1.7 JSON -> isolated SQLite contract.

This is a synthetic-fixture/test harness and migration reference implementation,
NOT the Windows client. It never writes the legacy directory or any model path.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sqlite3
import tempfile
import uuid

SCHEMA_VERSION = 1
_NAMESPACE = uuid.UUID('b16c63c3-7d7a-4df2-b0a4-f39fa8f47f62')
_HEX = re.compile(r'^[0-9a-fA-F]{64}$')


class MigrationError(ValueError):
    pass


def load_json(path: Path, required: bool = False):
    if not path.is_file():
        if required:
            raise MigrationError(f'缺少必要旧数据: {path.name}')
        return None
    try:
        return json.loads(path.read_text(encoding='utf-8-sig'))
    except (ValueError, UnicodeError) as e:
        raise MigrationError(f'JSON 损坏：{path.name}: {e}') from e


def read_legacy(root: Path) -> tuple[dict, dict, dict]:
    catalog = load_json(root / 'catalog.json', required=True)
    covers = load_json(root / 'covers.json') or {}
    categories = load_json(root / 'ui-categories.v1.json') or {
        'schema': 1, 'categories': [], 'assignments': {}
    }
    if not isinstance(catalog, dict) or not isinstance(catalog.get('assets'), list):
        raise MigrationError('catalog.json 需要包含 assets 数组')
    if not isinstance(covers, dict):
        raise MigrationError('covers.json 必须是旧 ID 到封面文件名的对象')
    if not isinstance(categories, dict) or categories.get('schema') != 1:
        raise MigrationError('旧分类必须是 schema=1')
    if not isinstance(categories.get('categories'), list) or not isinstance(categories.get('assignments'), dict):
        raise MigrationError('分类定义/关联格式不正确')
    names = categories['categories']
    if any(not isinstance(c, str) or not c.strip() for c in names) or len(names) != len(set(names)):
        raise MigrationError('分类定义存在空值或重复')
    assets = catalog['assets']
    ids = set()
    for n, a in enumerate(assets):
        if not isinstance(a, dict) or not isinstance(a.get('id'), str) or not _HEX.fullmatch(a['id']):
            raise MigrationError(f'素材 #{n + 1} 缺少合法的旧 ID')
        old = a['id'].lower()
        if old in ids:
            raise MigrationError(f'旧 ID 重复: {old}')
        ids.add(old)
        if any(not isinstance(a.get(k), str) or not a[k] for k in ('full', 'name', 'kind')):
            raise MigrationError(f'旧素材 {old} 缺少路径/名称/类型')
        # Paths are recorded only; no model read or write occurs.
        if not re.match(r'^[a-zA-Z]:\\', a['full']):
            raise MigrationError(f'旧素材 {old} 的绝对 Windows 路径无效')
    # Old Windows IDs are hex SHA256; make key lookups insensitive to casing.
    if len({k.lower() for k in covers if isinstance(k, str)}) != len(covers):
        raise MigrationError('封面关联含有仅大小写不同的重复 ID')
    if len({k.lower() for k in categories['assignments'] if isinstance(k, str)}) != len(categories['assignments']):
        raise MigrationError('分类关联含有仅大小写不同的重复 ID')
    for key, name in covers.items():
        if not isinstance(key, str) or key.lower() not in ids:
            raise MigrationError(f'存在找不到素材的封面关联: {key}')
        if not isinstance(name, str) or not name or Path(name).name != name or name in ('.', '..') or '/' in name or '\\' in name:
            raise MigrationError(f'封面文件名无效: {name!r}')
        actual = root / 'covers' / name
        if not actual.is_file() or actual.is_symlink():
            raise MigrationError(f'封面文件丢失或不允许软链接: {name}')
    for key, category in categories['assignments'].items():
        if not isinstance(key, str) or key.lower() not in ids:
            raise MigrationError(f'存在找不到素材的分类关联: {key}')
        if category not in names:
            raise MigrationError(f'分类不存在: {category}')
    covers = {k.lower(): v for k, v in covers.items()}
    categories['assignments'] = {k.lower(): v for k, v in categories['assignments'].items()}
    return catalog, covers, categories


def _sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open('rb') as f:
        for piece in iter(lambda: f.read(1024 * 1024), b''):
            h.update(piece)
    return h.hexdigest()


def dry_run(legacy: Path) -> dict:
    catalog, covers, cats = read_legacy(legacy)
    return {
        'status': 'validated',
        'assets': len(catalog['assets']),
        'covers': len(covers),
        'assignments': len(cats['assignments']),
        'legacy_source_untouched': True,
        'model_files_not_opened': True,
    }


def _write_db(db: Path, catalog: dict, covers: dict, cats: dict, filehashes: dict[str, str]) -> dict:
    con = sqlite3.connect(str(db))
    try:
        con.execute('PRAGMA foreign_keys=ON')
        con.executescript('''
        CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE assets(
          id TEXT PRIMARY KEY, legacy_id TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
          kind TEXT NOT NULL, ext TEXT NOT NULL, full_path TEXT NOT NULL,
          relative_path TEXT NOT NULL, preview TEXT NOT NULL, bytes INTEGER NOT NULL,
          changed TEXT NOT NULL, resources TEXT NOT NULL
        );
        CREATE TABLE categories(name TEXT PRIMARY KEY);
        CREATE TABLE assignments(asset_id TEXT NOT NULL PRIMARY KEY REFERENCES assets(id),
          category TEXT NOT NULL REFERENCES categories(name));
        CREATE TABLE covers(asset_id TEXT NOT NULL PRIMARY KEY REFERENCES assets(id),
          filename TEXT NOT NULL, sha256 TEXT NOT NULL);
        CREATE INDEX asset_names ON assets(name);
        ''')
        con.execute('INSERT INTO meta VALUES (?,?)', ('schema', str(SCHEMA_VERSION)))
        con.execute('INSERT INTO meta VALUES (?,?)', ('source_format', 'v0.1.7-json'))
        con.executemany('INSERT INTO categories VALUES (?)', [(s,) for s in cats['categories']])
        count = 0
        for a in catalog['assets']:
            old = a['id'].lower()
            aid = str(uuid.uuid5(_NAMESPACE, old))  # permanent at import, independent of drive letter later
            con.execute('INSERT INTO assets VALUES (?,?,?,?,?,?,?,?,?,?,?)', (
                aid,old,a['name'],a['kind'],str(a.get('ext') or ''),a['full'],
                str(a.get('rel') or ''),str(a.get('preview') or ''),
                int(a.get('bytes') or 0),str(a.get('changed') or ''),str(a.get('resources') or '')
            ))
            if old in covers:
                name = covers[old]
                con.execute('INSERT INTO covers VALUES (?,?,?)', (aid,name,filehashes[name]))
            if old in cats['assignments']:
                con.execute('INSERT INTO assignments VALUES (?,?)', (aid,cats['assignments'][old]))
            count += 1
        assert con.execute('SELECT COUNT(*) FROM assets').fetchone()[0] == count
        assert con.execute('PRAGMA foreign_key_check').fetchall() == []
        assert con.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        con.commit()
        return {'assets':count,'covers':len(covers),'assignments':len(cats['assignments'])}
    finally:
        con.close()


def import_legacy(legacy: Path, destination: Path) -> dict:
    """Publish a brand-new isolated snapshot. Never overwrite existing destination."""
    legacy = legacy.resolve(strict=True)
    catalog, covers, cats = read_legacy(legacy)
    destination = destination.absolute()
    if destination.exists() or destination.is_symlink():
        raise MigrationError('目标目录已经存在，拒绝覆盖已有 V2 或 Stable/Beta 数据')
    if destination == legacy or legacy in destination.parents or destination in legacy.parents:
        raise MigrationError('目标必须位于旧数据目录之外，不允许向旧数据目录写入')
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.v2-import-', dir=destination.parent))
    try:
        (staging / 'covers').mkdir()
        old_source_snapshot = staging / 'legacy-snapshot'
        old_source_snapshot.mkdir()
        original_sha = {}
        for name in ('catalog.json', 'covers.json', 'ui-categories.v1.json'):
            file = legacy / name
            if file.exists():
                shutil.copy2(file, old_source_snapshot / name)
                original_sha[name] = _sha(file)
        filehashes = {}
        for name in set(covers.values()):
            oldfile = legacy / 'covers' / name
            # Prevent symlink swap to a path outside covers directory.
            if not oldfile.resolve().is_relative_to((legacy / 'covers').resolve()):
                raise MigrationError('封面源路径不安全')
            shutil.copy2(oldfile, staging / 'covers' / name)
            filehashes[name] = _sha(oldfile)
            if _sha(staging / 'covers' / name) != filehashes[name]:
                raise MigrationError(f'封面复制校验失败: {name}')
        stats = _write_db(staging / 'catalog.sqlite3',catalog,
                          {k.lower():v for k,v in covers.items()},
                          {'categories':cats['categories'], 'assignments':{k.lower():v for k,v in cats['assignments'].items()}},filehashes)
        for name, sha in original_sha.items():
            if sha != _sha(legacy / name) or sha != _sha(old_source_snapshot / name):
                raise MigrationError(f'旧数据在迁移途中被修改: {name}')
        manifest = {'source_hashes':original_sha, 'covers_hashes':filehashes,
                    'schema':SCHEMA_VERSION, **stats}
        (staging / 'migration-report.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
        staging.rename(destination)
        return {'status':'imported','destination':str(destination),**stats}
    except Exception:
        shutil.rmtree(staging,ignore_errors=True)
        raise


def create_recovery_point(data_root: Path, backup_root: Path, retain: int = 3) -> Path:
    """Verified SQLite + cover snapshot, separate from versioned application files.

    Only for a quiescent V2 profile, never for the old v0.1.7 data directory.
    """
    if retain < 1:
        raise MigrationError('至少保留一个恢复点')
    if not (data_root / 'catalog.sqlite3').is_file():
        raise MigrationError('SQLite 文件不存在，拒绝伪造恢复点')
    backup_root.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.point-',dir=backup_root))
    try:
        con=sqlite3.connect(str(data_root / 'catalog.sqlite3'))
        try:
            if con.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise MigrationError('源数据库未通过完整性检查')
            dst=sqlite3.connect(str(staging / 'catalog.sqlite3'))
            try: con.backup(dst)
            finally: dst.close()
        finally: con.close()
        if (data_root / 'covers').is_dir():
            shutil.copytree(data_root / 'covers',staging / 'covers',symlinks=False)
        digest={str(p.relative_to(staging)): _sha(p) for p in staging.rglob('*') if p.is_file()}
        (staging / 'verify.json').write_text(json.dumps(digest,sort_keys=True),encoding='utf-8')
        if any(_sha(staging / key) != value for key,value in digest.items()):
            raise MigrationError('恢复点校验失败')
        verify=sqlite3.connect(str(staging / 'catalog.sqlite3'))
        try:
            if verify.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise MigrationError('恢复点数据库有错误')
        finally: verify.close()
        name='backup-'+uuid.uuid4().hex
        staging.rename(backup_root / name)
        finished=sorted((x for x in backup_root.iterdir() if x.is_dir() and x.name.startswith('backup-')),
                        key=lambda p:p.stat().st_mtime_ns,reverse=True)
        for old in finished[retain:]:
            # Retention only affects snapshots inside explicitly designated backup root.
            shutil.rmtree(old)
        return backup_root / name
    except Exception:
        shutil.rmtree(staging,ignore_errors=True)
        raise


def main() -> None:
    p = argparse.ArgumentParser(description='V2 legacy read-only importer contract; use only synthetic or backup data')
    p.add_argument('--legacy',type=Path,required=True)
    p.add_argument('--destination',type=Path)
    args=p.parse_args()
    try:
        result = import_legacy(args.legacy,args.destination) if args.destination else dry_run(args.legacy)
        print(json.dumps(result,ensure_ascii=False,indent=2))
    except (OSError,MigrationError,sqlite3.Error,ValueError) as e:
        p.exit(1,'迁移未完成，旧数据保持原状: '+str(e)+'\n')

if __name__=='__main__':
    main()