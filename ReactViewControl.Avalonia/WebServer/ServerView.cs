﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace ReactViewControl.WebServer {
    class ServerView {

        static ServerView() {
            ServerService.StartServer();
        }

        delegate object CallTargetMethod(Func<object> target);

        private ReactViewRender.NativeAPI nativeAPI;
        public string NativeAPIName;
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


            var serializedObject = SerializedObject.SerializeObject(objectToBind);
            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI)objectToBind;
                NativeAPIName = name;
                ServerApiStartup.NewNativeObject(this);
            }
            var text = $"{{ \"RegisterObjectName\": \"{name}\", \"Object\": {serializedObject} }}";
            _ = SendWebSocketMessage(text);
            return true;
        }

        public void UnregisterWebJavaScriptObject(string name) {
            var text = $"{{ \"UnregisterObjectName\": \"{name}\"}}";
            if (registeredObjects.ContainsKey(name)) {
                _ = SendWebSocketMessage(text);
                registeredObjects.Remove(name);
            }
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

        internal async Task SendWebSocketMessage(string message) {
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
                ServerApiStartup.SetLastConnectionWithActivity(this);
                ReceiveMessage(text);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            ServerApiStartup.CloseSocket(this);
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
                    var text = $"{{ \"ResizePopup\": \"ResizePopup\", \"Arguments\": {JsonSerializer.Serialize(dimensions)} }}";
                    while (webSocket == null) {
                        await Task.Delay(10);
                    }
                    _ = SendWebSocketMessage(text);
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
