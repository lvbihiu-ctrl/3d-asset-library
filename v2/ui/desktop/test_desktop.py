"""Issue #32 desktop UI contract tests using only synthetic content."""

from pathlib import Path
import os
import re
import unittest

from playwright.sync_api import sync_playwright


ROOT = Path(__file__).resolve().parents[3]
HTML = Path(__file__).parent / "index.html"
BRIDGE = ROOT / "v2/ui/update/bridge.js"
APP = ROOT / "v2/ui/preview/app.js"


def contrast_ratio(rgb_a, rgb_b):
    def channel(value):
        value /= 255
        return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4

    def luminance(rgb):
        red, green, blue = [channel(value) for value in rgb]
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue

    lighter, darker = sorted((luminance(rgb_a), luminance(rgb_b)), reverse=True)
    return (lighter + 0.05) / (darker + 0.05)


class DesktopUIContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._pw = sync_playwright().start()
        executable = os.environ.get("CHROMIUM_BIN") or r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        cls.browser = cls._pw.chromium.launch(
            headless=True,
            executable_path=executable,
            args=["--no-sandbox"],
        )

    @classmethod
    def tearDownClass(cls):
        cls.browser.close()
        cls._pw.stop()

    def setUp(self):
        self.page = self.browser.new_page(viewport={"width": 1016, "height": 689})
        raw = HTML.read_text(encoding="utf-8")
        raw = re.sub(r'<script[^>]*src="[^"]+"[^>]*></script>', "", raw)
        self.page.set_content(raw)
        self.page.add_script_tag(content=BRIDGE.read_text(encoding="utf-8"))
        self.page.add_script_tag(content=APP.read_text(encoding="utf-8"))

    def tearDown(self):
        self.page.close()

    def test_native_only_actions_are_disabled_and_explained(self):
        for control_id in (
            "pasteCover",
            "findImage",
            "locateFile",
            "importFolder",
            "checkUpdate",
        ):
            self.assertTrue(self.page.locator(f"#{control_id}").is_disabled(), control_id)
        status = self.page.locator("#nativeMode")
        self.assertEqual(status.get_attribute("data-state"), "offline")
        self.assertIn("未连接真实素材库", status.inner_text())

    def test_theme_search_category_layout_and_card_selection(self):
        self.page.locator("#theme").click()
        self.assertIn("dark", self.page.locator("body").get_attribute("class"))
        self.assertEqual(self.page.locator("#theme").get_attribute("aria-pressed"), "true")

        self.page.locator("#search").fill("几何球")
        self.assertEqual(self.page.locator("article.asset").count(), 2)
        self.page.locator("#search").fill("")
        self.page.locator("[data-filter='cat:工业设计']").click()
        self.assertEqual(self.page.locator("article.asset").count(), 3)

        self.page.locator("#gridBtn").click()
        self.assertIn("gridmode", self.page.locator("#app").get_attribute("class"))
        card = self.page.locator("article.asset").nth(1)
        expected_name = card.locator(".aname").inner_text()
        card.click()
        self.assertEqual(card.get_attribute("aria-current"), "true")
        self.assertIn(expected_name, self.page.locator("#detail").inner_text())

    def test_empty_search_has_clear_recovery_action(self):
        self.page.locator("#search").fill("never-a-match")
        self.assertEqual(self.page.locator("article.asset").count(), 0)
        self.page.locator("#clearFiltersEmpty").click()
        self.assertEqual(self.page.locator("article.asset").count(), 15)
        self.assertEqual(self.page.locator("#search").input_value(), "")

    def test_keyboard_space_selects_card(self):
        card = self.page.locator("article.asset").nth(1)
        card.focus()
        card.press("Space")
        self.assertEqual(card.get_attribute("aria-current"), "true")

        first = self.page.locator("article.asset").first
        first.focus()
        first.press("ArrowRight")
        self.assertEqual(
            self.page.evaluate("document.activeElement?.dataset.id"),
            self.page.locator("article.asset").nth(1).get_attribute("data-id"),
        )

    def test_desktop_icons_are_consistent_inline_svg(self):
        for control_id in (
            "theme",
            "checkUpdate",
            "importFolder",
            "pasteCover",
            "findImage",
            "showAll",
            "locateFile",
            "gridBtn",
            "listBtn",
            "boardBtn",
        ):
            self.assertEqual(
                self.page.locator(f"#{control_id} svg.icon").count(),
                1,
                control_id,
            )

    def test_required_viewports_keep_readable_cards_and_inspector(self):
        for width, height in ((820, 600), (1016, 689), (1920, 1080)):
            self.page.set_viewport_size({"width": width, "height": height})
            self.page.evaluate("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))")
            self.assertFalse(
                self.page.evaluate("document.documentElement.scrollWidth > innerWidth"),
                f"horizontal overflow at {width}x{height}",
            )
            self.assertNotEqual(
                self.page.locator("#inspector").evaluate("element => getComputedStyle(element).display"),
                "none",
            )
            card_width = self.page.locator("article.asset").first.bounding_box()["width"]
            self.assertGreaterEqual(card_width, 210, f"card too narrow at {width}x{height}")
            self.assertLessEqual(card_width, 330, f"card too wide at {width}x{height}")

    def test_light_and_dark_text_tokens_meet_normal_text_contrast(self):
        self.assertIn(
            "body{margin:0;background:var(--page);color:var(--text)",
            HTML.read_text(encoding="utf-8"),
        )
        for dark in (False, True):
            if dark:
                self.page.locator("#theme").click()
            colors = self.page.evaluate(
                """
                () => {
                  const style = getComputedStyle(document.body);
                  const parse = name => style.getPropertyValue(name).trim()
                    .match(/[0-9a-f]{2}/gi).map(value => parseInt(value, 16));
                  return {
                    text: parse('--text'), muted: parse('--muted'), subtle: parse('--subtle'),
                    paper: parse('--paper'), panel: parse('--panel'), sidebar: parse('--sidebar')
                  };
                }
                """
            )
            self.assertGreaterEqual(contrast_ratio(colors["text"], colors["paper"]), 4.5)
            self.assertGreaterEqual(contrast_ratio(colors["muted"], colors["paper"]), 4.5)
            for background in ("paper", "panel", "sidebar"):
                self.assertGreaterEqual(
                    contrast_ratio(colors["subtle"], colors[background]),
                    4.5,
                    f"subtle text on {background} in {'dark' if dark else 'light'} mode",
                )

    def test_reduced_motion_and_visible_focus_rules_exist(self):
        source = HTML.read_text(encoding="utf-8")
        self.assertIn("prefers-reduced-motion: reduce", source)
        self.page.locator("#theme").focus()
        outline = self.page.locator("#theme").evaluate(
            "element => getComputedStyle(element).outlineWidth"
        )
        self.assertNotEqual(outline, "0px")


if __name__ == "__main__":
    unittest.main()
