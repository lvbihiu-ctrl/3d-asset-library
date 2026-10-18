import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest

from legacy_to_sqlite import MigrationError, create_recovery_point, dry_run, import_legacy


def aid(s):
    return hashlib.sha256(s.lower().encode('utf-8')).hexdigest()


class MigrationContractTests(unittest.TestCase):
    def setUp(self):
        self.ctx = tempfile.TemporaryDirectory()
        self.addCleanup(self.ctx.cleanup)
        self.root = Path(self.ctx.name)
        self.legacy = self.root / 'legacy'
        self.legacy.mkdir()
        (self.legacy / 'covers').mkdir()
        self.p1 = 'X:\\models\\A\\lamp.c4d'
        self.p2 = 'X:\\models\\B\\lamp.c4d'
        self.i1 = aid(self.p1)
        self.i2 = aid(self.p2)
        self.assets = [
            {'id': self.i1,'name':'lamp','kind':'模型','full':self.p1,'rel':'A/lamp.c4d','ext':'C4D','bytes':200,'changed':'2026-10-18 12:12','preview':'/thumb/fake.jpg'},
            {'id': self.i2,'name':'lamp','kind':'模型','full':self.p2,'rel':'B/lamp.c4d','ext':'C4D','bytes':300,'changed':'2026-10-18 12:13','preview':''},
        ]
        (self.legacy / 'covers' / 'real-user-copied.jpg').write_bytes(b'fake-jpg-fixture-only')
        self.write('catalog.json', {'assets':self.assets,'summary':{'root':'X:\\models','assetCount':2}})
        self.write('covers.json', {self.i1:'real-user-copied.jpg'})
        self.write('ui-categories.v1.json',{'schema':1,'categories':['灯具','其他'],'assignments':{self.i2:'灯具'}})

    def write(self,name,obj):
        (self.legacy / name).write_text(json.dumps(obj,ensure_ascii=False),encoding='utf-8')

    def file_hashes(self):
        return {p.relative_to(self.legacy).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in self.legacy.rglob('*') if p.is_file()}

    def test_dry_run_does_not_create_any_files(self):
        before=self.file_hashes()
        self.assertEqual(dry_run(self.legacy)['assets'],2)
        self.assertEqual(before,self.file_hashes())
        self.assertEqual(list(self.root.iterdir()),[self.legacy])

    def test_import_maintains_each_legacy_id_and_cover_category_mapping(self):
        before=self.file_hashes()
        dest=self.root/'beta'
        result=import_legacy(self.legacy,dest)
        self.assertEqual(result['assets'],2)
        self.assertEqual(result['covers'],1)
        self.assertEqual(before,self.file_hashes())
        with sqlite3.connect(dest/'catalog.sqlite3') as c:
            row=c.execute('SELECT id,full_path FROM assets WHERE legacy_id=?',(self.i1,)).fetchone()
            self.assertEqual(row[1],self.p1)
            self.assertNotEqual(row[0],self.i1)
            self.assertEqual(c.execute('SELECT filename FROM covers WHERE asset_id=?',(row[0],)).fetchone()[0],'real-user-copied.jpg')
            self.assertEqual(c.execute('SELECT category FROM assignments JOIN assets ON assets.id=assignments.asset_id WHERE legacy_id=?',(self.i2,)).fetchone()[0],'灯具')
            self.assertEqual(c.execute('SELECT COUNT(*) FROM assets').fetchone()[0],2)
        report=json.loads((dest/'migration-report.json').read_text('utf-8'))
        self.assertEqual(report['covers'],1)
        self.assertEqual((dest/'covers'/'real-user-copied.jpg').read_bytes(),b'fake-jpg-fixture-only')
        self.assertEqual((dest/'legacy-snapshot'/'catalog.json').read_bytes(),(self.legacy/'catalog.json').read_bytes())

    def test_idempotency_never_overwrites_prior_import(self):
        dest=self.root/'stable'
        import_legacy(self.legacy,dest)
        sentinel=dest/'a-file-we-do-not-touch.txt'
        sentinel.write_text('hello')
        with self.assertRaises(MigrationError):import_legacy(self.legacy,dest)
        self.assertEqual(sentinel.read_text(),'hello')

    def test_two_profiles_can_import_without_shared_writable_db(self):
        beta=self.root/'beta'
        stable=self.root/'stable'
        import_legacy(self.legacy,beta)
        import_legacy(self.legacy,stable)
        self.assertNotEqual((beta/'catalog.sqlite3').resolve(),(stable/'catalog.sqlite3').resolve())
        with sqlite3.connect(beta/'catalog.sqlite3') as c:
            c.execute("UPDATE assets SET name='edited beta' WHERE legacy_id=?",(self.i1,))
        with sqlite3.connect(stable/'catalog.sqlite3') as c:
            self.assertEqual(c.execute('SELECT name FROM assets WHERE legacy_id=?',(self.i1,)).fetchone()[0],'lamp')

    def test_duplicate_old_ids_rejected_before_staging(self):
        self.assets[1]['id']=self.i1
        self.write('catalog.json',{'assets':self.assets})
        with self.assertRaisesRegex(MigrationError,'重复'):import_legacy(self.legacy,self.root/'out')
        self.assertFalse((self.root/'out').exists())

    def test_missing_cover_rejected_before_staging(self):
        (self.legacy/'covers'/'real-user-copied.jpg').unlink()
        with self.assertRaisesRegex(MigrationError,'封面文件丢失'):import_legacy(self.legacy,self.root/'out')
        self.assertFalse((self.root/'out').exists())

    def test_uppercase_legacy_id_links_are_preserved(self):
        self.write('covers.json', {self.i1.upper():'real-user-copied.jpg'})
        self.write('ui-categories.v1.json', {'schema':1,'categories':['灯具'],'assignments':{self.i2.upper():'灯具'}})
        dest=self.root/'out'
        r=import_legacy(self.legacy,dest)
        self.assertEqual((r['covers'],r['assignments']),(1,1))
        with sqlite3.connect(dest/'catalog.sqlite3') as c:
            self.assertEqual(c.execute('SELECT COUNT(*) FROM covers').fetchone()[0],1)
            self.assertEqual(c.execute('SELECT COUNT(*) FROM assignments').fetchone()[0],1)

    def test_unknown_cover_id_is_reported_never_dropped(self):
        self.write('covers.json',{'0'*64:'real-user-copied.jpg'})
        with self.assertRaisesRegex(MigrationError,'找不到素材'):import_legacy(self.legacy,self.root/'out')
        self.assertFalse((self.root/'out').exists())

    def test_unknown_category_assignment_is_reported(self):
        self.write('ui-categories.v1.json',{'schema':1,'categories':['灯具'],'assignments':{'0'*64:'灯具'}})
        with self.assertRaisesRegex(MigrationError,'找不到素材'):import_legacy(self.legacy,self.root/'out')

    def test_missing_category_definition_is_error(self):
        self.write('ui-categories.v1.json',{'schema':1,'categories':[],'assignments':{self.i2:'灯具'}})
        with self.assertRaisesRegex(MigrationError,'分类不存在'):import_legacy(self.legacy,self.root/'out')

    def test_reject_path_traversal_cover(self):
        self.write('covers.json',{self.i1:'../leak.jpg'})
        with self.assertRaisesRegex(MigrationError,'文件名无效'):import_legacy(self.legacy,self.root/'out')

    def test_malformed_json_rejected_and_old_files_intact(self):
        (self.legacy/'catalog.json').write_text('{bad-json',encoding='utf-8')
        prior=self.file_hashes()
        with self.assertRaisesRegex(MigrationError,'JSON 损坏'):import_legacy(self.legacy,self.root/'out')
        self.assertEqual(prior,self.file_hashes())

    def test_import_rejects_nested_destination_in_legacy(self):
        with self.assertRaises(MigrationError):import_legacy(self.legacy,self.legacy/'v2')
        self.assertFalse((self.legacy/'v2').exists())

    def test_import_never_needs_source_model_to_exist(self):
        self.assertFalse(Path(self.p1).exists())
        self.assertEqual(import_legacy(self.legacy,self.root/'out')['assets'],2)

    def test_three_verified_recovery_points_and_retention(self):
        dest=self.root/'beta'
        import_legacy(self.legacy,dest)
        backups=self.root/'backups'
        for _ in range(4):
            create_recovery_point(dest,backups,retain=3)
        dirs=list(backups.glob('backup-*'))
        self.assertEqual(len(dirs),3)
        for p in dirs:
            manifest=json.loads((p/'verify.json').read_text())
            for rel,h in manifest.items():
                self.assertEqual(hashlib.sha256((p/rel).read_bytes()).hexdigest(),h)
            with sqlite3.connect(p/'catalog.sqlite3') as c:
                self.assertEqual(c.execute('PRAGMA integrity_check').fetchone()[0],'ok')

    def test_recovery_failure_does_not_delete_previous_point(self):
        dest=self.root/'beta'
        import_legacy(self.legacy,dest)
        backups=self.root/'backups'
        point=create_recovery_point(dest,backups)
        (dest/'catalog.sqlite3').write_bytes(b'not a database')
        with self.assertRaises(sqlite3.DatabaseError):create_recovery_point(dest,backups)
        self.assertTrue((point/'catalog.sqlite3').is_file())


if __name__=='__main__': unittest.main()