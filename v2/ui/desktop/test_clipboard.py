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
            relativePath:letter+'.c4d', folder:'.', sizeBytes:100, hasCover:true,
            sourceRootId:letter.repeat(64),sourceRootName:'Folder '+letter
          }));
          window.assetLibraryHost = {request:async (action,payload) => {
            window.testCalls.push({action,payload});
            if(action==='getHostInfo') return {capabilities:['getHostInfo','getLibrary','setCoverFromClipboard','searchSimilarClipboard','importFolder','rescanLibrary','locateAsset','beginAssetDrag']};
            if(action==='getLibrary' || action==='rescanLibrary' || action==='importFolder') return {catalog:{assets,root:'D:/fixture',rootName:'fixture',sourceRoots:['a','b'].map(letter=>({id:letter.repeat(64),name:'Folder '+letter,path:'D:/'+letter,available:true}))},addedCount:0,newCoverCount:0};
            if(action==='beginAssetDrag') return {dropped:false};
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

    def test_root_navigation_and_rescan_preserve_both_directories(self):
        self.assertEqual(2,self.page.locator('[data-filter^="source:"]').count())
        self.page.locator('[data-filter="source:'+'b'*64+'"]').click()
        self.assertEqual(1,self.page.locator('article.asset').count())
        self.assertEqual('b'*64,self.page.locator('article.asset').get_attribute('data-id'))
        self.assertEqual('Folder b',self.page.locator('#pageName').inner_text())
        self.page.locator('#rescanLibrary').click()
        self.page.wait_for_function("testCalls.some(x=>x.action==='rescanLibrary') && document.querySelectorAll('article.asset').length===2")
        self.assertIn({'action':'rescanLibrary','payload':{}},self.page.evaluate('testCalls'))

    def test_two_cards_stay_compact_and_side_by_side(self):
        self.page.set_viewport_size({"width": 1920, "height": 1080})
        self.page.wait_for_timeout(100)
        first = self.page.locator('article.asset').nth(0).bounding_box()
        second = self.page.locator('article.asset').nth(1).bounding_box()
        area = self.page.locator('#assets').bounding_box()
        self.assertGreater(second['x'], first['x'] + first['width'] - 1)
        self.assertLess(abs(first['y'] - second['y']), 2)
        self.assertLessEqual(first['width'], 300)
        self.assertLessEqual(second['width'], 300)

    def test_ctrl_wheel_resizes_cards_not_sidebar_or_page_and_clamps(self):
        self.page.set_viewport_size({'width':1920,'height':1080})
        self.page.wait_for_timeout(100)
        width = lambda: self.page.locator('article.asset').first.bounding_box()['width']
        sidebar = self.page.locator('.nav').bounding_box()['width']
        before = width()
        self.page.locator('.main').dispatch_event('wheel', {'ctrlKey':True,'deltaY':-120})
        self.page.wait_for_timeout(50)
        self.assertGreater(width(), before)
        self.page.locator('.main').dispatch_event('wheel', {'ctrlKey':True,'deltaY':120})
        self.page.wait_for_timeout(50)
        self.assertEqual(width(), before)
        self.page.locator('.main').dispatch_event('wheel', {'ctrlKey':False,'deltaY':120})
        self.page.wait_for_timeout(50)
        self.assertEqual(width(), before)
        self.assertEqual(sidebar,self.page.locator('.nav').bounding_box()['width'])
        for direction, expected in ((120,160),(-120,480)):
            self.page.evaluate('(deltaY)=>{for(let i=0;i<40;i++)document.querySelector(".main").dispatchEvent(new WheelEvent("wheel",{ctrlKey:true,deltaY,bubbles:true,cancelable:true}));}', direction)
            self.page.wait_for_timeout(50)
            self.assertEqual(width(), expected)
        self.page.locator('#gridBtn').click()
        self.assertEqual(width(),480)
        self.page.locator('#boardBtn').click()
        self.page.wait_for_timeout(50)
        self.assertEqual(width(),480)

    def test_cover_image_cannot_be_dragged_as_a_browser_url(self):
        self.assertFalse(self.page.locator('article.asset img').first.evaluate('img => img.draggable'))
        self.assertFalse(self.page.locator('#detail img').evaluate('img => img.draggable'))

    def test_drag_requires_mouse_threshold_and_sends_only_model_id(self):
        card = self.page.locator('article.asset').first.bounding_box()
        x, y = card['x'] + 80, card['y'] + 80
        self.page.mouse.move(x, y)
        self.page.mouse.down()
        self.page.mouse.move(x + 2, y + 2)
        self.assertEqual([], self.page.evaluate("testCalls.filter(x=>x.action==='beginAssetDrag')"))
        self.page.mouse.move(x + 40, y + 20, steps=4)
        self.page.mouse.up()
        self.page.wait_for_function("document.documentElement.dataset.dragState === 'cancelled'")
        self.assertEqual([{'action':'beginAssetDrag','payload':{'assetId':'a'*64}}],
                         self.page.evaluate("testCalls.filter(x=>x.action==='beginAssetDrag')"))

    def test_masonry_uses_actual_image_ratio_and_reflows_without_overlap(self):
        self.page.set_viewport_size({'width':1920,'height':1080})
        self.page.locator('article.asset img').first.evaluate("img => img.src = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"800\"/>')")
        self.page.wait_for_function("document.querySelector('article.asset').style.getPropertyValue('--ar') === '0.5'")
        self.page.evaluate("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))")
        cover = self.page.locator('article.asset .cover').first.bounding_box()
        self.assertAlmostEqual(cover['width']/cover['height'], .5, places=2)
        for width in (1016, 1920):
            self.page.set_viewport_size({'width':width,'height':1080})
            self.page.evaluate("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))")
            boxes = self.page.locator('article.asset').evaluate_all('cards => cards.map(c => {const r=c.getBoundingClientRect();return {l:r.left,r:r.right,t:r.top,b:r.bottom};})')
            a,b = boxes
            self.assertTrue(a['r'] <= b['l'] or b['r'] <= a['l'] or a['b'] <= b['t'] or b['b'] <= a['t'])

