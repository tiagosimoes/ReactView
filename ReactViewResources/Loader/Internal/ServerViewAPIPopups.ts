
export function ResizePopup(windowSettings: object, onClose: Function = () => null ) {
    var ifrm = window.frameElement as HTMLFrameElement;
    var frameRoot = ifrm.contentDocument!.getElementById("webview_root");
    var alreadyHasTitle = (ifrm.contentDocument!.body.childElementCount > 1);
    var titleMinHeight = 36;
    if (frameRoot?.classList.contains("fit-content-width")) {
        windowSettings["Width"] = frameRoot?.scrollWidth;
    }
    if (frameRoot?.classList.contains("fit-content-height")) {
        // need these next 2 lines because guided tutorial needs resizing between steps
        ifrm.style.height = "2000px";
        frameRoot.style.height = "auto";
        windowSettings["Height"] = frameRoot.scrollHeight + titleMinHeight;
        frameRoot.style.height = "calc(100% - " + titleMinHeight + "px)";
    }
    let isResizable = windowSettings["IsResizable"] as Boolean
    if (isResizable) {
        ifrm.style.height = (windowSettings["Height"] + titleMinHeight) + "px";
        ifrm.style.resize = "both";
    } else {
        ifrm.style.height = windowSettings["Height"] + "px";
    }
    ifrm.style.width = windowSettings["Width"] + "px";
    if (!alreadyHasTitle) {
        SetDialogTitle();
    }
    ifrm.style.opacity = "1";
    window.top.document.body.style.cursor = "";
    function SetDialogTitle() {
        var title = ifrm.contentDocument!.createElement("div");
        ifrm.contentDocument!.body.insertBefore(title, frameRoot);
        frameRoot!.style.height = "calc(100% - " + titleMinHeight + "px)";
        title.textContent = windowSettings["Title"];
        title.style.background = "var(--body-background-color)";
        title.style.padding = "10px 15px";
        title.style.minHeight = titleMinHeight + "px"
        title.style.color = "var(--aggregator-subeditor-header-text-color)";
        title.style.fontWeight = "var(--emphasize-font-weight)";
        title.style.fontSize = "13px";
        var closeButton = ifrm.contentDocument!.createElement("span");
        closeButton.textContent = "✕";
        closeButton.style.position = "absolute";
        closeButton.style.right = "16px";
        closeButton.onclick = () => onClose();
        title.appendChild(closeButton);
        title.draggable = true;
        title.ondragstart = (event: DragEvent) => {
            ifrm.dataset.xOffset = ((event.screenX - ifrm.offsetLeft) as unknown as string);
            ifrm.dataset.yOffset = ((event.screenY - ifrm.offsetTop) as unknown as string);
        };
        title.ondrag = (event: DragEvent) => {
            if (event.screenX > 0) {
                ifrm.style.left = event.screenX - (ifrm.dataset.xOffset as any) + "px";
                ifrm.style.top = event.screenY - (ifrm.dataset.yOffset as any) + "px";
            }
        };
    }
}

export function OpenURLInPopup(url) {
    var topDocument = window.top.document;
    var ifrm = topDocument.createElement("iframe");
    ifrm.style.position = "fixed";
    ifrm.style.top = "30px";
    ifrm.style.left = "50%";
    ifrm.style.width = "100vw";
    ifrm.style.height = "0px";
    ifrm.style.transform = "translate(-50%, 0)";
    ifrm.style.zIndex = "2147483647";
    ifrm.style.overflow = "auto";
    ifrm.style.boxShadow = "1px 1px 10px var(--shadow-level-uniform-color1)";
    ifrm.style.opacity = "0";
    ifrm.style.transitionProperty = "opacity";
    ifrm.style.transitionDuration = ".1s";
    ifrm.frameBorder = "0";
    ifrm.setAttribute("src", url);
    ifrm.focus();
    topDocument.body.appendChild(ifrm);
    topDocument.body.style.cursor = "progress";
}
