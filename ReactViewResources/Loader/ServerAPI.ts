import { nativeAPIObjectName } from "./Internal/Environment";

var returnValues = new Object();
var websocket;

export async function setWebSocketsConnection() {
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
    OpenTooltip
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
                ResizePopup((document.body.firstElementChild as HTMLElement)?.offsetWidth, document.body.scrollHeight);
            }
            break;
        case Operation[Operation.ResizePopup]:
            ResizePopup(object.Arguments["Width"], object.Arguments["Height"]);
            break;
        case Operation[Operation.ReturnValue]:
            returnValues[objectNameValue] = object.Arguments;
            break;
        case Operation[Operation.OpenURL]:
            window.open(objectNameValue, "_blank")?.focus();
            break;
        case Operation[Operation.OpenURLInPopup]:
        case Operation[Operation.OpenTooltip]:
            OpenURLInPopup(objectNameValue);
            break;
        default:
            throw "NotImplemented";
    }
}

function ResizePopup(width: number, height: number) {
    var frameElem = window.frameElement as HTMLElement;
    frameElem.style.height = height + "px";
    frameElem.style.width = width + "px";
    setTimeout(() => frameElem.style.opacity = "1", 200);
}

function OpenURLInPopup(url) {
    var ifrm = window.top.document.createElement("iframe");
    ifrm.setAttribute("src", url);
    ifrm.style.position = "fixed";
    ifrm.style.top = "30px";
    ifrm.style.left = "50%";
    ifrm.style.width = "1000px";
    ifrm.style.transform = "translate(-50%, 0)";
    ifrm.style.zIndex = "2147483647";
    ifrm.style.resize = "both";
    ifrm.style.overflow = "auto";
    ifrm.frameBorder = "0";
    ifrm.style.boxShadow = "2px 2px 6px #aaa";
    ifrm.style.opacity = "0";
    ifrm.style.transitionProperty = "opacity";
    ifrm.style.transitionDuration = ".2s";
    window.top.document.body.appendChild(ifrm);
    ifrm.focus();
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
    window[registerObjectName] = new Object() as any;
    var windowObject = window[registerObjectName] as any;
    object.methods.forEach(function (method) {
        var methodName = method["MethodName"];
        if (method["ReturnType"].ClassName != "System.Void") {
            windowObject[lowerFirstLetter(methodName)] = async function (...theArgs) {
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

function lowerFirstLetter(string) {
    return string.charAt(0).toLowerCase() + string.slice(1);
}


