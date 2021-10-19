using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ReactViewControl.WebServer {
    class ServerView {

        static ServerView() {
            ServerService.StartServer();
        }

        delegate object CallTargetMethod(Func<object> target);

        private ReactViewRender.NativeAPI nativeAPI;
        readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        public bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

            // TODO TCS: Check if this is needded
            if (executeCallsInUI) {
                return RegisterWebJavaScriptObject(name, objectToBind, target => ExecuteInUI<object>(target), false);
            }

            if (interceptCall == null) {
                interceptCall = target => target();
            }

            object CallTargetMethod(Func<object> target) {
                // TODO TCS: Check if this is needded
                //if (isDisposing) {
                //    return null;
                //}
                try {
                    JavascriptPendingCalls.AddCount();
                    //if (isDisposing) {
                    //    // check again, to avoid concurrency problems with dispose
                    //    return null;
                    //}
                    return interceptCall(target);
                } finally {
                    JavascriptPendingCalls.Signal();
                }
            }


            var serializedObject = SerializedObject.SerializeObject(objectToBind);
            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI)objectToBind;
                ServerApiStartup.NewNativeObject(name, this);
            }
            var text = $"{{ \"RegisterObjectName\": \"{name}\", \"Object\": {serializedObject} }}";
            _ = SendWebSocketMessage(text);
            return true;
        }

        private T ExecuteInUI<T>(Func<T> action) {
            if (Dispatcher.UIThread.CheckAccess()) {
                return action();
            }
            return Dispatcher.UIThread.InvokeAsync(action).Result;
        }

        internal Stream GetCustomResource(string path, out string extension) {
            return nativeAPI.ViewRender.GetCustomResource(path, out extension);
        }

        private void ReceiveMessage(string text) {
            if (text.StartsWith("{\"EvaluateKey\"")) {
                var evaluateResult = SerializedObject.DeserializeEvaluateResult(text);
                evaluateResults[evaluateResult.EvaluateKey] = evaluateResult.EvaluatedResult;

            } else {
                var methodCall = SerializedObject.DeserializeMethodCall(text);
                var obj = registeredObjects[methodCall.ObjectName];
                var callTargetMethod = registeredObjectInterceptMethods[methodCall.ObjectName];
                callTargetMethod(() => {
                    var result = SerializedObject.ExecuteMethod(obj, methodCall);
                    if (obj.GetType().GetMethod(methodCall.MethodName).ReturnType != typeof(void)) {
                        ReturnValue(methodCall.CallKey, result);
                    }
                    return result;
                });
            }
        }

        private WebSocket webSocket;

        private async System.Threading.Tasks.Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
            while (webSocket == null) {
                webSocket = await WaitForNextWebSocket();
            }
            await webSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async System.Threading.Tasks.Task<WebSocket> WaitForNextWebSocket() {
            while (webSocket == null) {
                await System.Threading.Tasks.Task.Delay(500);
                webSocket = WebServer.ServerApiStartup.NextWebSocket;
            }
            WebServer.ServerApiStartup.NextWebSocket = null;
            _ = OnWebSocketMessageReceived(webSocket);
            return webSocket;
        }

        private async System.Threading.Tasks.Task OnWebSocketMessageReceived(WebSocket webSocket) {
            var buffer = new byte[1024 * 1024];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ReceiveMessage(text);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        public void UnregisterWebJavaScriptObject(string name) {
            var text = $"{{ \"UnregisterObjectName\": \"{name}\"}}";
            _ = SendWebSocketMessage(text);
            registeredObjects.Remove(name);
        }

        public void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            var text = $"{{ \"Execute\": \"{JsonEncodedText.Encode(functionName)}\", \"Arguments\": {JsonSerializer.Serialize(args)} }}";
            _ = SendWebSocketMessage(text);
        }
        private void ReturnValue(float callKey, object value) {
            var text = $"{{ \"ReturnValue\": \"{callKey}\", \"Arguments\": {JsonSerializer.Serialize(value)} }}";
            _ = SendWebSocketMessage(text);
        }

        private readonly Dictionary<string, JsonElement> evaluateResults = new Dictionary<string, JsonElement>();

        internal Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string method, object[] args) {
            var evaluateKey = Guid.NewGuid().ToString();
            var text = $"{{ \"EvaluateScriptFunctionWithSerializedParams\": \"{JsonEncodedText.Encode(method)}\", \"EvaluateKey\":\"{evaluateKey}\", \"Arguments\": {JsonSerializer.Serialize(args)} }}";
            _ = SendWebSocketMessage(text);
            while (!evaluateResults.ContainsKey(evaluateKey)) {
                Task.Delay(50);
            }
            return Task.FromResult<T>(JsonSerializer.Deserialize<T>((evaluateResults[evaluateKey]).GetRawText()));
        }

        internal string GetViewName() {
            while (nativeAPI.ViewRender.Host == null) {
                System.Threading.Tasks.Task.Delay(10);
            }
            return nativeAPI.ViewRender.Host.GetType().Name;
        }
    }
}
