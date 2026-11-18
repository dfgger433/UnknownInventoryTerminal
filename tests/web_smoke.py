import os
from pathlib import Path

from playwright.sync_api import sync_playwright


def main() -> None:
    with sync_playwright() as playwright:
        browser_path = os.environ.get("PLAYWRIGHT_CHROME")
        if not browser_path:
            candidate = Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe")
            browser_path = str(candidate) if candidate.exists() else None
        options = {"headless": True}
        if browser_path:
            options["executable_path"] = browser_path
        browser = playwright.chromium.launch(**options)
        page = browser.new_page(viewport={"width": 1280, "height": 900})
        page.goto("http://127.0.0.1:4173", wait_until="networkidle")
        assert page.locator("h1").inner_text() == "STORAGE ARRAY"
        assert page.locator(".item-tile").count() == 8
        page.locator("#search").fill("battery")
        assert page.locator(".item-tile").count() == 1
        assert page.locator(".item-name").inner_text() == "Battery cell"
        page.locator('[data-mode="container"]').click()
        assert page.locator('[data-mode="container"]').get_attribute("aria-selected") == "true"
        page.locator("#clear").click()
        assert page.locator(".item-tile").count() == 8
        page.screenshot(path="tests/web-terminal-smoke.png", full_page=True)
        browser.close()


if __name__ == "__main__":
    main()
