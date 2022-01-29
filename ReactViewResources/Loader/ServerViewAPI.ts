import { nativeAPIObjectName } from "./Internal/Environment";
import { OpenURLInPopup, ResizePopup } from "./Internal/ServerViewAPIPopups";
import { OpenMenu } from "./Internal/ServerViewAPIMenus";


var returnValues = new Object();
var websocket: WebSocket;

export async function setWebSocketsConnection() {
    var base = document.createElement('base');
    base.href = "/" + nativeAPIObjectName + "/";
    document.head.appendChild(base);
    history.replaceState("", "", "/");
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

    offscreenCanvasPolyFillForFirefoxAndSafari();
}

enum Operation {
    RegisterObjectName,
    UnregisterObjectName,
    EvaluateScriptFunctionWithSerializedParams,
    Execute,
    ResizePopup,
    ReturnValue,
    OpenURL,
    OpenURLInNewTab,
    OpenURLInPopup,
    OpenContextMenu,
    SetBrowserURL,
    MenuClicked,
    CloseWindow
}

function offscreenCanvasPolyFillForFirefoxAndSafari() {
    if ((() => {
        try { return (new OffscreenCanvas(0, 0)).getContext("2d"); }
        catch { return null; }
    })() == null) {
        // @ts-ignore
        window.OffscreenCanvas = class OffscreenCanvas {
            constructor(width, height) {
                var canvas = this["canvas"];
                canvas = document.createElement("canvas");
                canvas.width = width;
                canvas.height = height;
                canvas.convertToBlob = () => {
                    return new Promise(resolve => {
                        canvas.toBlob(resolve);
                    });
                };
                return canvas;
            }
        };
    }
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
            break;
        case Operation[Operation.ResizePopup]:
            setTimeout(() =>
                ResizePopup(JSON.parse(objectNameValue), () => websocket.send(JSON.stringify({ "CloseWindow": true })))
            , 0);
            break;
        case Operation[Operation.ReturnValue]:
            returnValues[objectNameValue] = object.Arguments;
            break;
        case Operation[Operation.OpenURL]:
            document.location = objectNameValue;
            break;
        case Operation[Operation.OpenURLInNewTab]:
            window.open(objectNameValue, "_blank")?.focus();
            break;
        case Operation[Operation.OpenContextMenu]:
            OpenMenu(JSON.parse(objectNameValue), hashCode => websocket.send(JSON.stringify({ "MenuClicked": hashCode })));
            break;
        case Operation[Operation.OpenURLInPopup]:
            OpenURLInPopup(objectNameValue);
            break;
        case Operation[Operation.SetBrowserURL]:
            history.replaceState("", "", objectNameValue);
            document.title = objectNameValue.match("[^\/]*$")[0];
            break;
        default:
            throw "NotImplemented";
    }
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
                    return "/" + nativeAPIObjectName + "/"; // This is needed for UI Editor
                } else if (methodName == "GetStylesInfo" && registerObjectName.endsWith("StylesEditorView")) {
                    return null; // TODO TCS Fix wiget styles editor properly (without this the socket seems to eter in a deadlock or something)
                }
                var methodCall = { ObjectName: registerObjectName, MethodName: methodName, Args: theArgs, CallKey: Math.round(Math.random() * 1000000) };
                reloadIfClosedSocket();
                websocket.send(JSON.stringify(methodCall));
                return await getReturnValue(methodCall.CallKey, methodCall);
            }
        } else {
            windowObject[lowerFirstLetter(methodName)] = function (...theArgs) {
                var methodCall = { ObjectName: registerObjectName, MethodName: methodName, Args: theArgs };
                reloadIfClosedSocket();
                websocket.send(JSON.stringify(methodCall));
            }
        }
    });
}

function reloadIfClosedSocket() {
    if (websocket.readyState == WebSocket.CLOSED) {
        var hasOpenModule = (/\/\w+$/).test(document.location.href);
        var elem = document.createElement("div");
        document.body.appendChild(elem);
        elem.outerHTML = "<div style='position:fixed;top:0;bottom:0;left:0;right:0;cursor:wait;display:flex;align-items:center;justify-content:center;z-index: 2147483647;background: #ffffff4d;'><div style='font-size:14px;background:var(--body-background-color);padding: 50px 100px;border-radius: 11px;box-shadow: 1px 1px 10px var(--shadow-level-uniform-color1);'>Reloading" + (hasOpenModule ? " auto-saved module" : "") + "...</div></div>";
        window.top.location.reload();
    }
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


