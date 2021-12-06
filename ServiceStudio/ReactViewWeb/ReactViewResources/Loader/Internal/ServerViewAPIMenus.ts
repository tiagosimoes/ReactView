import { disableMouseInteractions, enableMouseInteractions } from "./InputManager";

var mouseX, mouseY;

document.onmousemove = (event: PointerEvent) => { mouseX = event.clientX; mouseY = event.clientY };
document.body.oncontextmenu = () => false;

export function OpenMenu(menus, onMenuClick: Function) {
    var divMenu = document.createElement("div");
    divMenu.classList.add("serverview_contextMenu");
    divMenu.style.position = "absolute";
    divMenu.style.padding = "5px 0";
    divMenu.style.border = "1px solid var(--line-divider-color);";
    divMenu.style.borderRadius = "2px";
    divMenu.style.background = "var(--body-background-color)";
    divMenu.style.boxShadow = "2px 2px 4px var(--shadow-level-uniform-color1)";
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
            subMenuItem.style.padding = "5px 10px";
            subMenuItem.style.color = menuItem.IsEnabled ? "var(--body-font-color)" : "var(--text-disabled-color)";
            subMenuItem.onclick = () => menuItem.Items.length > 0 ? OpenMenu(menuItem.Items, onMenuClick) : menuClicked(menuItem.HashCode);
        } else {
            subMenuItem = document.createElement("hr"); /* separator */
            subMenuItem.style.border = "0px";
            subMenuItem.style.borderTop = "1px solid var(--line-divider-color);";
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