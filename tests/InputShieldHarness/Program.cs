// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Linq;
using UnknownInventoryTerminal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InputShieldHarness;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Only the fullscreen blocker accepts shield interaction; native menus stay active but hidden", NativeMenusAreHidden),
            ("Close restores borrowed menu state and releases added CanvasGroups", CloseRestoresMenus),
            ("Close and reopen in one frame reuse added components safely", SameFrameReopen),
            ("Destroyed shield and scene roots can be replaced without leaking menu leases", DestroyedRootsAndSceneChange),
            ("Close restores the drag cursor without hiding or replacing it", ClosePreservesDragVisual),
            ("Native tooltips keep their own Canvas and visibility across close, reopen and scene replacement", NativeTooltipRemainsGameOwned),
            ("Control hints render above item and native visuals without consuming input", TooltipLayers),
            ("Transparent hit surfaces do not depend on sprite alpha or transparent mesh culling", TransparentHitSurface),
            ("Fixed cell hits relay only to live native buttons without changing pointer coordinates", RelayNativeItemHit),
            ("Transfer controls retain a separate raycast path after the amount loses selection", ModalPointerSurvivesBlur),
            ("Cancel and Escape release owned selection without leaking the dismissal click", ModalDismissReleasesInput),
            ("Modal teardown and immediate reopen preserve the original inventory raycaster state", ModalResetAndReopen),
        };
        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {ex}");
            }
            finally
            {
                PlayerCamera.main = null;
                GlobalDark.main = null;
                EventSystem.current = null;
                foreach (var root in GameObject.Created) Object.Destroy(root);
                Object.EndFrame();
                GameObject.Created.Clear();
            }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} InputShield and hit-surface tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void NativeMenusAreHidden()
    {
        var radial = new GameObject("Native radial", typeof(Canvas), typeof(GraphicRaycaster));
        var radialCanvas = radial.GetComponent<Canvas>();
        radialCanvas.sortingOrder = 12;
        var radialRaycaster = radial.GetComponent<GraphicRaycaster>();
        var container = new GameObject("Inactive native container");
        container.SetActive(false);
        var radialButton = new GameObject("Radial button", typeof(Image));
        radialButton.transform.SetParent(radial.transform, false);
        var unrelated = new GameObject("Unrelated game UI");
        PlayerCamera.main = new PlayerCamera { radialMenu = radial.transform, containerMenu = container };
        var shield = new InputShield();

        shield.SetVisible(true);
        var blocker = GameObject.Created.Single(root => root.Name == "Block clicks");
        var rect = blocker.GetComponent<RectTransform>();
        Check(shield.IsBlocker(blocker) && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
              rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero && blocker.GetComponent<Image>().raycastTarget,
            "the shield should cover the entire viewport");
        Check(!shield.IsBlocker(radialButton) && !shield.IsBlocker(container) && !shield.IsBlocker(unrelated),
            "only the shield's own hierarchy is registered as a blocker");
        Check(radial.activeSelf && !container.activeSelf, "the shield must not change native root activeSelf");
        Check(IsHidden(radial) && IsHidden(container), "radial and container should reject pointer interaction even without an open container");
        Check(radialCanvas.sortingOrder == 12 && !radialCanvas.overrideSorting && radialRaycaster.enabled &&
              !container.GetComponent<Canvas>() && !container.GetComponent<GraphicRaycaster>(),
            "native menus must not be promoted or gain a GraphicRaycaster");

        container.SetActive(true); // The game can still open or animate its menu itself.
        shield.SetVisible(true);
        Check(container.activeSelf && IsHidden(container), "native activation must not bypass modal isolation");
        shield.SetVisible(false);
        Check(!shield.IsBlocker(blocker) && !blocker.activeSelf && container.activeSelf,
            "close must disable the blocker without closing a native menu");
        shield.Dispose();
    }

    private static void CloseRestoresMenus()
    {
        var radial = new GameObject("Radial", typeof(CanvasGroup));
        var borrowed = radial.GetComponent<CanvasGroup>();
        borrowed.alpha = 0.375f;
        borrowed.interactable = false;
        borrowed.blocksRaycasts = true;
        borrowed.enabled = false;
        var container = new GameObject("Container");
        PlayerCamera.main = new PlayerCamera { radialMenu = radial.transform, containerMenu = container };
        var shield = new InputShield();

        shield.SetVisible(true);
        var added = container.GetComponent<CanvasGroup>();
        Check(IsHidden(radial) && IsHidden(container), "both borrowed and added CanvasGroups should hide their menu");
        shield.SetVisible(true); // A refresh must not capture the leased values as the original.
        shield.SetVisible(false);
        Check(!borrowed.enabled && borrowed.alpha == 0.375f && !borrowed.interactable && borrowed.blocksRaycasts,
            "close should restore every original CanvasGroup setting immediately");
        Check(added.alpha == 1f && added.interactable && added.blocksRaycasts,
            "an added CanvasGroup should already be neutral before destruction");
        Object.EndFrame();
        Check(!added && !container.GetComponent<CanvasGroup>(), "the temporary group survived frame-end cleanup");
        Check(ReferenceEquals(borrowed, radial.GetComponent<CanvasGroup>()), "the borrowed group must survive as the same instance");
        shield.Dispose();
    }

    private static void SameFrameReopen()
    {
        var radial = new GameObject("Radial");
        var drag = new GameObject("Drag image", typeof(Image));
        var image = drag.GetComponent<Image>();
        PlayerCamera.main = new PlayerCamera { radialMenu = radial.transform, dragImage = image };
        var shield = new InputShield();
        shield.SetVisible(true);
        var group = radial.GetComponent<CanvasGroup>();
        var canvas = drag.GetComponent<Canvas>();

        shield.SetVisible(false);
        shield.SetVisible(true);
        Object.EndFrame();
        Check(group && ReferenceEquals(group, radial.GetComponent<CanvasGroup>()) && IsHidden(radial),
            "frame-end cleanup must not destroy a reopened menu's active lease");
        Check(canvas && ReferenceEquals(canvas, drag.GetComponent<Canvas>()) && !image.raycastTarget,
            "frame-end cleanup must not destroy a reopened drag visual's active lease");
        shield.SetVisible(false);
        Object.EndFrame();
        Check(!group && !canvas && image.raycastTarget, "the final close should release both added components");
        shield.Dispose();
    }

    private static void DestroyedRootsAndSceneChange()
    {
        var shield = new InputShield();
        var radial = new GameObject("Radial in old scene");
        var oldContainer = new GameObject("Surviving old container", typeof(CanvasGroup));
        var borrowed = oldContainer.GetComponent<CanvasGroup>();
        borrowed.alpha = 0.6f;
        PlayerCamera.main = new PlayerCamera { radialMenu = radial.transform, containerMenu = oldContainer };
        shield.SetVisible(true);
        var oldBlocker = GameObject.Created.Single(root => root.Name == "Block clicks");
        var oldShield = oldBlocker.transform.Parent!.gameObject;
        var oldGroup = radial.GetComponent<CanvasGroup>();

        Object.Destroy(radial);
        Object.Destroy(oldShield);
        Object.EndFrame();
        Check(!oldGroup && !shield.IsBlocker(oldBlocker), "destroyed roots must not remain registered");
        shield.SetVisible(true); // The old camera still holds a destroyed transform reference.
        var newBlocker = GameObject.Created.Single(root => root && root.Name == "Block clicks");
        Check(shield.IsBlocker(newBlocker), "refresh should recreate the blocker after its root is destroyed");
        var nextRadial = new GameObject("Radial in next scene");
        PlayerCamera.main = new PlayerCamera { radialMenu = nextRadial.transform };
        shield.SetVisible(true);
        Check(IsHidden(nextRadial) && borrowed.alpha == 0.6f && borrowed.blocksRaycasts && borrowed.interactable,
            "refresh after a camera swap should restore the surviving old menu and isolate the new one");
        shield.Dispose();
        Object.EndFrame();
        Check(!newBlocker && !nextRadial.GetComponent<CanvasGroup>(), "dispose should destroy the shield and release the scene's new lease");
    }

    private static void ClosePreservesDragVisual()
    {
        var cursor = new GameObject("Active drag cursor", typeof(Image));
        var image = cursor.GetComponent<Image>();
        PlayerCamera.main = new PlayerCamera { dragImage = image };
        var shield = new InputShield();

        shield.SetVisible(true);
        Check(image.enabled && !image.raycastTarget && cursor.GetComponent<Canvas>().sortingOrder == TerminalCanvasOrder.NativeVisuals &&
              !shield.IsBlocker(cursor), "active drag visual should render above the terminal and not accept input");
        shield.SetVisible(false);
        Object.EndFrame();

        Check(ReferenceEquals(image, PlayerCamera.main.dragImage) && image && image.enabled && image.raycastTarget &&
              !cursor.GetComponent<Canvas>(), "close should restore the drag cursor without hiding or replacing it");
        shield.Dispose();
    }

    private static void NativeTooltipRemainsGameOwned()
    {
        var tooltip = new GameObject("Hidden native tooltip", typeof(RectTransform), typeof(Image));
        tooltip.SetActive(false);
        GlobalDark.main = new GlobalDark { tooltipRect = tooltip.GetComponent<RectTransform>() };
        var shield = new InputShield();

        shield.SetVisible(true);
        Check(!tooltip.GetComponent<Canvas>() && !tooltip.activeSelf && tooltip.GetComponent<Image>().raycastTarget,
            "opening the terminal must not add a Canvas or change the native tooltip");
        tooltip.SetActive(true); // GlobalDark can show UI/extension hints while the terminal is open.
        shield.SetVisible(true);
        shield.SetVisible(false);
        shield.SetVisible(true);
        Object.EndFrame();
        Check(tooltip.activeSelf && !tooltip.GetComponent<Canvas>(),
            "same-frame close/reopen must leave the game-owned tooltip rendering through its original parent");

        var replacement = new GameObject("Replacement native tooltip", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        var canvas = replacement.GetComponent<Canvas>();
        canvas.enabled = false;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = 5;
        canvas.sortingOrder = 12;
        GlobalDark.main.tooltipRect = replacement.GetComponent<RectTransform>();
        shield.SetVisible(true);
        Check(!canvas.enabled && canvas.overrideSorting && canvas.sortingLayerID == 5 && canvas.sortingOrder == 12 &&
              replacement.GetComponent<GraphicRaycaster>().enabled && replacement.GetComponent<Image>().raycastTarget,
            "refresh and scene replacement must not borrow an existing tooltip Canvas or its raycasters");
        canvas.enabled = true; // A live game/other-mod change must not be rolled back to a snapshot.
        canvas.sortingOrder = 21;
        replacement.SetActive(false);
        shield.SetVisible(false);
        shield.Dispose();
        Object.EndFrame();
        Check(ReferenceEquals(canvas, replacement.GetComponent<Canvas>()) && canvas.enabled && canvas.overrideSorting &&
              canvas.sortingLayerID == 5 && canvas.sortingOrder == 21 && !replacement.activeSelf,
            "close and dispose must preserve the current game-owned tooltip state");
        replacement.SetActive(true);
        Check(replacement.activeInHierarchy && canvas.isActiveAndEnabled,
            "the next native hover can display the tooltip after terminal teardown");
    }

    private static void TooltipLayers()
    {
        var inventory = new GameObject("Inventory", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)) { layer = 5 };
        var item = TerminalCanvasOrder.AddTooltipLayer(inventory.transform, "Item tooltip", TerminalCanvasOrder.ItemTooltip);
        var hint = TerminalCanvasOrder.AddTooltipLayer(inventory.transform, "Control hint", TerminalCanvasOrder.ControlHint);
        var itemCanvas = item.gameObject.GetComponent<Canvas>();
        var hintCanvas = hint.gameObject.GetComponent<Canvas>();
        Check(TerminalCanvasOrder.Shield < TerminalCanvasOrder.Inventory &&
            TerminalCanvasOrder.Inventory < TerminalCanvasOrder.NativeVisuals &&
            TerminalCanvasOrder.NativeVisuals < itemCanvas.sortingOrder &&
            itemCanvas.sortingOrder < hintCanvas.sortingOrder && hintCanvas.sortingOrder <= short.MaxValue,
            "every overlay must have an unambiguous order on the default sorting layer");
        foreach (var overlay in new[] { item, hint })
        {
            var canvas = overlay.gameObject.GetComponent<Canvas>();
            var group = overlay.gameObject.GetComponent<CanvasGroup>();
            Check(overlay.Parent == inventory.transform && overlay.gameObject.layer == 5 && canvas.overrideSorting && canvas.sortingLayerID == 0 &&
                overlay.anchorMin == Vector2.zero && overlay.anchorMax == Vector2.one &&
                overlay.offsetMin == Vector2.zero && overlay.offsetMax == Vector2.zero,
                "tooltips must stretch across the inventory canvas, not be clipped to a cell or control");
            Check(!group.blocksRaycasts && !group.interactable && !overlay.gameObject.GetComponent<GraphicRaycaster>(),
                "tooltip layers must never become pointer targets");
        }
        inventory.SetActive(false);
        Check(!hint.gameObject.activeInHierarchy && !item.gameObject.activeInHierarchy, "settings and close can hide both layers together");
    }

    private static void TransparentHitSurface()
    {
        var image = new GameObject("Fixed transparent cell", typeof(Image)).GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;
        image.alphaHitTestMinimumThreshold = 0.75f;

        TerminalHitSurface.Enable(image);

        Check(image.color.Equals(Color.clear) && image.raycastTarget && image.alphaHitTestMinimumThreshold == 0 &&
            !image.canvasRenderer.cullTransparentMesh, "the cell must remain invisible while eligible for raycasts");
    }

    private static void RelayNativeItemHit()
    {
        var cell = new GameObject("Fixed cell", typeof(Image));
        var native = new GameObject("Animated native button", typeof(InvButton));
        native.transform.SetParent(cell.transform, false);
        var button = native.GetComponent<InvButton>();
        var pointer = new Vector2(123, 456);
        var hit = new RaycastResult { gameObject = cell, screenPosition = pointer };

        TerminalHitSurface.Relay(ref hit, null);
        Check(hit.gameObject == cell, "empty or group cells should keep the original hit");
        button.enabled = false;
        TerminalHitSurface.Relay(ref hit, button);
        Check(hit.gameObject == cell, "a disabled native button must not receive a hit");
        button.enabled = true;
        cell.SetActive(false);
        TerminalHitSurface.Relay(ref hit, button);
        Check(hit.gameObject == cell, "a native button under a hidden cell must not receive a hit");
        cell.SetActive(true);
        TerminalHitSurface.Relay(ref hit, button);
        Check(hit.gameObject == native && hit.screenPosition == pointer, "relay should only replace the target");
    }

    private static (TerminalModalInput Input, GraphicRaycaster Inventory, GameObject Overlay, GameObject Amount) Modal()
    {
        var inventory = new GameObject("Inventory", typeof(Canvas), typeof(GraphicRaycaster));
        inventory.GetComponent<Canvas>().sortingOrder = TerminalCanvasOrder.Inventory;
        var overlay = TerminalCanvasOrder.CreateModalLayer(5);
        var amount = new GameObject("Amount", typeof(Image));
        amount.transform.SetParent(overlay.transform, false);
        EventSystem.current = new EventSystem();
        var raycaster = inventory.GetComponent<GraphicRaycaster>();
        return (new TerminalModalInput(overlay, raycaster), raycaster, overlay, amount);
    }

    private static void ModalPointerSurvivesBlur()
    {
        var (input, inventory, overlay, amount) = Modal();
        var canvas = overlay.GetComponent<Canvas>();
        var raycaster = overlay.GetComponent<GraphicRaycaster>();
        Check(!overlay.activeSelf && overlay.transform.Parent == null && canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            canvas.sortingLayerID == 0 && canvas.sortingOrder > TerminalCanvasOrder.ControlHint && overlay.layer == 5,
            "the modal needs an independent top-level overlay above the inventory and hints");
        input.Open();
        EventSystem.current!.SetSelectedGameObject(amount);
        EventSystem.current.SetSelectedGameObject(null); // Click the backdrop; leave the modal open.
        input.Update(10);
        Check(input.IsOpen && input.BlocksInput(10) && raycaster.isActiveAndEnabled && !inventory.enabled &&
            EventSystem.current.currentSelectedGameObject == null,
            "blur must neither refocus the field nor let a passive inventory hit cover the modal");
        EventSystem.current.SetSelectedGameObject(amount);
        input.Update(11);
        Check(EventSystem.current.currentSelectedGameObject == amount && !inventory.enabled, "the amount can be selected again");
    }

    private static void ModalDismissReleasesInput()
    {
        var (input, inventory, overlay, amount) = Modal();
        var events = EventSystem.current!;
        input.Open();
        events.SetSelectedGameObject(amount);
        input.Dismiss(10);
        input.Update(10);
        Check(!input.IsOpen && input.BlocksInput(10) && !overlay.GetComponent<GraphicRaycaster>().isActiveAndEnabled &&
            !inventory.enabled && events.currentSelectedGameObject == null,
            "dismissing must deactivate modal hits and clear the typing target without clicking through");
        input.EndInputFrame(10);
        Check(input.BlocksInput(10) && !input.BlocksInput(11) && inventory.isActiveAndEnabled,
            "the raycaster is ready for next frame even if EventSystem updates before the plugin");
        var other = new GameObject("Another selected control");
        events.SetSelectedGameObject(other);
        input.Update(11);
        Check(!input.BlocksInput(11) && inventory.isActiveAndEnabled && events.currentSelectedGameObject == other,
            "the next frame must restore inventory clicks and keep any unrelated selection");
    }

    private static void ModalResetAndReopen()
    {
        var (input, inventory, _, amount) = Modal();
        var events = EventSystem.current!;
        input.Open();
        events.SetSelectedGameObject(amount);
        events.alreadySelecting = true;
        input.Dismiss(10);
        Check(events.currentSelectedGameObject == amount, "do not interrupt an in-progress EventSystem selection");
        events.alreadySelecting = false;
        input.Update(10);
        Check(events.currentSelectedGameObject == null && !inventory.enabled, "retry selection cleanup without releasing clicks early");
        input.Open();
        input.EndInputFrame(10);
        input.Update(11);
        Check(input.IsOpen && !inventory.enabled, "the old dismissal must not release a reopened modal");
        input.Reset(12);
        Check(!input.BlocksInput(12) && inventory.enabled, "panel teardown releases the original raycaster immediately");
        inventory.enabled = false;
        input.Open();
        input.Reset(13);
        Check(!input.BlocksInput(13) && !inventory.enabled, "do not enable a raycaster disabled by someone else before open");
    }

    private static bool IsHidden(GameObject menu)
    {
        var group = menu.GetComponent<CanvasGroup>();
        return group && group.enabled && group.alpha == 0f && !group.interactable && !group.blocksRaycasts;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
