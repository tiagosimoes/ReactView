import { waitForDOMReady } from "./Internal/Common";
import { libsPath, mainFrameName, webViewRootId, nativeAPIObjectName } from "./Internal/Environment";
import { loadScript } from "./Internal/ResourcesLoader";
import { newView, ViewMetadata } from "./Internal/ViewMetadata";

declare function define(name: string, dependencies: string[], definition: Function);
var returnValues = new Object();

async function bootstrap() {
    await waitForDOMReady();

    const rootElement = document.getElementById(webViewRootId);
    if (!rootElement) {
        throw new Error("Root element not found");
    }

    const mainView = newView(0, mainFrameName, true, rootElement);
    mainView.head = document.head;
    mainView.root = rootElement;

    var websocket = await setWebSocketsConnection();
    if (websocket != null) {
        websocket.onmessage = onWebSocketMessageReceived;
    }
    window["websocket"] = websocket;
    await loadFramework(mainView);

    const loader = await import("./Loader");
    loader.initialize(mainView);
}

function onWebSocketMessageReceived(event) {
    var object = JSON.parse(event.data);
    var objectName = Object.getOwnPropertyNames(object)[0]
    var objectNameValue = object[objectName];
    switch (objectName) {
        case "RegisterObjectName":
            registerObject(objectNameValue, object.Object);
            break;
        case "UnregisterObjectName":
            delete window[objectNameValue];
            break;
        case "Execute":
            execute(objectNameValue, object.Arguments)
            break;
        case "ReturnValue":
            returnValues[objectNameValue] = object.Arguments;
            break;
        case "OpenURL":
            window.open(objectNameValue, "_blank")?.focus();
            break;
        case "OpenURLInPopup":
            OpenURLInPopup(objectNameValue);
            break;
        default:
            throw "NotImplemented";
    }
}

function OpenURLInPopup(url) {
    var ifrm = document.createElement("iframe");
    ifrm.setAttribute("src", url);
    ifrm.style.width = "50vw";
    ifrm.style.height = "50vh";
    ifrm.style.position = "fixed";
    ifrm.style.top = "25vh";
    ifrm.style.left = "25vw";
    document.body.appendChild(ifrm);
    ifrm.focus();
}

function execute(script, args) {
    if (args != null) {
        eval(script + "(" + JSON.stringify(args) + ")");
    } else {
        eval(script);
    }
} 

function registerObject(registerObjectName: string, object: any) {
    window[registerObjectName] = new Object() as any;
    var windowObject = window[registerObjectName] as any;
    object.methods.forEach(function (method) {
        if (method["ReturnType"].ClassName != "System.Void") {
            windowObject[lowerFirstLetter(method["MethodName"])] = async function (...theArgs) {
                var methodCall = { ObjectName: registerObjectName, MethodName: method["MethodName"], Args: theArgs, CallKey: Math.round(Math.random() * 1000000) };
                window["websocket"].send(JSON.stringify(methodCall));
                return await getReturnValue(methodCall.CallKey, methodCall);
            }
        } else {
            windowObject[lowerFirstLetter(method["MethodName"])] = function (...theArgs) {
                var methodCall = { ObjectName: registerObjectName, MethodName: method["MethodName"], Args: theArgs };
                window["websocket"].send(JSON.stringify(methodCall));
            }
        }
    });
}
async function getReturnValue(callKey: number, methodCall:object): Promise<object> {
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


async function setWebSocketsConnection(): Promise<WebSocket> {
    return new Promise<WebSocket>((resolve) => {
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
}

async function loadFramework(view: ViewMetadata): Promise<void> {
    const reactLib: string = "React";
    const reactDOMLib: string = "ReactDOM";
    const externalLibsPath = libsPath + "node_modules/";

    await loadScript(externalLibsPath + "prop-types/prop-types.min.js", view); /* Prop-Types */
    await loadScript(externalLibsPath + "react/umd/react.production.min.js", view); /* React */
    await loadScript(externalLibsPath + "react-dom/umd/react-dom.production.min.js", view); /* ReactDOM */

    define("react", [], () => window[reactLib]);
    define("react-dom", [], () => window[reactDOMLib]);
}

bootstrap();