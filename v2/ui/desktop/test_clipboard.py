"""UI regression tests for real native capability wiring; native behavior is tested by the EXE smoke."""
from . import test_desktop as base
import unittest
import re

HTML, BRIDGE, APP = base.HTML, base.BRIDGE, base.APP


class ClipboardUIContract(unittest.TestCase):
    setUpClass = classmethod(base.DesktopUIContract.setUpClass.__func__)
    tearDownClass = classmethod(base.DesktopUIContract.tearDownClass.__func__)
    tearDown = base.DesktopUIContract.tearDown
    def setUp(self):
        self.page = self.browser.new_page(viewport={"width": 1016, "height": 689})
        raw = re.sub(r'<script[^>]*src="[^"]+"[^>]*></script>', "", HTML.read_text(encoding="utf-8"))
        self.page.set_content(raw)
        self.page.evaluate("""() => {
          window.testCalls = []; window.testClipboardError = false;
          const assets = ['a','b'].map((letter, index) => ({
            id:letter.repeat(64), name:'Model '+letter, fileName:letter+'.c4d', extension:'C4D',
            relativePath:letter+'.c4d', folder:'.', sizeBytes:100, hasCover:true
          }));
          window.assetLibraryHost = {request:async (action,payload) => {
            window.testCalls.push({action,payload});
            if(action==='getHostInfo') return {capabilities:['getHostInfo','getLibrary','setCoverFromClipboard','searchSimilarClipboard','importFolder','locateAsset']};
            if(action==='getLibrary') return {catalog:{assets,root:'D:/fixture',rootName:'fixture'}};
            if(window.testClipboardError) throw Error('剪贴板中没有图片');
            if(action==='setCoverFromClipboard') return {asset:assets.find(a=>a.id===payload.assetId),revision:'f'.repeat(32)};
            if(action==='searchSimilarClipboard') return {matches:[{assetId:'b'.repeat(64),distance:0},{assetId:'a'.repeat(64),distance:64}],skipped:0};
          }};
        }""")
        self.page.add_script_tag(content=BRIDGE.read_text(encoding="utf-8"))
        self.page.add_script_tag(content=APP.read_text(encoding="utf-8"))
        self.page.wait_for_function("document.documentElement.dataset.libraryReady === 'true'")

    def test_clipboard_controls_save_and_report_actual_failure(self):
        self.assertFalse(self.page.locator('#pasteCover').is_disabled())
        self.assertFalse(self.page.locator('#findImage').is_disabled())
        self.page.locator('#pasteCover').click()
        self.page.wait_for_function("document.documentElement.dataset.coverState === 'saved'")
        self.assertIn('?revision=', self.page.locator('article.asset img').first.get_attribute('src'))
        self.page.evaluate('window.testClipboardError = true')
        self.page.locator('#pasteCover').click()
        self.page.wait_for_function("document.documentElement.dataset.coverState === 'error'")
        self.assertIn('没有图片', self.page.locator('#toast').inner_text())

    def test_ctrl_v_does_not_hijack_search_field_paste(self):
        self.page.locator('#search').focus()
        self.page.locator('#search').press('Control+v')
        self.assertEqual(0, self.page.evaluate("testCalls.filter(x=>x.action==='setCoverFromClipboard').length"))
        self.page.locator('article.asset').first.focus()
        self.page.locator('article.asset').first.press('Control+v')
        self.page.wait_for_function("document.documentElement.dataset.coverState === 'saved'")
        self.assertEqual(1, self.page.evaluate("testCalls.filter(x=>x.action==='setCoverFromClipboard').length"))

    def test_image_search_sorts_and_all_assets_clears_results(self):
        self.page.locator('#findImage').click()
        self.page.wait_for_function("document.documentElement.dataset.imageSearchState === 'complete'")
        self.assertEqual('b'*64,self.page.locator('article.asset').first.get_attribute('data-id'))
        self.assertIn('基础相似度',self.page.locator('#pageName').inner_text())
        self.page.locator('#showAll').click()
        self.assertEqual('a'*64,self.page.locator('article.asset').first.get_attribute('data-id'))

