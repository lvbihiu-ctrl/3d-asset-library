import unittest
from protect_v2_pr import violation,verify

class SafetyTests(unittest.TestCase):
    def test_good_source(self):
        self.assertEqual(verify(["v2/main.cs","v2/ui/a.html","v2/a.svg",
           "docs/v2/PRODUCT_CONTRACT.md",".github/workflows/v2-pr-safety.yml"]),[])
    def test_models(self):
        for x in ["a.c4d","x/y.fbx","asset.blend","asset.MAX","t.glb","t.obj"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_renders(self):
        for x in ["x.png","x.JPG","x.webp","x.exr","x.psd","x.HEIC"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_private_directory(self):
        for x in ["x/.local-data/index.json","x/covers/a.txt",
                  "client-assets/test.svg","a/thumbs/1.svg"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_frozen_legacy(self):
        for x in ["desktop-updates/native/latest.json",
            "desktop-updates/native/launcher/Program.cs",
            r"desktop-updates\native\app-v0.1.7.ps1"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_archives_signing_keys(self):
        for x in ["x.zip","x.sqlite3","x.exe","x.pfx"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_invalid_paths(self):
        for x in ["","/full/path","../outside","a/./b",r"C:\file.txt"]:
            with self.subTest(x=x):self.assertTrue(violation(x))
    def test_aggregates_findings(self):
        self.assertEqual(len(verify(["v2/main.py","v2/private.jpg","v2/a.c4d"])),2)

if __name__=="__main__":
    unittest.main()
