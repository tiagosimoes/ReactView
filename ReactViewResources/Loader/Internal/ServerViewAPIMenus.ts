﻿import { disableMouseInteractions, enableMouseInteractions } from "./InputManager";

var mouseX, mouseY;

document.onmousemove = (event: PointerEvent) => { mouseX = event.clientX; mouseY = event.clientY };
document.body.oncontextmenu = () => false;

window.addEventListener("blur", () => {
    //Remove browser native context menu from UI Editor. TODO TCS: This should later be done in UIEditor code itself
    setTimeout(() => {
        if (document.activeElement!.classList.contains("editor-canvas")) {
            var canvas = document.activeElement!.shadowRoot?.querySelector(".canvas");
            var UIEditorIframe = canvas?.shadowRoot?.querySelector("iframe")?.contentDocument;
            if (UIEditorIframe != null) {
                UIEditorIframe.oncontextmenu = () => false;
                var UIContentIframe = UIEditorIframe?.querySelector("iframe")?.contentDocument;
                if (UIContentIframe != null) {
                    UIContentIframe.oncontextmenu = () => false;
                }
            }
        }
    });
});

export function OpenMenu(menus, onMenuClick: Function) {
    var divMenu = document.createElement("div");
    divMenu.classList.add("serverview_contextMenu");
    divMenu.style.position = "absolute";
    divMenu.style.padding = "5px 0";
    divMenu.style.border = "1px solid var(--line-divider-color);";
    divMenu.style.borderRadius = "2px";
    divMenu.style.background = "var(--body-background-color)";
    divMenu.style.boxShadow = "2px 2px 8px var(--shadow-level-uniform-color1)";
    var menuClicked = (hashCode) => {
        enableMouseInteractions();
        document.querySelectorAll(".serverview_contextMenu").forEach(elem => document.body.removeChild(elem))
        onMenuClick(hashCode);
    }
    menus.forEach((menuItem) => {
        var subMenuItem;
        if (menuItem.Header != null) {
            subMenuItem = document.createElement("div");
            subMenuItem.dataset.Header = menuItem.Header;
            subMenuItem.textContent = menuItem.Header.replace("_", "");
            subMenuItem.style.padding = "5px 20px";
            subMenuItem.style.color = menuItem.IsEnabled ? "var(--body-font-color)" : "var(--text-disabled-color)";
            if (menuItem.Items.length > 0) {
                var subMenuItemArrow = document.createElement("span");
                subMenuItemArrow.textContent = "▶";
                subMenuItemArrow.style.position = "absolute";
                subMenuItemArrow.style.right = "5px";
                subMenuItem.appendChild(subMenuItemArrow);
                subMenuItem.onclick = () => {
                    document.querySelectorAll(".serverview_contextMenu").forEach(elem => { if (elem != divMenu)  document.body.removeChild(elem);});
                    OpenMenu(menuItem.Items, onMenuClick);
                }
            } else {
                subMenuItem.onclick = () => menuClicked(menuItem.HashCode);
            }
        } else {
            subMenuItem = document.createElement("hr"); /* separator */
            subMenuItem.style.border = "0px";
            subMenuItem.style.borderTop = "1px solid var(--line-divider-color)";
            subMenuItem.style.margin = "5px 0";
        }
        divMenu.appendChild(subMenuItem);
    });
    divMenu.style.opacity = "0";
    document.body.appendChild(divMenu);
    divMenu.style.left = mouseX + Math.min(document.body.clientWidth - (mouseX + divMenu.offsetWidth), 0) + "px";
    divMenu.style.top = mouseY + Math.min(document.body.clientHeight - (mouseY + divMenu.offsetHeight), 0) + "px";
    disableMouseInteractions();
    var root_layer = document.getElementById("webview_root_layer") as HTMLElement;
    if (root_layer != null) {
        root_layer.addEventListener("mousedown", () => menuClicked(0));
    }
    divMenu.style.zIndex = "2147483647";
    divMenu.style.transition = "opacity .2s";
    divMenu.style.opacity = "1";
}