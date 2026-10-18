"""Browser visual/behavior smoke tests; no Windows C4D/Velopack claims."""
from pathlib import Path
import os
import unittest
import re
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[3]
HTML = Path(__file__).parent / 'index.html'
BRIDGE = ROOT / 'v2/ui/update/bridge.js'
APP = HTML.parent / 'app.js'

class UIContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._pw=sync_playwright().start()
        cls.browser=cls._pw.chromium.launch(headless=True,executable_path=os.environ.get('CHROMIUM_BIN') or None,args=['--no-sandbox'])
    @classmethod
    def tearDownClass(cls):
        cls.browser.close()
        cls._pw.stop()

    def setUp(self):
        self.page=self.browser.new_page(viewport={'width':1016,'height':689})
        self.load_page()
    def load_page(self, count=None):
        raw=HTML.read_text(encoding='utf-8')
        raw=re.sub(r'<script[^>]*src="[^"]+"[^>]*></script>','',raw)
        self.page.set_content(raw)
        if count is not None:self.page.evaluate('(n)=>window.__syntheticCount=n', count)
        self.page.add_script_tag(content=BRIDGE.read_text(encoding='utf-8'))
        self.page.add_script_tag(content=APP.read_text(encoding='utf-8'))
    def tearDown(self):
        self.page.close()
    def test_synthetic_cards_and_private_images_are_not_embedded(self):
        self.assertEqual(self.page.locator('article.asset').count(),15)
        self.assertIn('合成测试',self.page.locator('#pageCount').inner_text())
        self.assertEqual(self.page.locator('img[src^="data:image/webp;base64"]').count(),0)
    def test_search_filter(self):
        self.page.locator('#search').fill('几何球')
        self.assertEqual(self.page.locator('article.asset').count(),2)
        self.page.locator('#search').fill('never-a-match')
        self.assertEqual(self.page.locator('article.asset').count(),0)
    def test_layout_theme_and_category(self):
        self.page.locator('#theme').click()
        self.assertIn('dark',self.page.locator('body').get_attribute('class'))
        self.page.locator('#listBtn').click()
        self.assertIn('listmode',self.page.locator('#app').get_attribute('class'))
        self.page.locator('article.asset').first.click()
        self.page.locator('#setcat').select_option(label='未分类')
        self.assertIn('未分类',self.page.locator('#detail').inner_text())
    def test_no_native_check_never_says_latest(self):
        self.page.locator('#checkUpdate').click()
        self.assertIn('检查更新失败',self.page.locator('#updateStatus').inner_text())
        self.assertTrue(self.page.locator('#nativeUpdate').is_disabled())
    def test_no_native_actions_explain_host_missing(self):
        self.page.locator('#importFolder').click()
        self.assertIn('HTML 预览',self.page.locator('#toast').inner_text())
    def test_compact_and_large_viewports_no_horizontal_page_overflow(self):
        for width,height in [(1016,689),(1920,1080)]:
            self.page.set_viewport_size({'width':width,'height':height})
            overflow=self.page.evaluate('document.documentElement.scrollWidth > innerWidth')
            self.assertFalse(overflow,f'page overflowed at {width}x{height}')
    def test_1000_unique_synthetic_assets(self):
        self.load_page(1000)
        self.assertEqual(self.page.locator('article.asset').count(),1000)
        self.assertIn('1000',self.page.locator('#pageCount').inner_text())

if __name__=='__main__': unittest.main()