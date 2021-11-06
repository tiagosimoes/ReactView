import { nativeAPIObjectName } from "./Internal/Environment";
import { OpenURLInPopup, ResizePopup } from "./Internal/ServerViewAPIPopups";
import { OpenMenu } from "./Internal/ServerViewAPIMenus";


var returnValues = new Object();
var websocket;

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
            break;
        case Operation[Operation.ResizePopup]:
            setTimeout(() =>
                ResizePopup(JSON.parse(objectNameValue), () => websocket.send(JSON.stringify({ "CloseWindow": true })))
            , 200);
            break;
        case Operation[Operation.ReturnValue]:
            returnValues[objectNameValue] = object.Arguments;
            break;
        case Operation[Operation.OpenURL]:
            window.open(objectNameValue, "_blank")?.focus();
            break;
        case Operation[Operation.OpenContextMenu]:
            OpenMenu(JSON.parse(objectNameValue), hashCode => websocket.send(JSON.stringify({ "MenuClicked": hashCode })));
            break;
        case Operation[Operation.OpenURLInPopup]:
        case Operation[Operation.OpenTooltip]:
            OpenURLInPopup(objectNameValue);
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
                }
                if (methodName == "GetStylesInfo" && registerObjectName.endsWith("StylesEditorView")) {
                    return null; // TODO TCS Fix wiget styles editor properly (without this the socket seems to eter in a deadlock or something)
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


