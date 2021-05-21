import { waitForDOMReady } from "./Internal/Common";
import { libsPath, mainFrameName, webViewRootId } from "./Internal/Environment";
import { loadScript } from "./Internal/ResourcesLoader";
import { newView, ViewMetadata } from "./Internal/ViewMetadata";

declare function define(name: string, dependencies: string[], definition: Function);

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
    var registerObjectName = object["RegisterObjectName"];
    if (registerObjectName != null) {
        window[registerObjectName] = new Object() as any;
        var windowObject = window[registerObjectName] as any;
        object.Object.methods.forEach(function (method) {
            if (method["ReturnType"].ClassName != "System.Void") {
                windowObject[lowerFirstLetter(method["MethodName"])] = async function (args) {
                    var methodCall = { ObjectName: registerObjectName, MethodName: method["MethodName"], Args: args };
                    return await sendMessage(JSON.stringify(methodCall));
                }
            } else {
                windowObject[lowerFirstLetter(method["MethodName"])] =  function (args) {
                    var methodCall = { ObjectName: registerObjectName, MethodName: method["MethodName"], Args: args };
                    window["websocket"].send(JSON.stringify(methodCall));
                }
            }
        });
    }
}

async function sendMessage(methodCall): Promise<object> {
    return new Promise((resolve, reject) => {
        var result = window["websocket"].send(JSON.stringify(methodCall));
        setTimeout(() => resolve(result), 1000)
    });
}

function lowerFirstLetter(string) {
    return string.charAt(0).toLowerCase() + string.slice(1);
}


async function setWebSocketsConnection(): Promise<WebSocket> {
    return new Promise<WebSocket>((resolve) => {
        if (document.location.protocol.startsWith("http")) {
            var docLocation = document.location;
            var webSocketLocation = docLocation.protocol.replace("http", "ws") + "//" + docLocation.host + "/ws";
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