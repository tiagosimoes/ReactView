import { nativeAPIObjectName } from "./Internal/Environment";
import { disableMouseInteractions, enableMouseInteractions } from "./Internal/InputManager";

var returnValues = new Object();
var websocket;
var mouseX, mouseY;

export async function setWebSocketsConnection() {
    var base = document.createElement('base');
    base.href = "/" + nativeAPIObjectName + "/";
    document.head.appendChild(base);
    history.pushState("", "", "/");
    websocket = await new Promise<WebSocket>((resolve) => {
        if (document.location.protocol.startsWith("http")) {
            var docLocation = document.location;
            var webSocketLocation = docLocation.protocol.replace("http", "ws") + "//" + docLocation.host + "/" + nativeAPIObjectName;
            var webSocket = new WebSocket(webSocketLocation);
            webSocket.onopen = function () {
                resolve(webSocket);
            }
        } else {
            resolve();
        }
    });
    if (websocket != null) {
        websocket.onmessage = onWebSocketMessageReceived;
        websocket.onclose = () => window.close();
    }
    document.onmousemove = (event: PointerEvent) => { mouseX = event.clientX; mouseY = event.clientY };
    document.body.oncontextmenu = () => false;
}

enum Operation {
    RegisterObjectName,
    UnregisterObjectName,
    EvaluateScriptFunctionWithSerializedParams,
    Execute,
    ResizePopup,
    ReturnValue,
    OpenURL,
    OpenURLInPopup,
    OpenTooltip,
    OpenContextMenu,
    MenuClicked,
    CloseWindow
}

function onWebSocketMessageReceived(event) {
    var object = JSON.parse(event.data);
    var objectName = Object.getOwnPropertyNames(object)[0]
    var objectNameValue = object[objectName];
    switch (objectName) {
        case Operation[Operation.RegisterObjectName]:
            registerObject(objectNameValue, object.Object);
            break;
        case Operation[Operation.UnregisterObjectName]:
            delete window[objectNameValue];
            if (objectNameValue == nativeAPIObjectName) {
                if (window.frameElement == null) {
                    //close browser tab
                    window.close();
                } else {
                    // close popup
                    window.frameElement.parentElement?.removeChild(window.frameElement);
                }
            }
            break;
        case Operation[Operation.EvaluateScriptFunctionWithSerializedParams]:
            var result = execute(objectNameValue, object.Arguments);
            var evaluateKeyName = Object.getOwnPropertyNames(object)[1]
            var evaluateKey = object[evaluateKeyName];
            var evaluatedResult = { EvaluateKey: evaluateKey, EvaluatedResult: result };
            websocket.send(JSON.stringify(evaluatedResult));
            break;
        case Operation[Operation.Execute]:
            execute(objectNameValue, object.Arguments)
            if (objectNameValue == "__Modules__(\"\",\"0\",\"Dialog.view\").setInnerView") {
                setTimeout(() => {
                    var ifrm = window.frameElement as HTMLFrameElement;
                    ifrm.style.width = (document.body.firstElementChild as HTMLElement)?.offsetWidth + "px";
                    ifrm.style.height = document.body.scrollHeight + "px";
                }, 200);
            }
            break;
        case Operation[Operation.ResizePopup]:
            ResizePopup(JSON.parse(objectNameValue));
            break;
        case Operation[Operation.ReturnValue]:
            returnValues[objectNameValue] = object.Arguments;
            break;
        case Operation[Operation.OpenURL]:
            window.open(objectNameValue, "_blank")?.focus();
            break;
        case Operation[Operation.OpenContextMenu]:
            OpenMenu(JSON.parse(objectNameValue));
            break;
        case Operation[Operation.OpenURLInPopup]:
        case Operation[Operation.OpenTooltip]:
            OpenURLInPopup(objectNameValue);
            break;
        default:
            throw "NotImplemented";
    }
}

function OpenMenu(menus) {
    var divMenu = document.createElement("div");
    divMenu.classList.add("serverview_contextMenu");
    divMenu.style.position = "absolute";
    divMenu.style.padding = "5px 0";
    divMenu.style.border = "1px solid #ccc";
    divMenu.style.borderRadius = "2px";
    divMenu.style.background = "#fff";
    divMenu.style.boxShadow = "2px 2px 4px #ccc";
    var menuClicked = (hashCode) => {
        websocket.send(JSON.stringify({ "MenuClicked": hashCode }));
        enableMouseInteractions();
        document.querySelectorAll(".serverview_contextMenu").forEach(elem => document.body.removeChild(elem))
    }
    menus.forEach((menuItem) => {
        var subMenuItem;
        if (menuItem.Header != null) {
            subMenuItem = document.createElement("div");
            subMenuItem.dataset.Header = menuItem.Header;
            subMenuItem.textContent = menuItem.Header.replace("_", "");
            subMenuItem.style.padding = "5px 10px";
            subMenuItem.style.color = menuItem.IsEnabled ? "var(--body-font-color)" : "var(--text-disabled-color)";
            subMenuItem.onclick = () => menuItem.Items.length > 0 ? OpenMenu(menuItem.Items): menuClicked(menuItem.HashCode);
        } else {
            subMenuItem = document.createElement("hr"); /* separator */
            subMenuItem.style.border = "0px";
            subMenuItem.style.borderTop = "1px solid #ccc";
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

function ResizePopup(windowSettings: object) {
    var ifrm = window.frameElement as HTMLFrameElement;
    var titleMinHeight = 36;
    let isResizable = windowSettings["IsResizable"] as Boolean
    if (isResizable) { 
        ifrm.style.height = (windowSettings["Height"] + titleMinHeight) + "px";
        ifrm.style.resize = "both";
    } else {
        ifrm.style.height = windowSettings["Height"] + "px";
    }
    ifrm.style.width = windowSettings["Width"] + "px";
    SetDialogTitle();
    setTimeout(() => ifrm.style.opacity = "1", 200);
    function SetDialogTitle() {
        var title = ifrm.contentDocument!.createElement("div");
        var frameRoot = ifrm.contentDocument!.getElementById("webview_root");
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
        closeButton.onclick = () => websocket.send(JSON.stringify({ "CloseWindow": true }));
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

function OpenURLInPopup(url) {
    var topDocument = window.top.document;
    var ifrm = topDocument.createElement("iframe");
    ifrm.style.position = "fixed";
    ifrm.style.top = "30px";
    ifrm.style.left = "50%";
    ifrm.style.width = "1000px";
    ifrm.style.transform = "translate(-50%, 0)";
    ifrm.style.zIndex = "2147483647";
    ifrm.style.overflow = "auto";
    ifrm.style.boxShadow = "2px 2px 6px #aaa";
    ifrm.style.opacity = "0";
    ifrm.style.transitionProperty = "opacity";
    ifrm.style.transitionDuration = ".2s";
    ifrm.frameBorder = "0";
    ifrm.setAttribute("src", url);
    ifrm.focus();
    topDocument.body.appendChild(ifrm);

}

function execute(script, args) {
    if (args != null) {
        if (Array.isArray(args)) {
            return eval(script + "(" + args.map(arg => JSON.stringify(arg)).join(",") + ")");
        } else {
            return eval(script + "(" + JSON.stringify(args) + ")");
        }
    } else {
        return eval(script);
    }
}

function registerObject(registerObjectName: string, object: any) {
    var lowerFirstLetter = (string)  => string.charAt(0).toLowerCase() + string.slice(1);
    window[registerObjectName] = new Object() as any;
    var windowObject = window[registerObjectName] as any;
    object.methods.forEach(function (method) {
        var methodName = method["MethodName"];
        if (method["ReturnType"].ClassName != "System.Void") {
            windowObject[lowerFirstLetter(methodName)] = async function (...theArgs) {
                if (methodName == "GetBaseUrl" && registerObjectName.endsWith("UIEditorView")) {
                    return "/"; // TODO TCS, fix the loading of UI Editor resources in a better way
                }
                var methodCall = { ObjectName: registerObjectName, MethodName: methodName, Args: theArgs, CallKey: Math.round(Math.random() * 1000000) };
                websocket.send(JSON.stringify(methodCall));
                return await getReturnValue(methodCall.CallKey, methodCall);
            }
        } else {
            windowObject[lowerFirstLetter(methodName)] = function (...theArgs) {
                var methodCall = { ObjectName: registerObjectName, MethodName: methodName, Args: theArgs };
                websocket.send(JSON.stringify(methodCall));
            }
        }
    });
}

async function getReturnValue(callKey: number, methodCall: object): Promise<object> {
    return new Promise((resolve, reject) => {
        var interval = setInterval(() => {
            if (returnValues[callKey] !== undefined) {
                var result = returnValues[callKey]
                delete returnValues[callKey];
                resolve(result);
                if (interval != 0) {
                    clearInterval(interval);
                    interval = 0;
                }
            }
        }, 10);
        setTimeout(() => {
            if (interval != 0) {
                clearInterval(interval);
                interval = 0;
                throw "timeout after 30s waiting for " + JSON.stringify(methodCall); // TODO TCS Review this timeout
            }
        }, 30000)
    });
}


