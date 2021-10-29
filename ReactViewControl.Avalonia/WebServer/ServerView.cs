using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace ReactViewControl.WebServer {
    class ServerView {

        delegate object CallTargetMethod(Func<object> target);

        private ReactViewRender.NativeAPI nativeAPI;
        public string NativeAPIName;
        public DateTime LastActivity;
        readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        public bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

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

            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI)objectToBind;
                NativeAPIName = name;
                ServerAPI.NewNativeObject(this);
            }
            _ = SendWebSocketMessageRegister(name, objectToBind);
            return true;
        }

        public void UnregisterWebJavaScriptObject(string name) {
            if (registeredObjects.ContainsKey(name)) {
                _ = SendWebSocketMessage(ServerAPI.Operation.UnregisterObjectName, name);
                registeredObjects.Remove(name);
            }
        }

        internal bool IsOpen() {
            return webSocket != null && webSocket.State == WebSocketState.Open;
        }

        public void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            _ = SendWebSocketMessage(ServerAPI.Operation.Execute, functionName, JsonSerializer.Serialize(args));
        }
        private void ReturnValue(float callKey, object value) {
            _ = SendWebSocketMessage(ServerAPI.Operation.ReturnValue, callKey.ToString(), JsonSerializer.Serialize(value));
        }

        private readonly Dictionary<string, JsonElement> evaluateResults = new Dictionary<string, JsonElement>();

        internal Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string method, object[] args) {
            var evaluateKey = Guid.NewGuid().ToString();
            _ = SendWebSocketMessageEvaluate(method, evaluateKey, args);
            while (!evaluateResults.ContainsKey(evaluateKey)) {
                Task.Delay(50);
            }
            return Task.FromResult<T>(JsonSerializer.Deserialize<T>((evaluateResults[evaluateKey]).GetRawText()));
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

        internal async Task SendWebSocketMessage(ServerAPI.Operation operation, string value, string arguments = "[]") {
            await SendWebSocketMessage($"{{ \"{operation}\": \"{JsonEncodedText.Encode(value)}\", \"Arguments\":{arguments} }}");
        }

        private async Task SendWebSocketMessageRegister(string name, object objectToBind) {
            await SendWebSocketMessage($"{{ \"{ServerAPI.Operation.RegisterObjectName}\": \"{name}\", \"Object\": {SerializedObject.SerializeObject(objectToBind)} }}");
        }

        private async Task SendWebSocketMessageEvaluate(string method, string evaluateKey, object[] args) {
            await SendWebSocketMessage($"{{ \"{ServerAPI.Operation.EvaluateScriptFunctionWithSerializedParams}\": \"{JsonEncodedText.Encode(method)}\", \"EvaluateKey\":\"{evaluateKey}, \"Arguments\":{JsonSerializer.Serialize(args)} }}");
        }

        private async Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
            while (webSocket == null) {
                await Task.Delay(10);
            }
            await webSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ListenForMessages(WebSocket webSocket) {
            var buffer = new byte[1024 * 1024];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                LastActivity = DateTime.Now;
                ReceiveMessage(text);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            ServerAPI.CloseSocket(this);
        }

        internal string GetViewName() {
            while (nativeAPI.ViewRender.Host == null) {
                Task.Delay(10);
            }
            return nativeAPI.ViewRender.Host.GetType().Name;
        }

        private async void SetPopupDimensionsIfNeeded() {
            if (GetViewName() == "ReactViewHostForPlugins" || GetViewName() == "DialogView") {
                while (!nativeAPI.ViewRender.IsInitialized) {
                    await Task.Delay(10);
                }
                await Task.Delay(100);
                var dimensions = nativeAPI.ViewRender.Bounds;
                if (dimensions.Width != 0) {
                    while (webSocket == null) {
                        await Task.Delay(10);
                    }
                    _ = SendWebSocketMessage(ServerAPI.Operation.ResizePopup, "ResizePopup", JsonSerializer.Serialize(dimensions));
                }
            }
        }

        internal void SetSocket(WebSocket socket) {
            webSocket = socket;
            _ = ListenForMessages(webSocket);
            SetPopupDimensionsIfNeeded();
        }
    }
 }
